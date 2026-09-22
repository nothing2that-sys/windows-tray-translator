using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

public sealed class GeminiTargetLanguageTests
{
    [Theory]
    [InlineData("Auto")]
    [InlineData("auto")]
    public void BuildTargetLanguageInstruction_Auto_RequiresPriorResolution(string value)
    {
        Assert.Throws<ArgumentException>(() => GeminiApiClient.BuildTargetLanguageInstruction(value));
    }

    [Fact]
    public void BuildTargetLanguageInstruction_Vietnamese_UsesRequestedLanguage()
    {
        Assert.Equal(
            "Translate to Vietnamese.",
            GeminiApiClient.BuildTargetLanguageInstruction("Vietnamese"));
    }
}
