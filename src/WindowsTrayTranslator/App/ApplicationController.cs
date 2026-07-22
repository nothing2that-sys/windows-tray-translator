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
        selectedTextProvider = new ClipboardSelectedTextProvider(clipboard, keyboard, activeWindow, logger);
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

    private void OnHotkeyPressed(int hotkeyId) => _ = CaptureSelectedTextAsync(hotkeyId);

    private async Task CaptureSelectedTextAsync(int hotkeyId)
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
                Notify(result.ErrorMessage ?? "선택된 문자열을 가져오지 못했습니다.", ToolTipIcon.Warning);
                return;
            }

            string mode = hotkeyId == GlobalHotkeyService.ReadHotkeyId ? "읽기" : "작성";
            Notify($"{mode} 번역용 문자열 캡처 확인 완료 ({result.Text!.Length}자)\n현재 Phase 1~3 버전은 실제 번역 결과를 아직 표시하지 않습니다.", ToolTipIcon.Info);
        }
        catch (OperationCanceledException)
        {
            logger.Information("선택 문자열 캡처가 취소되었습니다.");
        }
        finally
        {
            operationGate.Release();
        }
    }

    private void Notify(string message, ToolTipIcon icon) =>
        NotificationRequested?.Invoke(this, new UserNotificationEventArgs(message, icon));

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
