using WindowsTrayTranslator.Clipboard;
using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.History;
using WindowsTrayTranslator.Hotkeys;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Security;
using WindowsTrayTranslator.Selection;
using WindowsTrayTranslator.Startup;
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
    private readonly InputAccessService inputAccess;
    private readonly ISelectedTextProvider clipboardFirstTextProvider;
    private readonly ISelectedTextProvider uiAutomationFirstTextProvider;
    private readonly GlobalHotkeyService hotkeys;
    private readonly DpapiSecretStore secretStore;
    private readonly HttpClient httpClient;
    private readonly GeminiApiClient geminiClient;
    private readonly GeminiTranslationProvider directTranslationProvider;
    private readonly StartupRegistrationService startupRegistration;
    private readonly TranslationHistoryStore historyStore;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly object readOperationSync = new();
    private ReadTranslationOperation? activeReadOperation;
    private long nextReadOperationId;

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
        inputAccess = new InputAccessService();
        ISelectedTextProvider clipboardProvider = new ClipboardSelectedTextProvider(clipboard, keyboard, activeWindow, logger);
        ISelectedTextProvider uiAutomationProvider = new UiAutomationSelectedTextProvider(activeWindow, logger);
        clipboardFirstTextProvider = new CompositeSelectedTextProvider(clipboardProvider, uiAutomationProvider, logger);
        uiAutomationFirstTextProvider = new CompositeSelectedTextProvider(uiAutomationProvider, clipboardProvider, logger);
        secretStore = new DpapiSecretStore(paths.SecretFile);
        httpClient = new HttpClient();
        geminiClient = new GeminiApiClient(httpClient, logger);
        directTranslationProvider = new GeminiTranslationProvider(geminiClient, secretStore, () => Settings);
        startupRegistration = new StartupRegistrationService(
            Environment.ProcessPath ?? Application.ExecutablePath);
        historyStore = new TranslationHistoryStore(
            paths.LogDirectory,
            settings.General.EnableLogging,
            settings.General.LogRetentionDays,
            logger);
        TranslationProvider = new RecoveringTranslationProvider(
            () => new GeminiTranslationSession(secretStore, () => Settings, logger),
            logger);

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
    public event EventHandler<LanguageSelectionRequestedEventArgs>? LanguageSelectionRequested;
    public event EventHandler? SettingsApplied;
    public event EventHandler? TranslationHistoryChanged;

    public AppPaths Paths { get; }
    public AppSettings Settings { get; }
    public ITranslationProvider TranslationProvider { get; }
    public string? StartupWarning { get; }
    public bool HasApiKey => secretStore.HasSecret(SecretNames.GeminiApiKey);
    public IReadOnlyList<TranslationHistoryEntry> LoadTranslationHistory() => historyStore.LoadRecent();
    public bool IsStartupEnabled
    {
        get
        {
            try
            {
                return startupRegistration.IsEnabled();
            }
            catch (Exception ex)
            {
                logger.Error("Windows 시작 프로그램 등록 상태를 확인하지 못했습니다.", ex);
                return Settings.General.RunAtWindowsStartup;
            }
        }
    }

    public void SetTranslationEnabled(bool enabled)
    {
        Settings.General.TranslationEnabled = enabled;
        configuration.Save(Settings);
    }

    public void SaveApiConfiguration(string? newApiKey, string model)
    {
        GeminiModelPolicy.EnsureUsable(model);

        if (!string.IsNullOrWhiteSpace(newApiKey))
        {
            secretStore.WriteSecret(SecretNames.GeminiApiKey, newApiKey);
        }

        Settings.Api.Model = model.Trim();
        configuration.Save(Settings);
        logger.Information($"API 설정을 저장했습니다. Provider=Gemini, Model={Settings.Api.Model}, HasKey={HasApiKey}");
    }

    public void ApplySettings(AppSettings candidate, string? newApiKey)
    {
        GeminiModelPolicy.EnsureUsable(candidate.Api.Model);
        if (candidate.Api.EnableFallbackModel)
        {
            GeminiModelPolicy.EnsureUsable(candidate.Api.FallbackModel);
            if (string.Equals(candidate.Api.Model, candidate.Api.FallbackModel, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("기본 모델과 보조 모델은 서로 달라야 합니다.");
            }
        }
        HotkeyDefinition read = HotkeyDefinition.Parse(candidate.Translation.ReadHotkey);
        HotkeyDefinition replace = HotkeyDefinition.Parse(candidate.Translation.ReplaceHotkey);
        if (read == replace)
        {
            throw new InvalidOperationException("읽기 번역과 작성 번역에 같은 단축키를 사용할 수 없습니다.");
        }

        SettingsValidator.Normalize(candidate);
        AppSettings previous = CloneSettings(Settings);
        bool previousStartup = startupRegistration.IsEnabled();
        string? previousApiKey = secretStore.ReadSecret(SecretNames.GeminiApiKey);
        bool apiKeyChanged = !string.IsNullOrWhiteSpace(newApiKey);

        try
        {
            if (apiKeyChanged)
            {
                secretStore.WriteSecret(SecretNames.GeminiApiKey, newApiKey!);
            }

            hotkeys.Register(candidate.Translation.ReadHotkey, candidate.Translation.ReplaceHotkey);
            startupRegistration.SetEnabled(candidate.General.RunAtWindowsStartup);
            configuration.Save(candidate);
            CopySettings(candidate, Settings);
            clipboard.Configure(
                Settings.Translation.ClipboardRetryCount,
                Settings.Translation.ClipboardRetryDelayMs);
            if (logger is FileLogger fileLogger)
            {
                fileLogger.Configure(Settings.General.EnableLogging, Settings.General.LogRetentionDays);
            }
            historyStore.Configure(Settings.General.EnableLogging, Settings.General.LogRetentionDays);

            logger.Information("설정을 저장하고 즉시 적용했습니다.");
        }
        catch
        {
            TryRollbackSettings(previous, previousStartup, previousApiKey, apiKeyChanged);
            throw;
        }

        try
        {
            SettingsApplied?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.Error("설정 적용 알림 처리 중 오류가 발생했습니다.", ex);
        }
    }

    public void DeleteApiKey()
    {
        secretStore.DeleteSecret(SecretNames.GeminiApiKey);
        logger.Information("저장된 Gemini API 키를 삭제했습니다.");
    }

    public Task<TranslationResult> TestConnectionAsync(string? unsavedApiKey, CancellationToken cancellationToken) =>
        TestConnectionAsync(unsavedApiKey, Settings.Api.Model, Settings.Api.RequestTimeoutSeconds, cancellationToken);

    public async Task<TranslationResult> TestConnectionAsync(
        string? unsavedApiKey,
        string model,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            GeminiModelPolicy.EnsureUsable(model);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return TranslationResult.Failure(TranslationFailureKind.ModelUnavailable, ex.Message);
        }

        string? apiKey = string.IsNullOrWhiteSpace(unsavedApiKey)
            ? secretStore.ReadSecret(SecretNames.GeminiApiKey)
            : unsavedApiKey.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return TranslationResult.Failure(TranslationFailureKind.MissingApiKey, "Gemini API 키를 설정해 주세요.");
        }

        return await geminiClient.TranslateAsync(
            apiKey,
            model.Trim(),
            new TranslationRequest("Hello.", "Korean"),
            timeoutSeconds,
            Settings.Api.TransientRetryCount,
            cancellationToken);
    }

    public async Task<GeminiModelListResult> ListModelsAsync(
        string? unsavedApiKey,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        string? apiKey = string.IsNullOrWhiteSpace(unsavedApiKey)
            ? secretStore.ReadSecret(SecretNames.GeminiApiKey)
            : unsavedApiKey.Trim();
        return await geminiClient.ListModelsAsync(apiKey ?? string.Empty, timeoutSeconds, cancellationToken);
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
            bool isReadTranslation = hotkeyId == GlobalHotkeyService.ReadHotkeyId;
            InvalidateActiveReadOperation();
            WindowIdentity sourceWindow = activeWindow.Capture();
            InputAccessResult access = inputAccess.Check(sourceWindow);
            if (!access.IsAllowed)
            {
                ShowPopup(isReadTranslation, TranslationPopupKind.Error, access.ErrorMessage!);
                return;
            }
            string configuredTargetLanguage = isReadTranslation
                ? Settings.Translation.ReadTargetLanguage
                : Settings.Translation.ReplaceTargetLanguage;
            string? targetLanguage = await ResolveTargetLanguageAsync(
                configuredTargetLanguage,
                isReadTranslation,
                shutdown.Token);
            if (targetLanguage is null)
            {
                return;
            }

            if (!activeWindow.IsStillActive(sourceWindow))
            {
                ShowPopup(isReadTranslation, TranslationPopupKind.Warning, "입력 위치가 변경되어 번역을 취소했습니다.");
                return;
            }

            ISelectedTextProvider selectedTextProvider = Settings.Translation.PreferUiAutomation
                ? uiAutomationFirstTextProvider
                : clipboardFirstTextProvider;
            SelectedTextResult result = await selectedTextProvider.GetSelectedTextAsync(
                sourceWindow,
                Settings.Translation.ClipboardCopyTimeoutMs,
                Settings.Translation.MaxInputCharacters,
                shutdown.Token);

            if (!result.IsSuccess)
            {
                if (isReadTranslation)
                {
                    ShowPopup(true, TranslationPopupKind.Error, result.ErrorMessage ?? "선택된 문자열을 가져오지 못했습니다.");
                }
                else
                {
                    Notify(result.ErrorMessage ?? "선택된 문자열을 가져오지 못했습니다.", ToolTipIcon.Warning);
                }
                return;
            }

            targetLanguage = AutomaticTargetLanguageResolver.Resolve(targetLanguage, result.Text!);
            if (isReadTranslation)
            {
                await ExecuteReadTranslationAsync(result.Text!, targetLanguage, shutdown.Token);
            }
            else
            {
                await ExecuteReplaceTranslationAsync(result.Text!, result.SourceWindow, targetLanguage, shutdown.Token);
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
                ShowPopup(true, TranslationPopupKind.Error, "번역 작업 중 오류가 발생했습니다.");
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

    private async Task ExecuteReadTranslationAsync(
        string originalText,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        TranslationRequest request = new(originalText, targetLanguage);
        CancellationTokenSource primaryCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ReadTranslationOperation operation = new(
            Interlocked.Increment(ref nextReadOperationId),
            request,
            Settings.Api.Model,
            Settings.Api.FallbackModel,
            primaryCancellation);
        lock (readOperationSync)
        {
            activeReadOperation = operation;
        }

        if (Settings.General.ShowTranslatingPopup)
        {
            ShowPopup(
                true,
                TranslationPopupKind.Translating,
                "기본 모델에서 번역하고 있습니다…",
                action: HasFallbackModel() ? TranslationPopupAction.SwitchToFallback : TranslationPopupAction.None,
                operationId: operation.Id,
                primaryModel: operation.PrimaryModel);
        }
        TranslationResult translation = await directTranslationProvider.TranslateWithModelAsync(
            request,
            operation.PrimaryModel,
            primaryCancellation.Token);

        bool switchToFallback;
        lock (readOperationSync)
        {
            if (!IsCurrentReadOperation(operation) || operation.Dismissed)
            {
                return;
            }

            operation.PrimaryInProgress = false;
            switchToFallback = operation.SwitchToFallbackRequested;
            if (translation.IsSuccess && !switchToFallback)
            {
                operation.PrimaryText = translation.TranslatedText;
            }
        }

        if (switchToFallback)
        {
            await ExecuteFallbackTranslationAsync(operation, useAsPrimary: true);
            return;
        }

        if (!translation.IsSuccess)
        {
            if (translation.FailureKind == TranslationFailureKind.Cancelled)
            {
                return;
            }

            logger.Warning($"읽기 번역에 실패했습니다. Kind={translation.FailureKind}, Characters={originalText.Length}");
            ShowPopup(
                true,
                TranslationPopupKind.Error,
                translation.ErrorMessage ?? "번역에 실패했습니다.",
                action: HasFallbackModel() ? TranslationPopupAction.RetryFallback : TranslationPopupAction.None,
                operationId: operation.Id,
                primaryModel: operation.PrimaryModel);
            return;
        }

        logger.Information($"읽기 번역을 완료했습니다. InputCharacters={originalText.Length}, OutputCharacters={translation.TranslatedText!.Length}");
        AddHistory(
            TranslationHistoryKind.Read,
            targetLanguage,
            originalText,
            translation.TranslatedText!);
        ShowPopup(
            true,
            TranslationPopupKind.Success,
            translation.TranslatedText!,
            translation.TranslatedText,
            HasFallbackModel() ? TranslationPopupAction.ShowFallback : TranslationPopupAction.None,
            operation.Id,
            primaryModel: operation.PrimaryModel);
    }

    public void RequestFallbackTranslation(long operationId)
    {
        ReadTranslationOperation? operation;
        bool cancelPrimary = false;
        bool useAsPrimary = false;
        lock (readOperationSync)
        {
            operation = activeReadOperation;
            if (operation is null || operation.Id != operationId || operation.Dismissed ||
                operation.FallbackInProgress || !HasFallbackModel())
            {
                return;
            }

            operation.FallbackInProgress = true;
            if (operation.PrimaryInProgress)
            {
                operation.SwitchToFallbackRequested = true;
                cancelPrimary = true;
                useAsPrimary = true;
            }
            else
            {
                useAsPrimary = string.IsNullOrEmpty(operation.PrimaryText);
            }
        }

        if (cancelPrimary)
        {
            ShowPopup(
                true,
                TranslationPopupKind.Translating,
                "기본 모델 요청을 중단하고 백업 모델로 전환하고 있습니다…",
                action: TranslationPopupAction.FallbackInProgress,
                operationId: operation.Id,
                primaryModel: operation.FallbackModel,
                title: "백업 모델로 전환 중");
            operation.PrimaryCancellation.Cancel();
            return;
        }

        if (!useAsPrimary)
        {
            ShowPopup(
                true,
                TranslationPopupKind.Success,
                operation.PrimaryText!,
                operation.PrimaryText,
                TranslationPopupAction.FallbackInProgress,
                operation.Id,
                secondaryMessage: "다른 번역을 만들고 있습니다…",
                primaryModel: operation.PrimaryModel,
                secondaryModel: operation.FallbackModel,
                title: "번역 비교");
        }
        else
        {
            ShowPopup(
                true,
                TranslationPopupKind.Translating,
                "백업 모델에서 다시 번역하고 있습니다…",
                action: TranslationPopupAction.FallbackInProgress,
                operationId: operation.Id,
                primaryModel: operation.FallbackModel,
                title: "백업 모델 번역 중");
        }

        _ = ExecuteFallbackTranslationAsync(operation, useAsPrimary);
    }

    public void CancelReadTranslation(long operationId)
    {
        ReadTranslationOperation? operation;
        lock (readOperationSync)
        {
            operation = activeReadOperation;
            if (operation is null || operation.Id != operationId)
            {
                return;
            }

            operation.Dismissed = true;
        }

        operation.PrimaryCancellation.Cancel();
        operation.FallbackCancellation?.Cancel();
    }

    private async Task ExecuteFallbackTranslationAsync(
        ReadTranslationOperation operation,
        bool useAsPrimary)
    {
        using CancellationTokenSource fallbackCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
        lock (readOperationSync)
        {
            if (!IsCurrentReadOperation(operation) || operation.Dismissed)
            {
                return;
            }

            operation.FallbackInProgress = true;
            operation.FallbackCancellation = fallbackCancellation;
        }

        TranslationResult fallback = await directTranslationProvider.TranslateWithModelAsync(
            operation.Request,
            operation.FallbackModel,
            fallbackCancellation.Token);

        lock (readOperationSync)
        {
            if (!IsCurrentReadOperation(operation) || operation.Dismissed)
            {
                return;
            }

            operation.FallbackInProgress = false;
            operation.FallbackCancellation = null;
            if (fallback.IsSuccess)
            {
                operation.FallbackText = fallback.TranslatedText;
            }
        }

        if (!fallback.IsSuccess)
        {
            if (fallback.FailureKind == TranslationFailureKind.Cancelled)
            {
                return;
            }

            logger.Warning($"백업 모델 번역에 실패했습니다. Kind={fallback.FailureKind}");
            if (!useAsPrimary && !string.IsNullOrEmpty(operation.PrimaryText))
            {
                ShowPopup(
                    true,
                    TranslationPopupKind.Success,
                    operation.PrimaryText,
                    operation.PrimaryText,
                    TranslationPopupAction.RetryFallback,
                    operation.Id,
                    secondaryMessage: $"백업 번역을 가져오지 못했습니다.\r\n{fallback.ErrorMessage ?? "잠시 후 다시 시도해 주세요."}",
                    primaryModel: operation.PrimaryModel,
                    secondaryModel: operation.FallbackModel,
                    title: "번역 비교");
            }
            else
            {
                ShowPopup(
                    true,
                    TranslationPopupKind.Error,
                    fallback.ErrorMessage ?? "백업 모델 번역에 실패했습니다.",
                    action: TranslationPopupAction.RetryFallback,
                    operationId: operation.Id,
                    primaryModel: operation.FallbackModel,
                    title: "백업 모델 응답 오류");
            }
            return;
        }

        if (useAsPrimary || string.IsNullOrEmpty(operation.PrimaryText))
        {
            logger.Information($"백업 모델로 읽기 번역을 완료했습니다. InputCharacters={operation.Request.OriginalText.Length}, OutputCharacters={fallback.TranslatedText!.Length}");
            AddHistory(
                TranslationHistoryKind.Read,
                operation.Request.TargetLanguage,
                operation.Request.OriginalText,
                fallback.TranslatedText!);
            ShowPopup(
                true,
                TranslationPopupKind.Success,
                fallback.TranslatedText!,
                fallback.TranslatedText,
                operationId: operation.Id,
                primaryModel: operation.FallbackModel,
                title: "백업 모델 번역 완료");
            return;
        }

        ShowPopup(
            true,
            TranslationPopupKind.Success,
            operation.PrimaryText,
            operation.PrimaryText,
            operationId: operation.Id,
            secondaryMessage: fallback.TranslatedText,
            primaryModel: operation.PrimaryModel,
            secondaryModel: operation.FallbackModel,
            title: "번역 비교");
    }

    private bool HasFallbackModel() =>
        Settings.Api.EnableFallbackModel &&
        !string.IsNullOrWhiteSpace(Settings.Api.FallbackModel) &&
        !string.Equals(Settings.Api.Model, Settings.Api.FallbackModel, StringComparison.OrdinalIgnoreCase) &&
        !GeminiModelPolicy.IsKnownRetired(Settings.Api.FallbackModel);

    private bool IsCurrentReadOperation(ReadTranslationOperation operation) =>
        ReferenceEquals(activeReadOperation, operation);

    private void InvalidateActiveReadOperation()
    {
        ReadTranslationOperation? previous;
        lock (readOperationSync)
        {
            previous = activeReadOperation;
            activeReadOperation = null;
            if (previous is not null)
            {
                previous.Dismissed = true;
            }
        }

        previous?.PrimaryCancellation.Cancel();
        previous?.FallbackCancellation?.Cancel();
        previous?.PrimaryCancellation.Dispose();
    }

    private async Task ExecuteReplaceTranslationAsync(
        string originalText,
        WindowIdentity sourceWindow,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        if (Settings.General.ShowTranslatingPopup)
        {
            ShowPopup(false, TranslationPopupKind.Translating, "번역 중...");
        }
        TranslationResult translation = await TranslationProvider.TranslateAsync(
            new TranslationRequest(originalText, targetLanguage),
            cancellationToken);

        if (!translation.IsSuccess)
        {
            logger.Warning($"작성 번역에 실패했습니다. Kind={translation.FailureKind}, Characters={originalText.Length}");
            ShowPopup(false, TranslationPopupKind.Error, translation.ErrorMessage ?? "번역에 실패했습니다.");
            return;
        }

        string replacement = ReplaceOutputFormatter.Format(
            originalText,
            translation.TranslatedText!,
            ReplaceOutputFormatter.Parse(Settings.Translation.ReplaceFormat));
        AddHistory(
            TranslationHistoryKind.Replace,
            targetLanguage,
            originalText,
            translation.TranslatedText!);

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
                    false,
                    TranslationPopupKind.Warning,
                    $"클립보드가 변경되어 자동 치환을 취소했습니다.\r\n번역 결과를 확인한 후 복사 버튼을 눌러 주세요.\r\n\r\n{replacement}",
                    replacement);
                return;
            }

            logger.Information($"작성 번역을 완료했습니다. InputCharacters={originalText.Length}, OutputCharacters={replacement.Length}");
            ShowPopup(false, TranslationPopupKind.Success, "선택 영역을 번역문으로 교체했습니다.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.Error("작성 번역 결과를 붙여넣지 못했습니다.", ex);
            ShowPopup(
                false,
                TranslationPopupKind.Warning,
                $"자동 입력에 실패했습니다. 번역 결과를 확인한 후 복사 버튼을 눌러 주세요.\r\n\r\n{replacement}",
                replacement);
        }
    }

    private void ShowChangedTargetWarning(string replacement)
    {
        logger.Warning("활성 창 또는 포커스가 변경되어 작성 번역 자동 치환을 취소했습니다.");
        ShowPopup(
            false,
            TranslationPopupKind.Warning,
            $"활성 창이 변경되어 자동 치환을 취소했습니다.\r\n번역 결과를 확인한 후 복사 버튼을 눌러 주세요.\r\n\r\n{replacement}",
            replacement);
    }

    private async Task<string?> ResolveTargetLanguageAsync(
        string configuredTargetLanguage,
        bool isReadTranslation,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(configuredTargetLanguage, "Select", StringComparison.OrdinalIgnoreCase))
        {
            return configuredTargetLanguage;
        }

        string recentLanguage = isReadTranslation
            ? Settings.Translation.ReadRecentTargetLanguage
            : Settings.Translation.ReplaceRecentTargetLanguage;
        LanguageSelectionRequestedEventArgs request = new(isReadTranslation, recentLanguage);
        LanguageSelectionRequested?.Invoke(this, request);
        if (!request.WasHandled)
        {
            ShowPopup(isReadTranslation, TranslationPopupKind.Error, "언어 선택 창을 표시하지 못했습니다.");
            return null;
        }

        string? selected = await request.Selection.WaitAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            if (isReadTranslation)
            {
                Settings.Translation.ReadRecentTargetLanguage = selected;
            }
            else
            {
                Settings.Translation.ReplaceRecentTargetLanguage = selected;
            }

            configuration.Save(Settings);
        }

        return selected;
    }

    private void TryRollbackSettings(
        AppSettings previous,
        bool previousStartup,
        string? previousApiKey,
        bool apiKeyChanged)
    {
        try
        {
            hotkeys.Register(previous.Translation.ReadHotkey, previous.Translation.ReplaceHotkey);
            startupRegistration.SetEnabled(previousStartup);
            if (apiKeyChanged)
            {
                if (string.IsNullOrEmpty(previousApiKey))
                {
                    secretStore.DeleteSecret(SecretNames.GeminiApiKey);
                }
                else
                {
                    secretStore.WriteSecret(SecretNames.GeminiApiKey, previousApiKey);
                }
            }
        }
        catch (Exception rollbackError)
        {
            logger.Error("설정 변경 실패 후 기존 상태를 복구하지 못했습니다.", rollbackError);
        }
    }

    private static AppSettings CloneSettings(AppSettings source)
    {
        AppSettings clone = new();
        CopySettings(source, clone);
        return clone;
    }

    private static void CopySettings(AppSettings source, AppSettings target)
    {
        target.General.TranslationEnabled = source.General.TranslationEnabled;
        target.General.RunAtWindowsStartup = source.General.RunAtWindowsStartup;
        target.General.ShowTranslatingPopup = source.General.ShowTranslatingPopup;
        target.General.PopupTimeoutMs = source.General.PopupTimeoutMs;
        target.General.ReadPopupTimeoutMs = source.General.ReadPopupTimeoutMs;
        target.General.ReplacePopupTimeoutMs = source.General.ReplacePopupTimeoutMs;
        target.General.EnableLogging = source.General.EnableLogging;
        target.General.LogRetentionDays = source.General.LogRetentionDays;

        target.Translation.ReadTargetLanguage = source.Translation.ReadTargetLanguage;
        target.Translation.ReplaceTargetLanguage = source.Translation.ReplaceTargetLanguage;
        target.Translation.ReplaceFormat = source.Translation.ReplaceFormat;
        target.Translation.ReadHotkey = source.Translation.ReadHotkey;
        target.Translation.ReplaceHotkey = source.Translation.ReplaceHotkey;
        target.Translation.MaxInputCharacters = source.Translation.MaxInputCharacters;
        target.Translation.ClipboardCopyTimeoutMs = source.Translation.ClipboardCopyTimeoutMs;
        target.Translation.ClipboardRestoreDelayMs = source.Translation.ClipboardRestoreDelayMs;
        target.Translation.ClipboardRetryCount = source.Translation.ClipboardRetryCount;
        target.Translation.ClipboardRetryDelayMs = source.Translation.ClipboardRetryDelayMs;
        target.Translation.PreferUiAutomation = source.Translation.PreferUiAutomation;
        target.Translation.ReadRecentTargetLanguage = source.Translation.ReadRecentTargetLanguage;
        target.Translation.ReplaceRecentTargetLanguage = source.Translation.ReplaceRecentTargetLanguage;
        target.Translation.TranslationStyle = source.Translation.TranslationStyle;
        target.Translation.Politeness = source.Translation.Politeness;
        target.Translation.CustomInstructions = source.Translation.CustomInstructions;
        target.Translation.Glossary = source.Translation.Glossary;
        target.Translation.ExcludedTerms = source.Translation.ExcludedTerms;

        target.Api.Provider = source.Api.Provider;
        target.Api.Model = source.Api.Model;
        target.Api.RequestTimeoutSeconds = source.Api.RequestTimeoutSeconds;
        target.Api.TransientRetryCount = source.Api.TransientRetryCount;
        target.Api.EnableFallbackModel = source.Api.EnableFallbackModel;
        target.Api.FallbackModel = source.Api.FallbackModel;
    }

    private void Notify(string message, ToolTipIcon icon) =>
        NotificationRequested?.Invoke(this, new UserNotificationEventArgs(message, icon));

    private void AddHistory(
        TranslationHistoryKind kind,
        string targetLanguage,
        string originalText,
        string translatedText)
    {
        historyStore.Append(new TranslationHistoryEntry(
            DateTimeOffset.Now,
            kind,
            targetLanguage,
            originalText,
            translatedText));
        if (Settings.General.EnableLogging)
        {
            TranslationHistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ShowPopup(
        bool isReadTranslation,
        TranslationPopupKind kind,
        string message,
        string? copyText = null,
        TranslationPopupAction action = TranslationPopupAction.None,
        long operationId = 0,
        string? secondaryMessage = null,
        string? primaryModel = null,
        string? secondaryModel = null,
        string? title = null) =>
        TranslationPopupRequested?.Invoke(this, new TranslationPopupRequestedEventArgs(
            kind,
            message,
            copyText,
            isReadTranslation
                ? Settings.General.ReadPopupTimeoutMs
                : Settings.General.ReplacePopupTimeoutMs,
            action,
            operationId,
            secondaryMessage,
            primaryModel,
            secondaryModel,
            title));

    public void Dispose()
    {
        shutdown.Cancel();
        InvalidateActiveReadOperation();
        hotkeys.Dispose();
        if (TranslationProvider is IDisposable disposableTranslationProvider)
        {
            disposableTranslationProvider.Dispose();
        }
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

public enum TranslationPopupAction
{
    None,
    SwitchToFallback,
    ShowFallback,
    FallbackInProgress,
    RetryFallback
}

public sealed class TranslationPopupRequestedEventArgs(
    TranslationPopupKind kind,
    string message,
    string? copyText,
    int timeoutMs,
    TranslationPopupAction action = TranslationPopupAction.None,
    long operationId = 0,
    string? secondaryMessage = null,
    string? primaryModel = null,
    string? secondaryModel = null,
    string? title = null) : EventArgs
{
    public TranslationPopupKind Kind { get; } = kind;
    public string Message { get; } = message;
    public string? CopyText { get; } = copyText;
    public int TimeoutMs { get; } = timeoutMs;
    public TranslationPopupAction Action { get; } = action;
    public long OperationId { get; } = operationId;
    public string? SecondaryMessage { get; } = secondaryMessage;
    public string? PrimaryModel { get; } = primaryModel;
    public string? SecondaryModel { get; } = secondaryModel;
    public string? Title { get; } = title;
}

internal sealed class ReadTranslationOperation(
    long id,
    TranslationRequest request,
    string primaryModel,
    string fallbackModel,
    CancellationTokenSource primaryCancellation)
{
    public long Id { get; } = id;
    public TranslationRequest Request { get; } = request;
    public string PrimaryModel { get; } = primaryModel;
    public string FallbackModel { get; } = fallbackModel;
    public CancellationTokenSource PrimaryCancellation { get; } = primaryCancellation;
    public CancellationTokenSource? FallbackCancellation { get; set; }
    public bool PrimaryInProgress { get; set; } = true;
    public bool SwitchToFallbackRequested { get; set; }
    public bool FallbackInProgress { get; set; }
    public bool Dismissed { get; set; }
    public string? PrimaryText { get; set; }
    public string? FallbackText { get; set; }
}

public sealed class LanguageSelectionRequestedEventArgs(bool isReadTranslation = true, string recentLanguage = "Korean") : EventArgs
{
    private readonly TaskCompletionSource<string?> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<string?> Selection => completion.Task;
    public bool WasHandled { get; private set; }
    public bool IsReadTranslation { get; } = isReadTranslation;
    public string RecentLanguage { get; } = recentLanguage;

    public void MarkHandled() => WasHandled = true;
    public void Complete(string? language) => completion.TrySetResult(language);
}
