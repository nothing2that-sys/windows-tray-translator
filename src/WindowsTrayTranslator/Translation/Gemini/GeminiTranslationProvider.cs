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

    /// <summary>
    /// Generic entry point for non-translation actions. It reuses the same API key, model policy,
    /// timeout and retry settings as translation, so a caller never has to reach the client
    /// directly. The fallback model is deliberately not reachable from here.
    /// </summary>
    internal Task<TranslationResult> GenerateAsync(
        PromptPlan plan,
        CancellationToken cancellationToken)
    {
        AppSettings settings = settingsAccessor();
        string model = ResolveActionModel(settings.Api);
        if (TryResolveApiKey(model, out string apiKey, out TranslationResult? failure))
        {
            return Task.FromResult(failure!);
        }

        return client.GenerateAsync(
            apiKey,
            model.Trim(),
            plan.SystemPrompt,
            plan.UserPrompt,
            settings.Api.ActionRequestTimeoutSeconds,
            settings.Api.TransientRetryCount,
            cancellationToken);
    }

    public Task<TranslationResult> TranslateWithModelAsync(
        TranslationRequest request,
        string model,
        CancellationToken cancellationToken)
    {
        if (TryResolveApiKey(model, out string apiKey, out TranslationResult? failure))
        {
            return Task.FromResult(failure!);
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

    /// <summary>
    /// Quick Action generation runs on its own model when one is configured, because a summary needs
    /// more capability than a translation. A blank value falls back to the translation model instead
    /// of failing, so an empty box can never disable the actions.
    /// </summary>
    internal static string ResolveActionModel(ApiSettings api) =>
        api.EnableActionModel && !string.IsNullOrWhiteSpace(api.ActionModel)
            ? api.ActionModel
            : api.Model;

    /// <summary>Reads the stored key once and applies the shared model policy.</summary>
    private bool TryResolveApiKey(string model, out string apiKey, out TranslationResult? failure)
    {
        apiKey = secretStore.ReadSecret(SecretNames.GeminiApiKey) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            failure = TranslationResult.Failure(
                TranslationFailureKind.MissingApiKey,
                "Gemini API 키를 설정해 주세요.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(model) || GeminiModelPolicy.IsKnownRetired(model))
        {
            failure = TranslationResult.Failure(
                TranslationFailureKind.ModelUnavailable,
                "설정한 Gemini 모델을 사용할 수 없습니다. 설정에서 모델 목록을 새로고침해 주세요.");
            return true;
        }

        failure = null;
        return false;
    }
}
