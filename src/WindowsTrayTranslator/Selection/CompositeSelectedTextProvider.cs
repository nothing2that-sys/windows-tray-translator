using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Selection;

public sealed class CompositeSelectedTextProvider : ISelectedTextProvider
{
    private readonly ISelectedTextProvider primary;
    private readonly ISelectedTextProvider fallback;
    private readonly ILogger logger;

    public CompositeSelectedTextProvider(
        ISelectedTextProvider primary,
        ISelectedTextProvider fallback,
        ILogger logger)
    {
        this.primary = primary;
        this.fallback = fallback;
        this.logger = logger;
    }

    public async Task<SelectedTextResult> GetSelectedTextAsync(
        WindowIdentity sourceWindow,
        int timeoutMs,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        SelectedTextResult primaryResult = await primary.GetSelectedTextAsync(
            sourceWindow,
            timeoutMs,
            maxCharacters,
            cancellationToken);

        if (primaryResult.IsSuccess || !primaryResult.CanFallback)
        {
            return primaryResult;
        }

        logger.Information($"클립보드 캡처 실패 후 UI Automation 폴백을 시도합니다. Process={sourceWindow.ProcessName}");
        return await fallback.GetSelectedTextAsync(
            sourceWindow,
            timeoutMs,
            maxCharacters,
            cancellationToken);
    }
}
