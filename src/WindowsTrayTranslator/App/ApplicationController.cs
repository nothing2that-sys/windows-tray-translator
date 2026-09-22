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
using System.Text.Json;

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
    private readonly ClipboardMutationCoordinator clipboardMutations = new();
    private readonly CancellationTokenSource shutdown = new();
    private readonly object translationInteractionSync = new();
    private readonly object paletteSync = new();
    private QuickActionSession? activePaletteSession;
    private long paletteOperationId;
    private CancellationTokenSource? paletteCancellation;
    private TranslationInteraction? activeTranslationInteraction;
    private long nextTranslationInteractionId;

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
        SensitiveInputProbe sensitiveProbe = new(logger);
        ISelectedTextProvider clipboardProvider = new ClipboardSelectedTextProvider(clipboard, clipboardMutations, sensitiveProbe, keyboard, activeWindow, logger);
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
            paths.HistoryDirectory,
            settings.General.EnableTranslationHistory,
            settings.General.HistoryRetentionDays,
            logger);
        hotkeys = new GlobalHotkeyService(OnHotkeyPressed, logger);
        IReadOnlyList<string> hotkeyFailures = hotkeys.RegisterStartup(
            settings.Translation.ReadHotkey,
            settings.Translation.ReplaceHotkey,
            settings.Translation.ActionPaletteHotkey);
        StartupWarning = string.Join(
            Environment.NewLine,
            new[] { configuration.LastLoadWarning }
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Concat(hotkeyFailures)
                .Select(message => message!));
        if (string.IsNullOrWhiteSpace(StartupWarning))
        {
            StartupWarning = null;
        }
    }

    public event EventHandler<UserNotificationEventArgs>? NotificationRequested;
    public event EventHandler<TranslationPopupRequestedEventArgs>? TranslationPopupRequested;
    public event EventHandler<LanguageSelectionRequestedEventArgs>? LanguageSelectionRequested;
    public event EventHandler? SettingsApplied;
    public event EventHandler? TranslationHistoryChanged;
    internal event EventHandler<QuickActionSession>? ActionPaletteRequested;

    public AppPaths Paths { get; }
    public AppSettings Settings { get; }
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
        candidate = CloneSettings(candidate);
        SettingsValidator.Normalize(candidate);
        GeminiModelPolicy.EnsureUsable(candidate.Api.Model);
        if (candidate.Api.EnableActionModel)
        {
            GeminiModelPolicy.EnsureUsable(candidate.Api.ActionModel);
        }
        if (candidate.Api.EnableFallbackModel)
        {
            GeminiModelPolicy.EnsureUsable(candidate.Api.FallbackModel);
            if (string.Equals(candidate.Api.Model, candidate.Api.FallbackModel, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("기본 모델과 보조 모델은 서로 달라야 합니다.");
            }
        }
        AppSettings previous = CloneSettings(Settings);
        bool previousStartup = startupRegistration.IsEnabled();
        string? previousApiKey = secretStore.ReadSecret(SecretNames.GeminiApiKey);
        bool apiKeyChanged = !string.IsNullOrWhiteSpace(newApiKey);

        List<SettingsApplyStep> steps = [];
        if (apiKeyChanged)
        {
            steps.Add(new SettingsApplyStep(
                "API 키",
                () => secretStore.WriteSecret(SecretNames.GeminiApiKey, newApiKey!),
                () => RestoreApiKey(previousApiKey)));
        }
        steps.Add(new SettingsApplyStep(
            "전역 단축키",
            () => hotkeys.Register(
                candidate.Translation.ReadHotkey,
                candidate.Translation.ReplaceHotkey,
                candidate.Translation.ActionPaletteHotkey),
            () => hotkeys.Register(
                previous.Translation.ReadHotkey,
                previous.Translation.ReplaceHotkey,
                previous.Translation.ActionPaletteHotkey)));
        steps.Add(new SettingsApplyStep(
            "시작 프로그램 설정",
            () => startupRegistration.SetEnabled(candidate.General.RunAtWindowsStartup),
            () => startupRegistration.SetEnabled(previousStartup)));
        steps.Add(new SettingsApplyStep(
            "설정 파일",
            () => configuration.Save(candidate),
            () => configuration.Save(previous)));
        steps.Add(new SettingsApplyStep(
            "클립보드 설정",
            () => clipboard.Configure(
                candidate.Translation.ClipboardRetryCount,
                candidate.Translation.ClipboardRetryDelayMs),
            () => clipboard.Configure(
                previous.Translation.ClipboardRetryCount,
                previous.Translation.ClipboardRetryDelayMs)));
        if (logger is FileLogger fileLogger)
        {
            steps.Add(new SettingsApplyStep(
                "로그 설정",
                () => fileLogger.Configure(candidate.General.EnableLogging, candidate.General.LogRetentionDays),
                () => fileLogger.Configure(previous.General.EnableLogging, previous.General.LogRetentionDays)));
        }
        steps.Add(new SettingsApplyStep(
            "히스토리 설정",
            () => historyStore.Configure(
                candidate.General.EnableTranslationHistory,
                candidate.General.HistoryRetentionDays),
            () => historyStore.Configure(
                previous.General.EnableTranslationHistory,
                previous.General.HistoryRetentionDays)));
        steps.Add(new SettingsApplyStep(
            "설정 적용 로그",
            () => logger.Information("설정을 저장하고 즉시 적용했습니다."),
            () => { }));
        steps.Add(new SettingsApplyStep(
            "메모리 설정",
            () => CopySettings(candidate, Settings),
            () => CopySettings(previous, Settings)));

        SettingsApplyTransaction.Execute(
            steps,
            (name, rollbackError) =>
            {
                logger.Error($"설정 변경 실패 후 {name}을(를) 복구하지 못했습니다.", rollbackError);
            });

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
        if (!HotkeyCommandMap.TryResolve(hotkeyId, out HotkeyCommand command))
        {
            logger.Warning($"알 수 없는 전역 단축키 ID를 무시했습니다. Id={hotkeyId}");
            return;
        }

        if (!Settings.General.TranslationEnabled)
        {
            return;
        }

        // Any new hotkey cancels and closes an open palette before its own command runs.
        DismissActionPalette(null);

        if (command == HotkeyCommand.ShowActionPalette)
        {
            await ShowActionPaletteAsync();
            return;
        }

        if (!await operationGate.WaitAsync(0))
        {
            Notify("현재 번역 작업이 진행 중입니다.", ToolTipIcon.Warning);
            return;
        }

        try
        {
            bool isReadTranslation = command == HotkeyCommand.ReadTranslation;
            InvalidateActiveTranslationInteraction();
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

            if (!result.ClipboardRestored)
            {
                Notify("선택 문자열은 가져왔지만 기존 클립보드를 복원하지 못했습니다.", ToolTipIcon.Warning);
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
            if (command == HotkeyCommand.ReadTranslation)
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

    /// <summary>
    /// Captures the selection while holding the operation gate, then releases the gate so the
    /// palette never blocks Alt+R/Alt+T while it is on screen.
    /// </summary>
    private async Task ShowActionPaletteAsync()
    {
        if (!await operationGate.WaitAsync(0))
        {
            Notify("현재 번역 작업이 진행 중입니다.", ToolTipIcon.Warning);
            return;
        }

        try
        {
            WindowIdentity sourceWindow = activeWindow.Capture();
            InputAccessResult access = inputAccess.Check(sourceWindow);
            if (!access.IsAllowed)
            {
                Notify(access.ErrorMessage!, ToolTipIcon.Warning);
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
                Notify(result.ErrorMessage ?? "선택된 문자열을 가져오지 못했습니다.", ToolTipIcon.Warning);
                return;
            }

            if (!result.ClipboardRestored)
            {
                Notify("선택 문자열은 가져왔지만 기존 클립보드를 복원하지 못했습니다.", ToolTipIcon.Warning);
            }

            QuickActionSession session = new(new CapturedSelection(result.Text!, result.SourceWindow));
            lock (paletteSync)
            {
                activePaletteSession = session;
            }

            ActionPaletteRequested?.Invoke(this, session);
        }
        catch (OperationCanceledException)
        {
            logger.Information("Quick Action 선택 문자열 캡처가 취소되었습니다.");
        }
        catch (Exception ex)
        {
            logger.Error("Quick Action 준비 중 오류가 발생했습니다.", ex);
            Notify("Quick Action을 시작하지 못했습니다.", ToolTipIcon.Error);
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <summary>
    /// Runs entirely on the UI thread with no await, so admission, the source-window recheck and the
    /// session claim cannot be interleaved with the palette timeout.
    /// </summary>
    internal PaletteAdmission TryStartPaletteAction(QuickActionSession session, BuiltInActionKind kind)
    {
        bool isCurrent;
        lock (paletteSync)
        {
            isCurrent = ReferenceEquals(activePaletteSession, session);
        }

        PaletteAdmission admission = DecidePaletteAdmission(
            session,
            isCurrent,
            () => operationGate.Wait(0),
            () => operationGate.Release(),
            () => activeWindow.IsStillActive(session.Selection.SourceWindow));

        if (admission == PaletteAdmission.TargetChanged)
        {
            lock (paletteSync)
            {
                activePaletteSession = null;
            }

            Notify("선택한 창이 바뀌어 실행하지 않았습니다. 다시 선택해 주세요.", ToolTipIcon.Warning);
            return admission;
        }

        if (admission != PaletteAdmission.Started)
        {
            return admission;
        }

        long operationId = Interlocked.Increment(ref nextTranslationInteractionId);
        CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
        string text = session.Selection.Text;
        lock (paletteSync)
        {
            paletteOperationId = operationId;
            paletteCancellation = cancellation;
            activePaletteSession = null;
        }

        session.TryComplete();
        _ = RunPaletteActionAsync(kind, text, operationId, cancellation);
        return PaletteAdmission.Started;
    }

    /// <summary>
    /// The whole body is inside try/finally and the finally is the single place that releases the
    /// operation gate, so no synchronous or asynchronous failure can strand it.
    /// </summary>
    private Task RunPaletteActionAsync(
        BuiltInActionKind kind,
        string selectedText,
        long operationId,
        CancellationTokenSource cancellation) =>
        RunPaletteGenerationAsync(
            token =>
            {
                if (Settings.General.ShowTranslatingPopup)
                {
                    ShowPopup(
                        true,
                        TranslationPopupKind.Translating,
                        "결과를 만들고 있습니다…",
                        operationId: operationId,
                        title: PromptComposer.DisplayName(kind));
                }

                return GenerateActionResultAsync(kind, selectedText, token);
            },
            cancellation,
            () => IsCurrentPaletteOperation(operationId),
            result => PublishPaletteResult(kind, selectedText, operationId, result),
            ex =>
            {
                logger.Error("Quick Action 실행 중 오류가 발생했습니다.", ex);
                Notify("Quick Action 실행 중 오류가 발생했습니다.", ToolTipIcon.Error);
            },
            () => CompletePaletteOperation(operationId, cancellation),
            logger);

    /// <summary>
    /// A translated summary is two steps, not one instruction: the selection is translated through
    /// the normal translation path first, and only the translation is summarized. Summarizing and
    /// translating in a single call is what a small model collapses into a keyword list.
    /// The translation failure is returned as-is, so the user sees why it stopped.
    /// </summary>
    private async Task<TranslationResult> GenerateActionResultAsync(
        BuiltInActionKind kind,
        string selectedText,
        CancellationToken cancellationToken)
    {
        if (kind != BuiltInActionKind.SummarizeTranslated)
        {
            return await directTranslationProvider.GenerateAsync(
                PromptComposer.Compose(kind, selectedText),
                cancellationToken);
        }

        string targetLanguage = ResolveSummaryTargetLanguage(Settings, selectedText);
        string summarySource = selectedText;
        if (ShouldTranslateBeforeSummary(targetLanguage, selectedText))
        {
            TranslationResult translated = await directTranslationProvider.TranslateAsync(
                new TranslationRequest(selectedText, targetLanguage),
                cancellationToken);
            if (!translated.IsSuccess)
            {
                return translated;
            }

            summarySource = translated.TranslatedText!;
        }

        return await directTranslationProvider.GenerateAsync(
            PromptComposer.Compose(kind, summarySource, targetLanguage),
            cancellationToken);
    }

    /// <summary>
    /// The summary language follows the read translation setting. "Select" is not carried over: the
    /// palette is a mouse-only menu that already took a click, so a second chooser on top of the
    /// progress popup is avoided and the language last chosen for reading is used instead. "Auto"
    /// is resolved from the text exactly as reading resolves it.
    /// </summary>
    internal static string ResolveSummaryTargetLanguage(AppSettings settings, string selectedText)
    {
        string configured = settings.Translation.ReadTargetLanguage?.Trim() ?? string.Empty;
        if (IsSelectPlaceholder(configured))
        {
            configured = settings.Translation.ReadRecentTargetLanguage?.Trim() ?? string.Empty;
        }

        if (IsSelectPlaceholder(configured) || string.IsNullOrEmpty(configured))
        {
            configured = "Korean";
        }

        return AutomaticTargetLanguageResolver.Resolve(configured, selectedText);
    }

    private static bool IsSelectPlaceholder(string value) =>
        string.Equals(value, "Select", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Skips a Korean-to-Korean round trip, which costs a call and can only degrade the text. Any
    /// other pair is translated: only Korean can be detected here, so an English selection with an
    /// English target is still sent through translation rather than guessed at.
    /// </summary>
    internal static bool ShouldTranslateBeforeSummary(string targetLanguage, string selectedText) =>
        !(string.Equals(targetLanguage.Trim(), "Korean", StringComparison.OrdinalIgnoreCase) &&
            AutomaticTargetLanguageResolver.IsKoreanDominant(selectedText));

    /// <summary>
    /// Owns the generation lifecycle. Every exit path - success, failure, staleness, cancellation
    /// and any synchronous or asynchronous exception - runs <paramref name="complete"/> exactly
    /// once, and a stale or cancelled operation publishes nothing.
    /// </summary>
    internal static async Task RunPaletteGenerationAsync(
        Func<CancellationToken, Task<TranslationResult>> generate,
        CancellationTokenSource cancellation,
        Func<bool> isCurrentOperation,
        Action<TranslationResult> publish,
        Action<Exception> reportUnexpected,
        Action complete,
        ILogger logger)
    {
        try
        {
            TranslationResult result = await generate(cancellation.Token);
            if (!isCurrentOperation() || cancellation.IsCancellationRequested)
            {
                return;
            }

            if (!result.IsSuccess && result.FailureKind == TranslationFailureKind.Cancelled)
            {
                return;
            }

            publish(result);
        }
        catch (OperationCanceledException)
        {
            logger.Information("Quick Action 실행이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            reportUnexpected(ex);
        }
        finally
        {
            complete();
        }
    }

    private void PublishPaletteResult(
        BuiltInActionKind kind,
        string selectedText,
        long operationId,
        TranslationResult result)
    {
        (TranslationPopupKind popupKind, string message, string? copyText) =
            ResolveActionPopup(result, Settings);
        if (result.IsSuccess)
        {
            logger.Information($"Quick Action을 완료했습니다. Action={kind}, InputCharacters={selectedText.Length}, OutputCharacters={result.TranslatedText!.Length}, Kind={popupKind}");
        }
        else
        {
            logger.Warning($"Quick Action 실행에 실패했습니다. Action={kind}, Kind={result.FailureKind}, InputCharacters={selectedText.Length}");
        }

        ShowPopup(
            true,
            popupKind,
            message,
            copyText: copyText,
            operationId: operationId,
            title: PromptComposer.DisplayName(kind));
    }

    private void CompletePaletteOperation(long operationId, CancellationTokenSource cancellation)
    {
        lock (paletteSync)
        {
            if (paletteOperationId == operationId)
            {
                paletteCancellation = null;
            }

            cancellation.Dispose();
        }

        operationGate.Release();
    }

    /// <summary>
    /// The admission order is the safety contract: a stale session never runs, a busy gate never
    /// discards the captured text, and a changed target releases the gate it just took.
    /// </summary>
    internal static PaletteAdmission DecidePaletteAdmission(
        QuickActionSession session,
        bool isCurrentSession,
        Func<bool> tryAcquireGate,
        Action releaseGate,
        Func<bool> isTargetStillActive)
    {
        if (!isCurrentSession || session.Completed)
        {
            return PaletteAdmission.Stale;
        }

        if (!tryAcquireGate())
        {
            return PaletteAdmission.Busy;
        }

        if (!isTargetStillActive())
        {
            releaseGate();
            session.TryComplete();
            return PaletteAdmission.TargetChanged;
        }

        return PaletteAdmission.Started;
    }

    /// <summary>
    /// An unusually long result is shown with a warning and stays copyable. It is never turned into
    /// a failure because a palette action never replaces the selection.
    /// </summary>
    internal static (TranslationPopupKind Kind, string Message, string? CopyText) ResolveActionPopup(
        TranslationResult result,
        AppSettings settings)
    {
        if (!result.IsSuccess)
        {
            return (TranslationPopupKind.Error, result.ErrorMessage ?? "결과를 만들지 못했습니다.", null);
        }

        string output = result.TranslatedText!;
        return output.Length > settings.Translation.MaxInputCharacters
            ? (TranslationPopupKind.Warning,
                $"결과가 예상보다 깁니다. 내용을 확인한 후 복사해 주세요.\r\n\r\n{output}",
                output)
            : (TranslationPopupKind.Success, output, output);
    }

    internal void DismissActionPalette(QuickActionSession? session)
    {
        QuickActionSession? current;
        lock (paletteSync)
        {
            current = activePaletteSession;
            if (current is null || (session is not null && !ReferenceEquals(current, session)))
            {
                return;
            }

            activePaletteSession = null;
        }

        current.TryComplete();
    }

    private bool IsCurrentPaletteOperation(long operationId)
    {
        lock (paletteSync)
        {
            return paletteOperationId == operationId;
        }
    }

    private bool TryCancelPaletteOperation(long operationId)
    {
        lock (paletteSync)
        {
            if (operationId == 0 || paletteOperationId != operationId)
            {
                return false;
            }

            paletteCancellation?.Cancel();
            return true;
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
        TranslationInteraction operation = new(
            Interlocked.Increment(ref nextTranslationInteractionId),
            request,
            Settings.Api.Model,
            Settings.Api.FallbackModel,
            primaryCancellation,
            isReadTranslation: true);
        lock (translationInteractionSync)
        {
            activeTranslationInteraction = operation;
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
        lock (translationInteractionSync)
        {
            if (!IsCurrentTranslationInteraction(operation) || operation.Dismissed)
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
            ShouldOfferFallbackAfterCompletion() ? TranslationPopupAction.ShowFallback : TranslationPopupAction.None,
            operation.Id,
            primaryModel: operation.PrimaryModel);
    }

    public void RequestFallbackTranslation(long operationId)
    {
        TranslationInteraction? operation;
        bool cancelPrimary = false;
        bool useAsPrimary = false;
        lock (translationInteractionSync)
        {
            operation = activeTranslationInteraction;
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
                operation.IsReadTranslation,
                TranslationPopupKind.Translating,
                "기본 모델 요청을 중단하고 보조 모델로 재진행하고 있습니다…",
                action: TranslationPopupAction.FallbackInProgress,
                operationId: operation.Id,
                primaryModel: operation.FallbackModel,
                title: "보조 모델로 재진행 중");
            operation.PrimaryCancellation.Cancel();
            return;
        }

        if (!useAsPrimary)
        {
            ShowPopup(
                operation.IsReadTranslation,
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
                operation.IsReadTranslation,
                TranslationPopupKind.Translating,
                "보조 모델에서 다시 번역하고 있습니다…",
                action: TranslationPopupAction.FallbackInProgress,
                operationId: operation.Id,
                primaryModel: operation.FallbackModel,
                title: "보조 모델 번역 중");
        }

        _ = RunFallbackTranslationObservedAsync(operation, useAsPrimary);
    }

    private async Task RunFallbackTranslationObservedAsync(TranslationInteraction operation, bool useAsPrimary)
    {
        try
        {
            await ExecuteFallbackTranslationAsync(operation, useAsPrimary);
        }
        catch (OperationCanceledException)
        {
            logger.Information("보조 모델 번역이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            logger.Error("보조 모델 번역 중 예기치 않은 오류가 발생했습니다.", ex);
            if (IsCurrentTranslationInteraction(operation) && !operation.Dismissed)
            {
                ShowPopup(
                    operation.IsReadTranslation,
                    TranslationPopupKind.Error,
                    "보조 모델 번역 중 오류가 발생했습니다.",
                    action: TranslationPopupAction.RetryFallback,
                    operationId: operation.Id);
            }
        }
        finally
        {
            lock (translationInteractionSync)
            {
                operation.FallbackInProgress = false;
                operation.FallbackCancellation = null;
            }
        }
    }

    public void CancelTranslationInteraction(long operationId)
    {
        // A palette generation owns no TranslationInteraction, so its popup must cancel it here.
        if (TryCancelPaletteOperation(operationId))
        {
            return;
        }

        TranslationInteraction? operation;
        lock (translationInteractionSync)
        {
            operation = activeTranslationInteraction;
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
        TranslationInteraction operation,
        bool useAsPrimary)
    {
        using CancellationTokenSource fallbackCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
        lock (translationInteractionSync)
        {
            if (!IsCurrentTranslationInteraction(operation) || operation.Dismissed)
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

        lock (translationInteractionSync)
        {
            if (!IsCurrentTranslationInteraction(operation) || operation.Dismissed)
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

            logger.Warning($"보조 모델 번역에 실패했습니다. Kind={fallback.FailureKind}");
            if (!useAsPrimary && !string.IsNullOrEmpty(operation.PrimaryText))
            {
                ShowPopup(
                    operation.IsReadTranslation,
                    TranslationPopupKind.Success,
                    operation.PrimaryText,
                    operation.PrimaryText,
                    TranslationPopupAction.RetryFallback,
                    operation.Id,
                    secondaryMessage: $"보조 모델 번역을 가져오지 못했습니다.\r\n{fallback.ErrorMessage ?? "잠시 후 다시 시도해 주세요."}",
                    primaryModel: operation.PrimaryModel,
                    secondaryModel: operation.FallbackModel,
                    title: "번역 비교");
            }
            else
            {
                ShowPopup(
                    operation.IsReadTranslation,
                    TranslationPopupKind.Error,
                    fallback.ErrorMessage ?? "보조 모델 번역에 실패했습니다.",
                    action: TranslationPopupAction.RetryFallback,
                    operationId: operation.Id,
                    primaryModel: operation.FallbackModel,
                    title: "보조 모델 응답 오류");
            }
            return;
        }

        if (useAsPrimary || string.IsNullOrEmpty(operation.PrimaryText))
        {
            if (!operation.IsReadTranslation)
            {
                await ApplyReplacementAsync(
                    operation,
                    fallback.TranslatedText!,
                    fallbackCancellation.Token,
                    offerFallbackAfterCompletion: false);
                return;
            }

            logger.Information($"보조 모델로 읽기 번역을 완료했습니다. InputCharacters={operation.Request.OriginalText.Length}, OutputCharacters={fallback.TranslatedText!.Length}");
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
                title: "보조 모델 번역 완료");
            return;
        }

        string fallbackDisplayText = operation.IsReadTranslation
            ? fallback.TranslatedText!
            : ReplaceOutputFormatter.Format(
                operation.Request.OriginalText,
                fallback.TranslatedText!,
                ReplaceOutputFormatter.Parse(Settings.Translation.ReplaceFormat));
        ShowPopup(
            operation.IsReadTranslation,
            TranslationPopupKind.Success,
            operation.PrimaryText,
            operation.PrimaryText,
            operationId: operation.Id,
            secondaryMessage: fallbackDisplayText,
            primaryModel: operation.PrimaryModel,
            secondaryModel: operation.FallbackModel,
            title: "번역 비교");
    }

    private bool HasFallbackModel() =>
        Settings.Api.EnableFallbackModel &&
        !string.IsNullOrWhiteSpace(Settings.Api.FallbackModel) &&
        !string.Equals(Settings.Api.Model, Settings.Api.FallbackModel, StringComparison.OrdinalIgnoreCase) &&
        !GeminiModelPolicy.IsKnownRetired(Settings.Api.FallbackModel);

    private bool ShouldOfferFallbackAfterCompletion() =>
        HasFallbackModel() && Settings.Api.OfferFallbackAfterCompletion;

    private bool IsCurrentTranslationInteraction(TranslationInteraction operation) =>
        ReferenceEquals(activeTranslationInteraction, operation);

    private void InvalidateActiveTranslationInteraction()
    {
        TranslationInteraction? previous;
        lock (translationInteractionSync)
        {
            previous = activeTranslationInteraction;
            activeTranslationInteraction = null;
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
        TranslationRequest request = new(originalText, targetLanguage);
        CancellationTokenSource primaryCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TranslationInteraction operation = new(
            Interlocked.Increment(ref nextTranslationInteractionId),
            request,
            Settings.Api.Model,
            Settings.Api.FallbackModel,
            primaryCancellation,
            isReadTranslation: false,
            sourceWindow);
        lock (translationInteractionSync)
        {
            activeTranslationInteraction = operation;
        }

        if (Settings.General.ShowTranslatingPopup)
        {
            ShowPopup(
                false,
                TranslationPopupKind.Translating,
                "선택한 문장을 번역하고 있습니다…",
                action: HasFallbackModel() ? TranslationPopupAction.SwitchToFallback : TranslationPopupAction.None,
                operationId: operation.Id);
        }
        TranslationResult translation = await directTranslationProvider.TranslateWithModelAsync(
            request,
            operation.PrimaryModel,
            primaryCancellation.Token);

        bool switchToFallback;
        lock (translationInteractionSync)
        {
            if (!IsCurrentTranslationInteraction(operation) || operation.Dismissed)
            {
                return;
            }

            operation.PrimaryInProgress = false;
            switchToFallback = operation.SwitchToFallbackRequested;
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

            logger.Warning($"작성 번역에 실패했습니다. Kind={translation.FailureKind}, Characters={originalText.Length}");
            ShowPopup(
                false,
                TranslationPopupKind.Error,
                translation.ErrorMessage ?? "번역에 실패했습니다.",
                action: HasFallbackModel() ? TranslationPopupAction.RetryFallback : TranslationPopupAction.None,
                operationId: operation.Id);
            return;
        }

        await ApplyReplacementAsync(
            operation,
            translation.TranslatedText!,
            primaryCancellation.Token,
            offerFallbackAfterCompletion: ShouldOfferFallbackAfterCompletion());
    }

    private async Task ApplyReplacementAsync(
        TranslationInteraction operation,
        string translatedText,
        CancellationToken cancellationToken,
        bool offerFallbackAfterCompletion)
    {
        string originalText = operation.Request.OriginalText;
        string replacement = ReplaceOutputFormatter.Format(
            originalText,
            translatedText,
            ReplaceOutputFormatter.Parse(Settings.Translation.ReplaceFormat));
        lock (translationInteractionSync)
        {
            if (!IsCurrentTranslationInteraction(operation) || operation.Dismissed)
            {
                return;
            }
            operation.PrimaryText = replacement;
        }
        AddHistory(
            TranslationHistoryKind.Replace,
            operation.Request.TargetLanguage,
            originalText,
            translatedText);

        if (operation.SourceWindow is not { } sourceWindow || !activeWindow.IsStillActive(sourceWindow))
        {
            ShowChangedTargetWarning(replacement);
            return;
        }

        try
        {
            PasteTextOutcome pasteResult = await clipboardMutations.RunAsync(async () =>
            {
                if (!activeWindow.IsStillActive(sourceWindow) || !IsCurrentTranslationInteraction(operation))
                {
                    return new PasteTextOutcome(PasteTextResult.TargetChanged);
                }

                using ClipboardSnapshot snapshot = clipboard.CaptureSnapshot();
                if (!activeWindow.IsStillActive(sourceWindow) || !IsCurrentTranslationInteraction(operation))
                {
                    return new PasteTextOutcome(PasteTextResult.TargetChanged);
                }

                return await clipboard.PasteTextWithOutcomeAsync(
                    replacement,
                    snapshot,
                    Settings.Translation.ClipboardRestoreDelayMs,
                    () => activeWindow.IsStillActive(sourceWindow) && IsCurrentTranslationInteraction(operation),
                    cancellationToken);
            }, cancellationToken);
            if (pasteResult.Status == PasteTextResult.TargetChanged)
            {
                ShowChangedTargetWarning(replacement);
                return;
            }
            if (pasteResult.Status == PasteTextResult.ClipboardChanged)
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
            string successMessage = pasteResult.ClipboardRestored
                ? "선택 영역을 번역문으로 교체했습니다."
                : "선택 영역을 번역문으로 교체했지만 기존 클립보드를 복원하지 못했습니다.";
            ShowPopup(
                false,
                pasteResult.ClipboardRestored ? TranslationPopupKind.Success : TranslationPopupKind.Warning,
                successMessage,
                action: offerFallbackAfterCompletion ? TranslationPopupAction.ShowFallback : TranslationPopupAction.None,
                operationId: operation.Id);
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

    private void RestoreApiKey(string? previousApiKey)
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

    internal static AppSettings CloneSettings(AppSettings source)
    {
        string json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    internal static void CopySettings(AppSettings source, AppSettings target)
    {
        AppSettings clone = CloneSettings(source);
        target.General = clone.General;
        target.Translation = clone.Translation;
        target.Api = clone.Api;
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
        if (ShouldRaiseTranslationHistoryChanged(Settings))
        {
            TranslationHistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal static bool ShouldRaiseTranslationHistoryChanged(AppSettings settings) =>
        settings.General.EnableTranslationHistory;

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
        DismissActionPalette(null);
        InvalidateActiveTranslationInteraction();
        hotkeys.Dispose();
        httpClient.Dispose();
        shutdown.Dispose();
        operationGate.Dispose();
        clipboardMutations.Dispose();
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

internal sealed class TranslationInteraction(
    long id,
    TranslationRequest request,
    string primaryModel,
    string fallbackModel,
    CancellationTokenSource primaryCancellation,
    bool isReadTranslation,
    WindowIdentity? sourceWindow = null)
{
    public long Id { get; } = id;
    public TranslationRequest Request { get; } = request;
    public string PrimaryModel { get; } = primaryModel;
    public string FallbackModel { get; } = fallbackModel;
    public CancellationTokenSource PrimaryCancellation { get; } = primaryCancellation;
    public bool IsReadTranslation { get; } = isReadTranslation;
    public WindowIdentity? SourceWindow { get; } = sourceWindow;
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
