using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Selection;

public sealed record SelectedTextResult(
    bool IsSuccess,
    string? Text,
    string? ErrorMessage,
    WindowIdentity SourceWindow,
    bool CanFallback,
    bool ClipboardRestored = true)
{
    public static SelectedTextResult Success(string text, WindowIdentity source) =>
        new(true, text, null, source, false, true);

    public static SelectedTextResult Failure(string message, WindowIdentity source, bool canFallback = true) =>
        new(false, null, message, source, canFallback, true);
}

internal static class SelectionMessages
{
    public const string SensitiveInput = "보호된 입력 영역에서는 사용할 수 없습니다.";
}
