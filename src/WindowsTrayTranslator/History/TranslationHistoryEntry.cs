namespace WindowsTrayTranslator.History;

public enum TranslationHistoryKind
{
    Read,
    Replace
}

public sealed record TranslationHistoryEntry(
    DateTimeOffset Timestamp,
    TranslationHistoryKind Kind,
    string TargetLanguage,
    string OriginalText,
    string TranslatedText);
