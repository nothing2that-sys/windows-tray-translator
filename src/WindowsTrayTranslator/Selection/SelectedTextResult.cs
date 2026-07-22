using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Selection;

public sealed record SelectedTextResult(
    bool IsSuccess,
    string? Text,
    string? ErrorMessage,
    WindowIdentity SourceWindow)
{
    public static SelectedTextResult Success(string text, WindowIdentity source) =>
        new(true, text, null, source);

    public static SelectedTextResult Failure(string message, WindowIdentity source) =>
        new(false, null, message, source);
}
