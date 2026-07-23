using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

public sealed class GeminiTargetLanguageTests
{
    [Theory]
    [InlineData("Auto")]
    [InlineData("auto")]
    public void BuildTargetLanguageInstruction_Auto_DefinesBidirectionalRule(string value)
    {
        string instruction = GeminiApiClient.BuildTargetLanguageInstruction(value);

        Assert.Contains("Korean", instruction);
        Assert.Contains("English", instruction);
        Assert.Contains("Detect", instruction);
    }

    [Fact]
    public void BuildTargetLanguageInstruction_Vietnamese_UsesRequestedLanguage()
    {
        Assert.Equal(
            "Translate to Vietnamese.",
            GeminiApiClient.BuildTargetLanguageInstruction("Vietnamese"));
    }
}
