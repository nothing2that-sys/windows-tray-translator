using WindowsTrayTranslator.Clipboard;
using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Hotkeys;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Security;
using WindowsTrayTranslator.Selection;
using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.Translation.Gemini;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.App;

public sealed class ApplicationController : IDisposable
{
    private readonly ConfigurationService configuration;
    private readonly ILogger logger;
    private readonly KeyboardInputService keyboard;
    private readonly ClipboardService clipboard;
    private readonly ActiveWindowService activeWindow;
    private readonly ISelectedTextProvider selectedTextProvider;
    private readonly GlobalHotkeyService hotkeys;
    private readonly DpapiSecretStore secretStore;
    private readonly HttpClient httpClient;
    private readonly GeminiApiClient geminiClient;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();

    public ApplicationController(
        AppPaths paths,
        ConfigurationService configuration,
        AppSettings settings,
        ILogger logger)
    {
        Paths = paths;
        this.configuration = configuration;
        Settings = settings;
        this.logger = logger;

        keyboard = new KeyboardInputService();
        clipboard = new ClipboardService(
            settings.Translation.ClipboardRetryCount,
            settings.Translation.ClipboardRetryDelayMs,
            keyboard,
            logger);
        activeWindow = new ActiveWindowService();
        ISelectedTextProvider clipboardProvider = new ClipboardSelectedTextProvider(clipboard, keyboard, activeWindow, logger);
        ISelectedTextProvider uiAutomationProvider = new UiAutomationSelectedTextProvider(activeWindow, logger);
        selectedTextProvider = new CompositeSelectedTextProvider(clipboardProvider, uiAutomationProvider, logger);
        secretStore = new DpapiSecretStore(paths.SecretFile);
        httpClient = new HttpClient();
        geminiClient = new GeminiApiClient(httpClient, logger);
        TranslationProvider = new GeminiTranslationProvider(geminiClient, secretStore, () => Settings);

        hotkeys = new GlobalHotkeyService(OnHotkeyPressed, logger);
        try
        {
            hotkeys.Register(settings.Translation.ReadHotkey, settings.Translation.ReplaceHotkey);
        }
        catch (Exception ex)
        {
            logger.Error("전역 단축키를 등록하지 못했습니다.", ex);
            StartupWarning = ex.Message;
        }
    }

    public event EventHandler<UserNotificationEventArgs>? NotificationRequested;
    public event EventHandler<TranslationPopupRequestedEventArgs>? TranslationPopupRequested;

    public AppPaths Paths { get; }
    public AppSettings Settings { get; }
    public ITranslationProvider TranslationProvider { get; }
    public string? StartupWarning { get; }
    public bool HasApiKey => secretStore.HasSecret(SecretNames.GeminiApiKey);

    public void SetTranslationEnabled(bool enabled)
    {
        Settings.General.TranslationEnabled = enabled;
        configuration.Save(Settings);
    }

    public void SaveApiConfiguration(string? newApiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Gemini 모델명을 입력해 주세요.", nameof(model));
        }

        if (!string.IsNullOrWhiteSpace(newApiKey))
        {
            secretStore.WriteSecret(SecretNames.GeminiApiKey, newApiKey);
        }

