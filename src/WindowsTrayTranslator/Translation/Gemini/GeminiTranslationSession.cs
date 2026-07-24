using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Security;

namespace WindowsTrayTranslator.Translation.Gemini;

internal sealed class GeminiTranslationSession : ITranslationProvider, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly GeminiTranslationProvider provider;

    public GeminiTranslationSession(
        ISecretStore secretStore,
        Func<AppSettings> settingsAccessor,
        ILogger logger)
    {
        httpClient = new HttpClient();
        provider = new GeminiTranslationProvider(
            new GeminiApiClient(httpClient, logger),
            secretStore,
            settingsAccessor);
    }

    public Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken) =>
        provider.TranslateAsync(request, cancellationToken);

    public void Dispose() => httpClient.Dispose();
}
