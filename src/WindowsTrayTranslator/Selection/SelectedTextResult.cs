using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Selection;

public sealed record SelectedTextResult(
    bool IsSuccess,
    string? Text,
    string? ErrorMessage,
    WindowIdentity SourceWindow,
    bool CanFallback)
{
    public static SelectedTextResult Success(string text, WindowIdentity source) =>
        new(true, text, null, source, false);

    public static SelectedTextResult Failure(string message, WindowIdentity source, bool canFallback = true) =>
        new(false, null, message, source, canFallback);
}
