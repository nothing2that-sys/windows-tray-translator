namespace WindowsTrayTranslator.Translation;

public enum ReplaceOutputFormat
{
    TranslationOnly,
    TranslationWithOriginal,
    OriginalWithTranslation,
    TranslationAndOriginalOnNewLine
}

public static class ReplaceOutputFormatter
{
    public static ReplaceOutputFormat Parse(string? value)
    {
        if (Enum.TryParse(value, ignoreCase: true, out ReplaceOutputFormat format) &&
            Enum.IsDefined(format))
        {
            return format;
        }

        return ReplaceOutputFormat.TranslationWithOriginal;
    }

    public static string Format(string original, string translation, ReplaceOutputFormat format) => format switch
    {
        ReplaceOutputFormat.TranslationOnly => translation,
        ReplaceOutputFormat.TranslationWithOriginal => $"{translation}({original})",
        ReplaceOutputFormat.OriginalWithTranslation => $"{original}({translation})",
        ReplaceOutputFormat.TranslationAndOriginalOnNewLine => $"{translation}{Environment.NewLine}({original})",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };
}
