using WindowsTrayTranslator.Hotkeys;

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
        settings.General.LogRetentionDays = Math.Clamp(settings.General.LogRetentionDays, 1, 365);
        settings.Translation.MaxInputCharacters = Math.Clamp(settings.Translation.MaxInputCharacters, 1, 100000);
        settings.Translation.ClipboardCopyTimeoutMs = Math.Clamp(settings.Translation.ClipboardCopyTimeoutMs, 100, 10000);
        settings.Translation.ClipboardRestoreDelayMs = Math.Clamp(settings.Translation.ClipboardRestoreDelayMs, 50, 5000);
        settings.Translation.ClipboardRetryCount = Math.Clamp(settings.Translation.ClipboardRetryCount, 1, 20);
        settings.Translation.ClipboardRetryDelayMs = Math.Clamp(settings.Translation.ClipboardRetryDelayMs, 10, 1000);
        settings.Api.RequestTimeoutSeconds = Math.Clamp(settings.Api.RequestTimeoutSeconds, 5, 120);
        settings.Api.TransientRetryCount = Math.Clamp(settings.Api.TransientRetryCount, 0, 5);

        settings.Translation.ReadTargetLanguage = NonEmpty(settings.Translation.ReadTargetLanguage, "Korean");
        settings.Translation.ReplaceTargetLanguage = NonEmpty(settings.Translation.ReplaceTargetLanguage, "English");
        settings.Translation.ReplaceFormat = NonEmpty(settings.Translation.ReplaceFormat, "TranslationWithOriginal");
        settings.Translation.ReadHotkey = NonEmpty(settings.Translation.ReadHotkey, "Alt+R");
        settings.Translation.ReplaceHotkey = NonEmpty(settings.Translation.ReplaceHotkey, "Alt+T");
        settings.Translation.ReadHotkey = ValidHotkeyOrDefault(settings.Translation.ReadHotkey, "Alt+R");
        settings.Translation.ReplaceHotkey = ValidHotkeyOrDefault(settings.Translation.ReplaceHotkey, "Alt+T");
        if (HotkeyDefinition.Parse(settings.Translation.ReadHotkey) == HotkeyDefinition.Parse(settings.Translation.ReplaceHotkey))
        {
            settings.Translation.ReadHotkey = "Alt+R";
            settings.Translation.ReplaceHotkey = "Alt+T";
        }
        settings.Api.Provider = "Gemini";
        settings.Api.Model = NonEmpty(settings.Api.Model, "gemini-3.5-flash");
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
}
