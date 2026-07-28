using WindowsTrayTranslator.Translation;

namespace WindowsTrayTranslator.Tests;

public sealed class AutomaticTargetLanguageResolverTests
{
    [Theory]
    [InlineData("안녕하세요.", "English")]
    [InlineData("Please check 설비 A.", "English")]
    [InlineData("Sếp ơi nay sếp xem em máy AA với ạ", "Korean")]
    [InlineData("Please check the fixture.", "Korean")]
    [InlineData("確認をお願いします。", "Korean")]
    public void Resolve_Auto_UsesDeterministicHangulRule(string sourceText, string expected)
    {
        Assert.Equal(expected, AutomaticTargetLanguageResolver.Resolve("Auto", sourceText));
    }

    [Fact]
    public void Resolve_ExplicitTarget_ReturnsConfiguredLanguage()
    {
        Assert.Equal(
            "Vietnamese",
            AutomaticTargetLanguageResolver.Resolve("Vietnamese", "한국어 원문"));
    }
}
