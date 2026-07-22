using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Security;

namespace WindowsTrayTranslator.Translation.Gemini;

public sealed class GeminiTranslationProvider : ITranslationProvider
{
    private readonly GeminiApiClient client;
    private readonly ISecretStore secretStore;
    private readonly Func<AppSettings> settingsAccessor;

    public GeminiTranslationProvider(
        GeminiApiClient client,
        ISecretStore secretStore,
        Func<AppSettings> settingsAccessor)
    {
        this.client = client;
        this.secretStore = secretStore;
        this.settingsAccessor = settingsAccessor;
    }

    public async Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        string? apiKey = secretStore.ReadSecret(SecretNames.GeminiApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return TranslationResult.Failure(TranslationFailureKind.MissingApiKey, "Gemini API 키를 설정해 주세요.");
        }

        AppSettings settings = settingsAccessor();
        return await client.TranslateAsync(
            apiKey,
            settings.Api.Model,
            request,
            settings.Api.RequestTimeoutSeconds,
            settings.Api.TransientRetryCount,
            cancellationToken);
    }
}
