using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

public sealed class TranslationPreferencesTests
{
    [Fact]
    public void BuildSystemPrompt_IncludesStyleToneGlossaryAndExcludedTerms()
    {
        string prompt = GeminiApiClient.BuildSystemPrompt(new TranslationPreferences(
            "Business", "Formal", "fixture=지그", "EIS, Codex"));

        Assert.Contains("professional business", prompt);
        Assert.Contains("formal and polite", prompt);
        Assert.Contains("fixture=지그", prompt);
        Assert.Contains("EIS, Codex", prompt);
    }
}
