using System.Text.Json.Serialization;

namespace WindowsTrayTranslator.Translation.Gemini;

internal sealed class GeminiGenerateRequest
{
    [JsonPropertyName("system_instruction")]
    public required GeminiContent SystemInstruction { get; init; }

    [JsonPropertyName("contents")]
    public required GeminiContent[] Contents { get; init; }
}

internal sealed class GeminiContent
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }

    [JsonPropertyName("parts")]
    public GeminiPart[] Parts { get; init; } = [];
}

internal sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

internal sealed class GeminiGenerateResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[]? Candidates { get; init; }

    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; init; }
}

internal sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; init; }
}

internal sealed class GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; init; }
}
