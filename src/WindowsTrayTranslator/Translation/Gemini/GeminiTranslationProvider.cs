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
        AppSettings settings = settingsAccessor();
        TranslationResult result = await TranslateWithModelAsync(
            request,
            settings.Api.Model,
            cancellationToken);
        if (result.IsSuccess ||
            !settings.Api.EnableFallbackModel ||
            string.IsNullOrWhiteSpace(settings.Api.FallbackModel) ||
            string.Equals(settings.Api.Model, settings.Api.FallbackModel, StringComparison.OrdinalIgnoreCase) ||
            result.FailureKind is not (TranslationFailureKind.RateLimit or TranslationFailureKind.Server or TranslationFailureKind.ModelUnavailable))
        {
            return result;
        }

        return await TranslateWithModelAsync(
            request,
            settings.Api.FallbackModel,
            cancellationToken);
    }

    public Task<TranslationResult> TranslateWithModelAsync(
        TranslationRequest request,
        string model,
        CancellationToken cancellationToken)
    {
        string? apiKey = secretStore.ReadSecret(SecretNames.GeminiApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(TranslationResult.Failure(
                TranslationFailureKind.MissingApiKey,
                "Gemini API 키를 설정해 주세요."));
        }

        if (string.IsNullOrWhiteSpace(model) || GeminiModelPolicy.IsKnownRetired(model))
        {
            return Task.FromResult(TranslationResult.Failure(
                TranslationFailureKind.ModelUnavailable,
                "설정한 Gemini 모델을 사용할 수 없습니다. 설정에서 모델 목록을 새로고침해 주세요."));
        }

        AppSettings settings = settingsAccessor();
        TranslationRequest enrichedRequest = request with
        {
            Preferences = new TranslationPreferences(
                settings.Translation.TranslationStyle,
                settings.Translation.Politeness,
                settings.Translation.CustomInstructions,
                settings.Translation.Glossary,
                settings.Translation.ExcludedTerms)
        };

        return client.TranslateAsync(
            apiKey,
            model.Trim(),
            enrichedRequest,
            settings.Api.RequestTimeoutSeconds,
            settings.Api.TransientRetryCount,
            cancellationToken);
    }
}
