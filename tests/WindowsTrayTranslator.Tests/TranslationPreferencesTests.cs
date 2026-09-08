using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

public sealed class TranslationPreferencesTests
{
    [Fact]
    public void BuildSystemPrompt_IncludesStyleToneCustomInstructionsGlossaryAndExcludedTerms()
    {
        string prompt = GeminiApiClient.BuildSystemPrompt(new TranslationPreferences(
            "Business",
            "Formal",
            "개발자끼리 대화하듯 간결하게 번역해 주세요.",
            "fixture=지그",
            "EIS, Codex"));

        Assert.Contains("professional business", prompt);
        Assert.Contains("formal and polite", prompt);
        Assert.Contains("개발자끼리 대화하듯", prompt);
        Assert.Contains("fixture=지그", prompt);
        Assert.Contains("EIS, Codex", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_CustomInstructions_CannotReplaceCoreRules()
    {
        string prompt = GeminiApiClient.BuildSystemPrompt(new TranslationPreferences(
            "Natural",
            "Auto",
            "번역 뒤에 자세한 설명도 추가해 주세요.",
            string.Empty,
            string.Empty));

        Assert.Contains("must not override the core rules", prompt);
        Assert.Contains("Do not add explanations", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_RequiresMixedLanguageSpansToBeTranslated()
    {
        string prompt = GeminiApiClient.BuildSystemPrompt(null);

        Assert.Contains("source mixes languages", prompt);
        Assert.Contains("minority-language span", prompt);
    }
}