        Settings.Api.Model = model.Trim();
        configuration.Save(Settings);
        logger.Information($"API 설정을 저장했습니다. Provider=Gemini, Model={Settings.Api.Model}, HasKey={HasApiKey}");
    }

    public void DeleteApiKey()
    {
        secretStore.DeleteSecret(SecretNames.GeminiApiKey);
        logger.Information("저장된 Gemini API 키를 삭제했습니다.");
    }

    public async Task<TranslationResult> TestConnectionAsync(string? unsavedApiKey, CancellationToken cancellationToken)
    {
        string? apiKey = string.IsNullOrWhiteSpace(unsavedApiKey)
            ? secretStore.ReadSecret(SecretNames.GeminiApiKey)
            : unsavedApiKey.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return TranslationResult.Failure(TranslationFailureKind.MissingApiKey, "Gemini API 키를 설정해 주세요.");
        }

        return await geminiClient.TranslateAsync(
            apiKey,
            Settings.Api.Model,
            new TranslationRequest("Hello.", "Korean"),
            Settings.Api.RequestTimeoutSeconds,
            Settings.Api.TransientRetryCount,
            cancellationToken);
    }

    private void OnHotkeyPressed(int hotkeyId) => _ = ExecuteHotkeyAsync(hotkeyId);

    private async Task ExecuteHotkeyAsync(int hotkeyId)
    {
        if (!Settings.General.TranslationEnabled)
        {
            return;
        }

        if (!await operationGate.WaitAsync(0))
        {
            Notify("현재 번역 작업이 진행 중입니다.", ToolTipIcon.Warning);
            return;
        }

        try
        {
            WindowIdentity sourceWindow = activeWindow.Capture();
            SelectedTextResult result = await selectedTextProvider.GetSelectedTextAsync(
                sourceWindow,
                Settings.Translation.ClipboardCopyTimeoutMs,
                Settings.Translation.MaxInputCharacters,
                shutdown.Token);

            if (!result.IsSuccess)
            {
                if (hotkeyId == GlobalHotkeyService.ReadHotkeyId)
                {
                    ShowPopup(TranslationPopupKind.Error, result.ErrorMessage ?? "선택된 문자열을 가져오지 못했습니다.");
                }
                else
                {
                    Notify(result.ErrorMessage ?? "선택된 문자열을 가져오지 못했습니다.", ToolTipIcon.Warning);
                }
                return;
            }

            if (hotkeyId == GlobalHotkeyService.ReadHotkeyId)
            {
                await ExecuteReadTranslationAsync(result.Text!, shutdown.Token);
            }
            else
            {
                await ExecuteReplaceTranslationAsync(result.Text!, result.SourceWindow, shutdown.Token);
            }
        }
        catch (OperationCanceledException)
        {
            logger.Information("선택 문자열 캡처가 취소되었습니다.");
        }
        catch (Exception ex)
        {
            logger.Error("번역 작업 중 예기치 않은 오류가 발생했습니다.", ex);
            if (hotkeyId == GlobalHotkeyService.ReadHotkeyId)
            {
                ShowPopup(TranslationPopupKind.Error, "번역 작업 중 오류가 발생했습니다.");
            }
            else
            {
                Notify("번역 작업 중 오류가 발생했습니다.", ToolTipIcon.Error);
            }
        }
        finally
        {
            operationGate.Release();
        }
    }

    private async Task ExecuteReadTranslationAsync(string originalText, CancellationToken cancellationToken)
    {
        ShowPopup(TranslationPopupKind.Translating, "번역 중...");
        TranslationResult translation = await TranslationProvider.TranslateAsync(
            new TranslationRequest(originalText, Settings.Translation.ReadTargetLanguage),
            cancellationToken);

        if (!translation.IsSuccess)
        {
            logger.Warning($"읽기 번역에 실패했습니다. Kind={translation.FailureKind}, Characters={originalText.Length}");
            ShowPopup(TranslationPopupKind.Error, translation.ErrorMessage ?? "번역에 실패했습니다.");
            return;
        }

        logger.Information($"읽기 번역을 완료했습니다. InputCharacters={originalText.Length}, OutputCharacters={translation.TranslatedText!.Length}");
        ShowPopup(TranslationPopupKind.Success, translation.TranslatedText!, translation.TranslatedText);
    }

    private async Task ExecuteReplaceTranslationAsync(
        string originalText,
        WindowIdentity sourceWindow,
        CancellationToken cancellationToken)
    {
        ShowPopup(TranslationPopupKind.Translating, "번역 중...");
        TranslationResult translation = await TranslationProvider.TranslateAsync(
            new TranslationRequest(originalText, Settings.Translation.ReplaceTargetLanguage),
            cancellationToken);

        if (!translation.IsSuccess)
        {
            logger.Warning($"작성 번역에 실패했습니다. Kind={translation.FailureKind}, Characters={originalText.Length}");
            ShowPopup(TranslationPopupKind.Error, translation.ErrorMessage ?? "번역에 실패했습니다.");
            return;
        }

        string replacement = ReplaceOutputFormatter.Format(
            originalText,
            translation.TranslatedText!,
            ReplaceOutputFormatter.Parse(Settings.Translation.ReplaceFormat));

        if (!activeWindow.IsStillActive(sourceWindow))
        {
            ShowChangedTargetWarning(replacement);
            return;
        }

        using ClipboardSnapshot snapshot = clipboard.CaptureSnapshot();
        if (!activeWindow.IsStillActive(sourceWindow))
        {
            ShowChangedTargetWarning(replacement);
            return;
        }

        try
        {
            PasteTextResult pasteResult = await clipboard.PasteTextAsync(
                replacement,
                snapshot,
                Settings.Translation.ClipboardRestoreDelayMs,
                () => activeWindow.IsStillActive(sourceWindow),
                cancellationToken);
            if (pasteResult == PasteTextResult.TargetChanged)
            {
                ShowChangedTargetWarning(replacement);
                return;
            }
            if (pasteResult == PasteTextResult.ClipboardChanged)
            {
                logger.Warning("클립보드가 변경되어 작성 번역 자동 치환을 취소했습니다.");
                ShowPopup(
                    TranslationPopupKind.Warning,
                    $"클립보드가 변경되어 자동 치환을 취소했습니다.\r\n번역 결과를 확인한 후 복사 버튼을 눌러 주세요.\r\n\r\n{replacement}",
                    replacement);
                return;
            }

            logger.Information($"작성 번역을 완료했습니다. InputCharacters={originalText.Length}, OutputCharacters={replacement.Length}");
            ShowPopup(TranslationPopupKind.Success, "선택 영역을 번역문으로 교체했습니다.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.Error("작성 번역 결과를 붙여넣지 못했습니다.", ex);
            ShowPopup(
                TranslationPopupKind.Warning,
                $"자동 입력에 실패했습니다. 번역 결과를 확인한 후 복사 버튼을 눌러 주세요.\r\n\r\n{replacement}",
                replacement);
        }
    }

    private void ShowChangedTargetWarning(string replacement)
    {
        logger.Warning("활성 창 또는 포커스가 변경되어 작성 번역 자동 치환을 취소했습니다.");
        ShowPopup(
            TranslationPopupKind.Warning,
            $"활성 창이 변경되어 자동 치환을 취소했습니다.\r\n번역 결과를 확인한 후 복사 버튼을 눌러 주세요.\r\n\r\n{replacement}",
            replacement);
    }

    private void Notify(string message, ToolTipIcon icon) =>
        NotificationRequested?.Invoke(this, new UserNotificationEventArgs(message, icon));

    private void ShowPopup(TranslationPopupKind kind, string message, string? copyText = null) =>
        TranslationPopupRequested?.Invoke(this, new TranslationPopupRequestedEventArgs(
            kind,
            message,
            copyText,
            Settings.General.PopupTimeoutMs));

    public void Dispose()
    {
        shutdown.Cancel();
        hotkeys.Dispose();
        httpClient.Dispose();
        shutdown.Dispose();
        operationGate.Dispose();
    }
}

public sealed class UserNotificationEventArgs(string message, ToolTipIcon icon) : EventArgs
{
    public string Message { get; } = message;
    public ToolTipIcon Icon { get; } = icon;
}

public enum TranslationPopupKind
{
    Translating,
    Success,
    Warning,
    Error
}

public sealed class TranslationPopupRequestedEventArgs(
    TranslationPopupKind kind,
    string message,
    string? copyText,
    int timeoutMs) : EventArgs
{
    public TranslationPopupKind Kind { get; } = kind;
    public string Message { get; } = message;
    public string? CopyText { get; } = copyText;
    public int TimeoutMs { get; } = timeoutMs;
}
