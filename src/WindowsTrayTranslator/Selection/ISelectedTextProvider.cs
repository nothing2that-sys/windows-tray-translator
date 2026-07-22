using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Selection;

public interface ISelectedTextProvider
{
    Task<SelectedTextResult> GetSelectedTextAsync(
        WindowIdentity sourceWindow,
        int timeoutMs,
        int maxCharacters,
        CancellationToken cancellationToken);
}
