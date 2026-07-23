namespace WindowsTrayTranslator.Translation;

public sealed record TranslationRequest(
    string OriginalText,
    string TargetLanguage,
    TranslationPreferences? Preferences = null);

public sealed record TranslationPreferences(
    string Style,
    string Politeness,
    string Glossary,
    string ExcludedTerms);
