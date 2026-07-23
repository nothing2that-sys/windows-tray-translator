using WindowsTrayTranslator.Clipboard;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Selection;

public sealed class ClipboardSelectedTextProvider : ISelectedTextProvider
{
    private readonly ClipboardService clipboard;
    private readonly KeyboardInputService keyboard;
    private readonly ActiveWindowService activeWindow;
    private readonly ILogger logger;

    public ClipboardSelectedTextProvider(
        ClipboardService clipboard,
        KeyboardInputService keyboard,
        ActiveWindowService activeWindow,
        ILogger logger)
    {
        this.clipboard = clipboard;
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
        ClipboardSnapshot? snapshot = null;
        try
        {
            snapshot = clipboard.CaptureSnapshot();
            clipboard.Clear();
            if (!await keyboard.WaitForModifiersReleasedAsync(1500, cancellationToken))
            {
                logger.Warning($"단축키 modifier가 해제되지 않아 캡처를 취소했습니다. Process={sourceWindow.ProcessName}");
                return SelectedTextResult.Failure("단축키를 놓은 후 다시 시도해 주세요.", sourceWindow, canFallback: false);
            }

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                if (!activeWindow.IsStillActive(sourceWindow))
                {
                    logger.Warning($"캡처 전에 활성 창 또는 포커스가 변경됐습니다. Process={sourceWindow.ProcessName}");
                    return SelectedTextResult.Failure("입력 위치가 변경되어 문자열 가져오기를 취소했습니다.", sourceWindow, canFallback: false);
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
                            return SelectedTextResult.Failure("선택된 문자열을 가져오지 못했습니다.", sourceWindow);
                        }

                        if (text.Length > maxCharacters)
                        {
                            return SelectedTextResult.Failure("번역 가능한 최대 문자 수를 초과했습니다.", sourceWindow, canFallback: false);
                        }

                        logger.Information($"선택 문자열을 캡처했습니다. Process={sourceWindow.ProcessName}, Characters={text.Length}, Attempt={attempt}");
                        return SelectedTextResult.Success(text, sourceWindow);
                    }

                    await Task.Delay(25, cancellationToken);
                    elapsed += 25;
                }

                logger.Warning($"선택 문자열 복사 응답이 없어 재시도합니다. Process={sourceWindow.ProcessName}, Attempt={attempt}, TimeoutMs={timeoutMs}");
                await Task.Delay(100, cancellationToken);
            }

            logger.Warning($"선택 문자열 캡처 시간이 초과됐습니다. Process={sourceWindow.ProcessName}, Attempts=2, TimeoutMs={timeoutMs}");
            return SelectedTextResult.Failure("선택된 문자열을 가져오지 못했습니다.", sourceWindow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Error("선택 문자열 캡처에 실패했습니다.", ex);
            return SelectedTextResult.Failure("선택된 문자열을 가져오지 못했습니다.", sourceWindow);
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
                    logger.Error("기존 클립보드를 복원하지 못했습니다.", ex);
                }
                finally
                {
                    snapshot.Dispose();
                }
            }
        }
    }
}
