using WindowsTrayTranslator.Translation;

namespace WindowsTrayTranslator.Tests;

public sealed class AutomaticTargetLanguageResolverTests
{
    [Theory]
    [InlineData("안녕하세요.", "English")]
    [InlineData("Please check 설비 A.", "Korean")]
    [InlineData("Sếp ơi nay sếp xem em máy AA với ạ", "Korean")]
    [InlineData("Please check the fixture.", "Korean")]
    [InlineData("確認をお願いします。", "Korean")]
    [InlineData("회의 meeting 일정", "English")]
    [InlineData("GetDpiForMonitor API를 호출하고 screen API 결과를 비교해줘.", "English")]
    public void Resolve_Auto_UsesDominantNaturalLanguage(string sourceText, string expected)
    {
        Assert.Equal(expected, AutomaticTargetLanguageResolver.Resolve("Auto", sourceText));
    }

    [Fact]
    public void Resolve_Auto_EnglishSentenceWithKoreanDisplayName_TargetsKorean()
    {
        const string source = "The mandatory electron screen API and Win32 GetDpiForMonitor checks both report 1.25. " +
            "A few possibilities remain, or 3번 모니터 maps to a different display rect than expected.";

        Assert.Equal("Korean", AutomaticTargetLanguageResolver.Resolve("Auto", source));
    }

    [Fact]
    public void Resolve_ExplicitTarget_ReturnsConfiguredLanguage()
    {
        Assert.Equal(
            "Vietnamese",
            AutomaticTargetLanguageResolver.Resolve("Vietnamese", "한국어 원문"));
    }
}
