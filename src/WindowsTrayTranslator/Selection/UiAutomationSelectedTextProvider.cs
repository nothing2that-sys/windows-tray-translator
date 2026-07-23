using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Selection;

public sealed class UiAutomationSelectedTextProvider : ISelectedTextProvider
{
    private readonly ActiveWindowService activeWindow;
    private readonly ILogger logger;

    public UiAutomationSelectedTextProvider(ActiveWindowService activeWindow, ILogger logger)
    {
        this.activeWindow = activeWindow;
        this.logger = logger;
    }

    public async Task<SelectedTextResult> GetSelectedTextAsync(
        WindowIdentity sourceWindow,
        int timeoutMs,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        if (!activeWindow.IsStillActive(sourceWindow))
        {
            return SelectedTextResult.Failure(
                "입력 위치가 변경되어 문자열 가져오기를 취소했습니다.",
                sourceWindow,
                canFallback: false);
        }

        try
        {
            Task<string?> readTask = Task.Run(ReadFocusedSelection, CancellationToken.None);
            string? text = await readTask.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                logger.Warning($"UI Automation에서 선택 문자열을 찾지 못했습니다. Process={sourceWindow.ProcessName}");
                return SelectedTextResult.Failure("선택된 문자열을 가져오지 못했습니다.", sourceWindow);
            }

            text = text.TrimEnd('\r', '\n');
            if (text.Length > maxCharacters)
            {
                return SelectedTextResult.Failure(
                    "번역 가능한 최대 문자 수를 초과했습니다.",
                    sourceWindow,
                    canFallback: false);
            }

            logger.Information($"UI Automation으로 선택 문자열을 캡처했습니다. Process={sourceWindow.ProcessName}, Characters={text.Length}");
            return SelectedTextResult.Success(text, sourceWindow);
        }
        catch (TimeoutException)
        {
            logger.Warning($"UI Automation 선택 문자열 조회 시간이 초과됐습니다. Process={sourceWindow.ProcessName}, TimeoutMs={timeoutMs}");
            return SelectedTextResult.Failure("선택된 문자열을 가져오지 못했습니다.", sourceWindow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            logger.Warning($"UI Automation 선택 문자열 조회를 지원하지 않습니다. Process={sourceWindow.ProcessName}, Error={ex.GetType().Name}");
            return SelectedTextResult.Failure("선택된 문자열을 가져오지 못했습니다.", sourceWindow);
        }
    }

    private static string? ReadFocusedSelection()
    {
        AutomationElement? focused = AutomationElement.FocusedElement;
        if (focused is null || (bool)focused.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true))
        {
            return null;
        }

        if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out object? patternObject) ||
            patternObject is not TextPattern textPattern)
        {
            return null;
        }

        TextPatternRange[] selections = textPattern.GetSelection();
        if (selections.Length == 0)
        {
            return null;
        }

        string text = string.Join(Environment.NewLine, selections.Select(range => range.GetText(-1)));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
