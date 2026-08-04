namespace WindowsTrayTranslator.Configuration;

public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
    public ApiSettings Api { get; set; } = new();
}

public sealed class GeneralSettings
{
    public bool TranslationEnabled { get; set; } = true;
    public bool RunAtWindowsStartup { get; set; }
    public bool ShowTranslatingPopup { get; set; } = true;
    // Legacy shared timeout retained so older configuration files can seed both new values.
    public int PopupTimeoutMs { get; set; } = 5000;
    public int ReadPopupTimeoutMs { get; set; } = -1;
    public int ReplacePopupTimeoutMs { get; set; } = -1;
    public bool EnableLogging { get; set; } = true;
    public int LogRetentionDays { get; set; } = 14;
}

public sealed class TranslationSettings
{
    public string ReadTargetLanguage { get; set; } = "Korean";
    public string ReplaceTargetLanguage { get; set; } = "English";
    public string ReplaceFormat { get; set; } = "TranslationWithOriginal";
    public string ReadHotkey { get; set; } = "Alt+R";
    public string ReplaceHotkey { get; set; } = "Alt+T";
    public int MaxInputCharacters { get; set; } = 5000;
    public int ClipboardCopyTimeoutMs { get; set; } = 2500;
    public int ClipboardRestoreDelayMs { get; set; } = 300;
    public int ClipboardRetryCount { get; set; } = 5;
    public int ClipboardRetryDelayMs { get; set; } = 50;
    public bool PreferUiAutomation { get; set; }
    public string ReadRecentTargetLanguage { get; set; } = "Korean";
    public string ReplaceRecentTargetLanguage { get; set; } = "English";
    public string TranslationStyle { get; set; } = "Natural";
    public string Politeness { get; set; } = "Auto";
    public string CustomInstructions { get; set; } = string.Empty;
    public string Glossary { get; set; } = string.Empty;
    public string ExcludedTerms { get; set; } = string.Empty;
}

public sealed class ApiSettings
{
    public string Provider { get; set; } = "Gemini";
    public string Model { get; set; } = "gemini-3.5-flash-lite";
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int TransientRetryCount { get; set; } = 2;
    public bool EnableFallbackModel { get; set; }
    public string FallbackModel { get; set; } = "gemini-3.1-flash-lite";
    public bool OfferFallbackAfterCompletion { get; set; }
}
