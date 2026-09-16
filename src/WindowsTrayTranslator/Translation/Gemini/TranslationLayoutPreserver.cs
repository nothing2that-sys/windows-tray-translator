using System.Text;
using System.Text.RegularExpressions;

namespace WindowsTrayTranslator.Translation.Gemini;

internal sealed class TranslationLayoutPreserver
{
    private static readonly Regex LineBreakPattern = new("(\r\n|\r|\n)", RegexOptions.Compiled);
    private static readonly Regex MarkerPattern = new("⟦WTT-LINE-(\\d{6})⟧", RegexOptions.Compiled);

    private readonly string[] originalLines;
    private readonly string[] separators;

    private TranslationLayoutPreserver(string requestText, string[] originalLines, string[] separators)
    {
        RequestText = requestText;
        this.originalLines = originalLines;
        this.separators = separators;
    }

    public string RequestText { get; }
    public bool UsesMarkers => separators.Length > 0;

    public static TranslationLayoutPreserver Prepare(string originalText)
    {
        string[] parts = LineBreakPattern.Split(originalText);
        if (parts.Length == 1)
        {
            return new TranslationLayoutPreserver(originalText, [originalText], []);
        }

        List<string> lines = [];
        List<string> lineSeparators = [];
        for (int index = 0; index < parts.Length; index++)
        {
            if (index % 2 == 0)
            {
                lines.Add(parts[index]);
            }
            else
            {
                lineSeparators.Add(parts[index]);
            }
        }

        StringBuilder markedText = new();
        for (int index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                markedText.Append('\n');
            }

            string content = lines[index].TrimStart(' ', '\t');
            markedText.Append($"⟦WTT-LINE-{index + 1:000000}⟧");
            if (content.Length > 0)
            {
                markedText.Append(' ').Append(content);
            }
        }

        return new TranslationLayoutPreserver(markedText.ToString(), lines.ToArray(), lineSeparators.ToArray());
    }

    public string Restore(string translatedText, out bool restored)
    {
        restored = false;
        if (!UsesMarkers)
        {
            return SanitizeOutput(translatedText);
        }

        MatchCollection matches = MarkerPattern.Matches(translatedText);
        if (matches.Count != originalLines.Length)
        {
            return SanitizeOutput(translatedText);
        }

        for (int index = 0; index < matches.Count; index++)
        {
            if (!int.TryParse(matches[index].Groups[1].Value, out int lineNumber) || lineNumber != index + 1)
            {
                return SanitizeOutput(translatedText);
            }
        }

        StringBuilder result = new();
        for (int index = 0; index < matches.Count; index++)
        {
            Match marker = matches[index];
            int end = index + 1 < matches.Count ? matches[index + 1].Index : translatedText.Length;
            string translatedLine = translatedText[(marker.Index + marker.Length)..end]
                .Trim('\r', '\n');
            if (translatedLine.StartsWith(' '))
            {
                translatedLine = translatedLine[1..];
            }

            translatedLine = translatedLine
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .TrimEnd();

            string originalLine = originalLines[index];
            if (string.IsNullOrWhiteSpace(originalLine))
            {
                result.Append(originalLine);
            }
            else
            {
                int indentationLength = originalLine.Length - originalLine.TrimStart(' ', '\t').Length;
                result.Append(originalLine.AsSpan(0, indentationLength));
                result.Append(translatedLine);
            }

            if (index < separators.Length)
            {
                result.Append(separators[index]);
            }
        }

        restored = true;
        return result.ToString();
    }

    /// <summary>
    /// Used by marker-free generation to reject a leaked internal marker instead of silently
    /// stripping it, which would also remove the output's leading indentation.
    /// </summary>
    internal static bool ContainsMarker(string text) => MarkerPattern.IsMatch(text);

    internal static string SanitizeOutput(string translatedText)
    {
        if (!MarkerPattern.IsMatch(translatedText))
        {
            return translatedText;
        }

        string withoutMarkers = MarkerPattern.Replace(translatedText, string.Empty);
        return Regex.Replace(withoutMarkers, "(?m)^[ \t]+", string.Empty).Trim();
    }
}
