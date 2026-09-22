using System.Text.Json;

namespace WindowsTrayTranslator.Translation.Gemini;

public static class GeminiResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TranslationResult Parse(string json)
    {
        GeminiGenerateResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<GeminiGenerateResponse>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return TranslationResult.Failure(TranslationFailureKind.InvalidResponse, "서버 응답을 해석하지 못했습니다.");
        }

        if (!string.IsNullOrWhiteSpace(response?.PromptFeedback?.BlockReason))
        {
            return TranslationResult.Failure(TranslationFailureKind.SafetyBlocked, "안전 정책으로 인해 결과를 받을 수 없습니다.");
        }

        GeminiCandidate? candidate = response?.Candidates?.FirstOrDefault();
        if (candidate is null)
        {
            return TranslationResult.Failure(TranslationFailureKind.InvalidResponse, "서버 응답에 결과가 없습니다.");
        }

        if (string.Equals(candidate.FinishReason, "SAFETY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.FinishReason, "BLOCKLIST", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.FinishReason, "PROHIBITED_CONTENT", StringComparison.OrdinalIgnoreCase))
        {
            return TranslationResult.Failure(TranslationFailureKind.SafetyBlocked, "안전 정책으로 인해 결과를 받을 수 없습니다.");
        }

        string translated = string.Concat(candidate.Content?.Parts.Select(part => part.Text) ?? []).Trim();
        translated = RemoveCodeFence(translated);
        if (string.IsNullOrWhiteSpace(translated))
        {
            return TranslationResult.Failure(TranslationFailureKind.EmptyResponse, "결과가 비어 있습니다.");
        }

        return TranslationResult.Success(translated);
    }

    private static string RemoveCodeFence(string value)
    {
        string trimmed = value.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal) ||
            !trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        int firstLineBreak = trimmed.IndexOf('\n');
        if (firstLineBreak < 0)
        {
            return string.Empty;
        }

        return trimmed[(firstLineBreak + 1)..^3].Trim();
    }
}
