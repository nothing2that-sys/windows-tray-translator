namespace WindowsTrayTranslator.Translation;

public enum TranslationFailureKind
{
    None,
    MissingApiKey,
    Network,
    Authentication,
    RateLimit,
    Server,
    Timeout,
    EmptyResponse,
    SafetyBlocked,
    InvalidResponse,
    ModelUnavailable,
    InvalidRequest,
    Cancelled
}

public sealed record TranslationResult(
    bool IsSuccess,
    string? TranslatedText,
    TranslationFailureKind FailureKind,
    string? ErrorMessage)
{
    public static TranslationResult Success(string text) =>
        new(true, text, TranslationFailureKind.None, null);

    public static TranslationResult Failure(TranslationFailureKind kind, string message) =>
        new(false, null, kind, message);
}
