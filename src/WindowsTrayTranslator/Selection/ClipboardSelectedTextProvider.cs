using WindowsTrayTranslator.Clipboard;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Selection;

public sealed class ClipboardSelectedTextProvider : ISelectedTextProvider
{
    private readonly IClipboardService clipboard;
    private readonly ClipboardMutationCoordinator clipboardMutations;
    private readonly SensitiveInputProbe sensitiveProbe;
    private readonly IKeyboardInputService keyboard;
    private readonly IActiveWindowService activeWindow;
    private readonly ILogger logger;

    public ClipboardSelectedTextProvider(
        ClipboardService clipboard,
        KeyboardInputService keyboard,
        ActiveWindowService activeWindow,
        ILogger logger)
        : this(clipboard, new ClipboardMutationCoordinator(), new SensitiveInputProbe(logger), keyboard, activeWindow, logger)
    {
    }

    internal ClipboardSelectedTextProvider(
        IClipboardService clipboard,
        ClipboardMutationCoordinator clipboardMutations,
        SensitiveInputProbe sensitiveProbe,
        IKeyboardInputService keyboard,
        IActiveWindowService activeWindow,
        ILogger logger)
    {
        this.clipboard = clipboard;
        this.clipboardMutations = clipboardMutations;
        this.sensitiveProbe = sensitiveProbe;
        this.keyboard = keyboard;
        this.activeWindow = activeWindow;
        this.logger = logger;
    }

    public async Task<SelectedTextResult> GetSelectedTextAsync(
        WindowIdentity sourceWindow,
        int timeoutMs,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        return await clipboardMutations.RunAsync(
            () => CaptureCoreAsync(sourceWindow, timeoutMs, maxCharacters, cancellationToken),
            cancellationToken);
    }

    private async Task<SelectedTextResult> CaptureCoreAsync(
        WindowIdentity sourceWindow,
        int timeoutMs,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        ClipboardSnapshot? snapshot = null;
        bool restoreOk = true;
        bool stopAttempts = false;
        SelectedTextResult result = SelectedTextResult.Failure("선택된 문자열을 가져오지 못했습니다.", sourceWindow);
        try
        {
            // Probe #1 runs before any clipboard access so a known password field never sees a
            // snapshot, a clear, or Ctrl+C.
            if (!await keyboard.WaitForModifiersReleasedAsync(1500, cancellationToken))
            {
                logger.Warning($"단축키 modifier가 해제되지 않아 캡처를 취소했습니다. Process={sourceWindow.ProcessName}");
                result = SelectedTextResult.Failure("단축키를 놓은 후 다시 시도해 주세요.", sourceWindow, canFallback: false);
            }
            else if (!activeWindow.IsStillActive(sourceWindow))
            {
                logger.Warning($"캡처 전에 활성 창 또는 포커스가 변경됐습니다. Process={sourceWindow.ProcessName}");
                result = SelectedTextResult.Failure("입력 위치가 변경되어 문자열 가져오기를 취소했습니다.", sourceWindow, canFallback: false);
            }
            else if (await sensitiveProbe.ProbeAsync(cancellationToken) == SensitiveProbeResult.Sensitive)
            {
                logger.Warning($"보호된 입력 영역이 감지되어 클립보드 접근 없이 캡처를 중단했습니다. Process={sourceWindow.ProcessName}");
                result = SelectedTextResult.Failure(SelectionMessages.SensitiveInput, sourceWindow, canFallback: false);
            }
            else
            {
                snapshot = clipboard.CaptureSnapshot();
                clipboard.Clear();
                for (int attempt = 1; attempt <= 2 && !result.IsSuccess && !stopAttempts; attempt++)
                {
                    if (!activeWindow.IsStillActive(sourceWindow))
                    {
                        logger.Warning($"캡처 전에 활성 창 또는 포커스가 변경됐습니다. Process={sourceWindow.ProcessName}");
                        result = SelectedTextResult.Failure("입력 위치가 변경되어 문자열 가져오기를 취소했습니다.", sourceWindow, canFallback: false);
                        break;
                    }

                    // Probe #2 runs immediately before every SendCopy so a focus change during the
                    // snapshot, the clear, or the previous attempt's polling still blocks Ctrl+C.
                    if (await sensitiveProbe.ProbeAsync(cancellationToken) == SensitiveProbeResult.Sensitive)
                    {
                        logger.Warning($"보호된 입력 영역이 감지되어 복사를 중단했습니다. Process={sourceWindow.ProcessName}, Attempt={attempt}");
                        result = SelectedTextResult.Failure(SelectionMessages.SensitiveInput, sourceWindow, canFallback: false);
                        break;
                    }

                    uint baselineSequence = clipboard.GetSequenceNumber();
                    keyboard.SendCopy();

                    int elapsed = 0;
                    while (elapsed < timeoutMs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (clipboard.GetSequenceNumber() != baselineSequence && clipboard.ContainsText())
                        {
                            string text = clipboard.GetText();
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                stopAttempts = true;
                                break;
                            }

                            if (text.Length > maxCharacters)
                            {
                                result = SelectedTextResult.Failure("번역 가능한 최대 문자 수를 초과했습니다.", sourceWindow, canFallback: false);
                                break;
                            }

                            logger.Information($"선택 문자열을 캡처했습니다. Process={sourceWindow.ProcessName}, Characters={text.Length}, Attempt={attempt}");
                            result = SelectedTextResult.Success(text, sourceWindow);
                            break;
                        }

                        await Task.Delay(25, cancellationToken);
                        elapsed += 25;
                    }

                    if (result.IsSuccess || !result.CanFallback || stopAttempts)
                    {
                        break;
                    }

                    logger.Warning($"선택 문자열 복사 응답이 없어 재시도합니다. Process={sourceWindow.ProcessName}, Attempt={attempt}, TimeoutMs={timeoutMs}");
                    if (attempt < 2)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }
            }

            if (!result.IsSuccess && result.CanFallback)
            {
                logger.Warning($"선택 문자열 캡처 시간이 초과됐습니다. Process={sourceWindow.ProcessName}, Attempts=2, TimeoutMs={timeoutMs}");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Error("선택 문자열 캡처에 실패했습니다.", ex);
            result = SelectedTextResult.Failure("선택된 문자열을 가져오지 못했습니다.", sourceWindow);
        }
        finally
        {
            if (snapshot is not null)
            {
                try
                {
                    clipboard.Restore(snapshot);
                }
                catch (Exception ex)
                {
                    restoreOk = false;
                    logger.Error("기존 클립보드를 복원하지 못했습니다.", ex);
                }
                finally
                {
                    snapshot.Dispose();
                }
            }
        }

        return result with { ClipboardRestored = restoreOk };
    }
}
