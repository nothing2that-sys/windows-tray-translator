using WindowsTrayTranslator.Hotkeys;
using WindowsTrayTranslator.Translation;

namespace WindowsTrayTranslator.Configuration;

public static class SettingsValidator
{
    public static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= DefaultSettingsFactory.Create();
        settings.General ??= new GeneralSettings();
        settings.Translation ??= new TranslationSettings();
        settings.Api ??= new ApiSettings();

        settings.General.PopupTimeoutMs = Math.Clamp(settings.General.PopupTimeoutMs, 1000, 60000);
        settings.General.ReadPopupTimeoutMs = settings.General.ReadPopupTimeoutMs == -1
            ? settings.General.PopupTimeoutMs
            : Math.Clamp(settings.General.ReadPopupTimeoutMs, 1000, 60000);
        settings.General.ReplacePopupTimeoutMs = settings.General.ReplacePopupTimeoutMs == -1
            ? settings.General.PopupTimeoutMs
            : Math.Clamp(settings.General.ReplacePopupTimeoutMs, 1000, 60000);
        settings.General.LogRetentionDays = Math.Clamp(settings.General.LogRetentionDays, 1, 365);
        settings.General.HistoryRetentionDays = Math.Clamp(settings.General.HistoryRetentionDays, 1, 365);
        settings.Translation.MaxInputCharacters = Math.Clamp(settings.Translation.MaxInputCharacters, 1, 100000);
        settings.Translation.ClipboardCopyTimeoutMs = Math.Clamp(settings.Translation.ClipboardCopyTimeoutMs, 100, 10000);
        settings.Translation.ClipboardRestoreDelayMs = Math.Clamp(settings.Translation.ClipboardRestoreDelayMs, 50, 5000);
        settings.Translation.ClipboardRetryCount = Math.Clamp(settings.Translation.ClipboardRetryCount, 1, 20);
        settings.Translation.ClipboardRetryDelayMs = Math.Clamp(settings.Translation.ClipboardRetryDelayMs, 10, 1000);
        settings.Api.RequestTimeoutSeconds = Math.Clamp(settings.Api.RequestTimeoutSeconds, 5, 120);
        settings.Api.TransientRetryCount = Math.Clamp(settings.Api.TransientRetryCount, 0, 5);
        settings.Api.ActionRequestTimeoutSeconds = Math.Clamp(settings.Api.ActionRequestTimeoutSeconds, 5, 300);

        settings.Translation.ReadTargetLanguage = ValidReadTargetLanguage(settings.Translation.ReadTargetLanguage);
        settings.Translation.ReplaceTargetLanguage = ValidReplaceTargetLanguage(settings.Translation.ReplaceTargetLanguage);
        settings.Translation.ReadRecentTargetLanguage = NonEmpty(settings.Translation.ReadRecentTargetLanguage, "Korean");
        settings.Translation.ReplaceRecentTargetLanguage = NonEmpty(settings.Translation.ReplaceRecentTargetLanguage, "English");
        settings.Translation.TranslationStyle = ValidChoice(settings.Translation.TranslationStyle, "Natural", "Natural", "Literal", "Business");
        settings.Translation.Politeness = ValidChoice(settings.Translation.Politeness, "Auto", "Auto", "Formal", "Casual");
        settings.Translation.CustomInstructions = LimitLength(settings.Translation.CustomInstructions, 4000);
        settings.Translation.Glossary = LimitLength(settings.Translation.Glossary, 4000);
        settings.Translation.ExcludedTerms = LimitLength(settings.Translation.ExcludedTerms, 2000);
        settings.Translation.ReplaceFormat = ValidReplaceFormatOrDefault(settings.Translation.ReplaceFormat);
        settings.Translation.ReadHotkey = NonEmpty(settings.Translation.ReadHotkey, "Alt+R");
        settings.Translation.ReplaceHotkey = NonEmpty(settings.Translation.ReplaceHotkey, "Alt+T");
        settings.Translation.ReadHotkey = ValidHotkeyOrDefault(settings.Translation.ReadHotkey, "Alt+R");
        settings.Translation.ReplaceHotkey = ValidHotkeyOrDefault(settings.Translation.ReplaceHotkey, "Alt+T");
        settings.Translation.ActionPaletteHotkey = ValidOptionalHotkeyOrEmpty(settings.Translation.ActionPaletteHotkey);
        if (HotkeyDefinition.Parse(settings.Translation.ReadHotkey) == HotkeyDefinition.Parse(settings.Translation.ReplaceHotkey))
        {
            settings.Translation.ReadHotkey = "Alt+R";
            settings.Translation.ReplaceHotkey = "Alt+T";
        }
        if (!string.IsNullOrEmpty(settings.Translation.ActionPaletteHotkey))
        {
            HotkeyDefinition palette = HotkeyDefinition.Parse(settings.Translation.ActionPaletteHotkey);
            if (palette == HotkeyDefinition.Parse(settings.Translation.ReadHotkey) ||
                palette == HotkeyDefinition.Parse(settings.Translation.ReplaceHotkey))
            {
                settings.Translation.ActionPaletteHotkey = string.Empty;
            }
        }
        settings.Api.Provider = "Gemini";
        settings.Api.Model = NonEmpty(settings.Api.Model, "gemini-3.5-flash-lite");
        settings.Api.FallbackModel = NonEmpty(settings.Api.FallbackModel, "gemini-3.1-flash-lite");
        settings.Api.ActionModel = NonEmpty(settings.Api.ActionModel, "gemini-3.5-flash");
        return settings;
    }

    private static string NonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string ValidHotkeyOrDefault(string value, string fallback)
    {
        try
        {
            HotkeyDefinition.Parse(value);
            return value;
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    private static string ValidOptionalHotkeyOrEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim();
        try
        {
            HotkeyDefinition.Parse(normalized);
            return normalized;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static string ValidReplaceFormatOrDefault(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out ReplaceOutputFormat format) && Enum.IsDefined(format)
            ? format.ToString()
            : ReplaceOutputFormat.TranslationWithOriginal.ToString();

    private static string ValidReplaceTargetLanguage(string? value)
    {
        return ValidSelectableTargetLanguage(value, "English");
    }

    private static string ValidReadTargetLanguage(string? value)
    {
        string normalized = NonEmpty(value, "Korean");
        return string.Equals(normalized, "Auto", StringComparison.OrdinalIgnoreCase)
            ? "Auto"
            : normalized;
    }

    private static string ValidSelectableTargetLanguage(string? value, string fallback)
    {
        string normalized = NonEmpty(value, fallback);
        return string.Equals(normalized, "Auto", StringComparison.OrdinalIgnoreCase)
            ? "Select"
            : normalized;
    }

    private static string ValidChoice(string? value, string fallback, params string[] allowed)
    {
        string normalized = NonEmpty(value, fallback);
        return allowed.FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)) ?? fallback;
    }

    private static string LimitLength(string? value, int maximum) =>
        string.IsNullOrEmpty(value) ? string.Empty : value[..Math.Min(value.Length, maximum)];
}
