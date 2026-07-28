namespace WindowsTrayTranslator.Translation;

public static class AutomaticTargetLanguageResolver
{
    public static string Resolve(string configuredTargetLanguage, string sourceText)
    {
        if (!string.Equals(configuredTargetLanguage.Trim(), "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return configuredTargetLanguage;
        }

        return ContainsHangul(sourceText) ? "English" : "Korean";
    }

    internal static bool ContainsHangul(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (char character in text)
        {
            if (character is
                (>= '\u1100' and <= '\u11FF') or
                (>= '\u3130' and <= '\u318F') or
                (>= '\uA960' and <= '\uA97F') or
                (>= '\uAC00' and <= '\uD7A3') or
                (>= '\uD7B0' and <= '\uD7FF'))
            {
                return true;
            }
        }

        return false;
    }
}
