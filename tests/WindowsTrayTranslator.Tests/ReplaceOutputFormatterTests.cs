using WindowsTrayTranslator.Translation;

namespace WindowsTrayTranslator.Tests;

public sealed class ReplaceOutputFormatterTests
{
    [Theory]
    [InlineData(ReplaceOutputFormat.TranslationOnly, "Hello.")]
    [InlineData(ReplaceOutputFormat.TranslationWithOriginal, "Hello.(안녕하세요.)")]
    [InlineData(ReplaceOutputFormat.OriginalWithTranslation, "안녕하세요.(Hello.)")]
    public void Format_ComposesConfiguredOutput(ReplaceOutputFormat format, string expected)
    {
        Assert.Equal(expected, ReplaceOutputFormatter.Format("안녕하세요.", "Hello.", format));
    }

    [Fact]
    public void Format_NewLineFormat_UsesPlatformNewLine()
    {
        Assert.Equal(
            $"Hello.{Environment.NewLine}(안녕하세요.)",
            ReplaceOutputFormatter.Format(
                "안녕하세요.",
                "Hello.",
                ReplaceOutputFormat.TranslationAndOriginalOnNewLine));
    }

    [Theory]
    [InlineData("TranslationOnly", ReplaceOutputFormat.TranslationOnly)]
    [InlineData("translationwithoriginal", ReplaceOutputFormat.TranslationWithOriginal)]
    [InlineData("Unknown", ReplaceOutputFormat.TranslationWithOriginal)]
    [InlineData("999", ReplaceOutputFormat.TranslationWithOriginal)]
    [InlineData(null, ReplaceOutputFormat.TranslationWithOriginal)]
    public void Parse_ReturnsConfiguredFormatOrSafeDefault(string? value, ReplaceOutputFormat expected)
    {
        Assert.Equal(expected, ReplaceOutputFormatter.Parse(value));
    }
}
