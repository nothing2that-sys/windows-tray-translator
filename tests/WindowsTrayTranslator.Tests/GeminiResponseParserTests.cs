using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

public sealed class GeminiResponseParserTests
{
    [Fact]
    public void Parse_NormalResponse_ReturnsText()
    {
        TranslationResult result = GeminiResponseParser.Parse("""
            { "candidates": [{ "content": { "parts": [{ "text": "안녕하세요." }] }, "finishReason": "STOP" }] }
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal("안녕하세요.", result.TranslatedText);
    }

    [Fact]
    public void Parse_CodeFence_RemovesFence()
    {
        TranslationResult result = GeminiResponseParser.Parse("""
            { "candidates": [{ "content": { "parts": [{ "text": "```text\nHello.\n```" }] } }] }
            """);

        Assert.Equal("Hello.", result.TranslatedText);
    }

    [Theory]
    [InlineData("{}", TranslationFailureKind.InvalidResponse)]
    [InlineData("not-json", TranslationFailureKind.InvalidResponse)]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"   \"}]}}]}", TranslationFailureKind.EmptyResponse)]
    [InlineData("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}", TranslationFailureKind.SafetyBlocked)]
    public void Parse_FailureResponse_ClassifiesError(string json, TranslationFailureKind expected)
    {
        TranslationResult result = GeminiResponseParser.Parse(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.FailureKind);
    }
}
