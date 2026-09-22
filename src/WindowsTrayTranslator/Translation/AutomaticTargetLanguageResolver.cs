using System.Text.RegularExpressions;

namespace WindowsTrayTranslator.Translation;

public static class AutomaticTargetLanguageResolver
{
    private static readonly Regex WordPattern = new(
        @"[\p{L}\p{M}\p{N}_]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Resolve(string configuredTargetLanguage, string sourceText)
    {
        if (!string.Equals(configuredTargetLanguage.Trim(), "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return configuredTargetLanguage;
        }

        return IsKoreanDominant(sourceText) ? "English" : "Korean";
    }

    internal static bool IsKoreanDominant(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        int koreanWordCount = 0;
        int otherLanguageWordCount = 0;

        foreach (Match match in WordPattern.Matches(text))
        {
            string word = match.Value;
            if (ContainsHangul(word))
            {
                koreanWordCount++;
            }
            else if (ContainsNonHangulLetter(word) && !IsLikelyTechnicalIdentifier(word))
            {
                otherLanguageWordCount++;
            }
        }

        if (koreanWordCount == 0)
        {
            return false;
        }

        if (otherLanguageWordCount == 0)
        {
            return true;
        }

        return koreanWordCount >= otherLanguageWordCount;
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

    private static bool ContainsNonHangulLetter(string text) =>
        text.Any(character => char.IsLetter(character) && !ContainsHangul(character.ToString()));

    private static bool IsLikelyTechnicalIdentifier(string word)
    {
        if (word.Length <= 1 || word.Contains('_') || word.Any(char.IsDigit))
        {
            return true;
        }

        char[] letters = word.Where(char.IsLetter).ToArray();
        if (letters.Length >= 2 && letters.All(char.IsUpper))
        {
            return true;
        }

        int internalUppercaseCount = word.Skip(1).Count(char.IsUpper);
        return internalUppercaseCount >= 2;
    }
}
