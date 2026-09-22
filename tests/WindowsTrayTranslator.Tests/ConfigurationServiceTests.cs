using WindowsTrayTranslator.Configuration;

namespace WindowsTrayTranslator.Tests;

public sealed class ConfigurationServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"WindowsTrayTranslator.Tests.{Guid.NewGuid():N}");

    [Fact]
    public void Load_WhenMissing_CreatesDefaults()
    {
        string file = Path.Combine(directory, "config", "appsettings.json");
        ConfigurationService service = new(file, new TestLogger());

        AppSettings settings = service.Load();

        Assert.Equal("Alt+R", settings.Translation.ReadHotkey);
        Assert.Equal("Alt+T", settings.Translation.ReplaceHotkey);
        Assert.Equal("Ctrl+Alt+A", settings.Translation.ActionPaletteHotkey);
        Assert.Equal("gemini-3.5-flash-lite", settings.Api.Model);
        Assert.Equal(5000, settings.General.ReadPopupTimeoutMs);
        Assert.Equal(5000, settings.General.ReplacePopupTimeoutMs);
        Assert.False(settings.Api.OfferFallbackAfterCompletion);
        Assert.False(settings.General.EnableTranslationHistory);
        Assert.Equal(30, settings.General.HistoryRetentionDays);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Load_FallbackAfterCompletionOption_PreservesEnabledValue()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(file, """
            {
              "Api": {
                "EnableFallbackModel": true,
                "FallbackModel": "fallback-model",
                "OfferFallbackAfterCompletion": true
              }
            }
            """);

        AppSettings settings = new ConfigurationService(file, new TestLogger()).Load();

        Assert.True(settings.Api.EnableFallbackModel);
        Assert.True(settings.Api.OfferFallbackAfterCompletion);
    }

    [Fact]
    public void Load_InvalidValues_NormalizesSafeRanges()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(file, """
            {
              "General": { "PopupTimeoutMs": -1, "LogRetentionDays": 1000, "HistoryRetentionDays": 0 },
              "Translation": { "MaxInputCharacters": 0, "ReadHotkey": "R", "ReplaceHotkey": "Alt+T", "ReplaceFormat": "999" },
              "Api": { "Model": "", "RequestTimeoutSeconds": 999, "ActionRequestTimeoutSeconds": 9999 }
            }
            """);

        AppSettings settings = new ConfigurationService(file, new TestLogger()).Load();

        Assert.Equal(1000, settings.General.PopupTimeoutMs);
        Assert.Equal(1000, settings.General.ReadPopupTimeoutMs);
        Assert.Equal(1000, settings.General.ReplacePopupTimeoutMs);
        Assert.Equal(365, settings.General.LogRetentionDays);
        Assert.Equal(1, settings.General.HistoryRetentionDays);
        Assert.Equal(1, settings.Translation.MaxInputCharacters);
        Assert.Equal("Alt+R", settings.Translation.ReadHotkey);
        Assert.Equal("TranslationWithOriginal", settings.Translation.ReplaceFormat);
        Assert.Equal("gemini-3.5-flash-lite", settings.Api.Model);
        Assert.Equal(120, settings.Api.RequestTimeoutSeconds);
        Assert.Equal(300, settings.Api.ActionRequestTimeoutSeconds);
    }

    [Fact]
    public void Load_SeparatePopupTimeouts_NormalizesIndependently()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(file, """
            {
              "General": {
                "PopupTimeoutMs": 5000,
                "ReadPopupTimeoutMs": 2500,
                "ReplacePopupTimeoutMs": 9000
              }
            }
            """);

        AppSettings settings = new ConfigurationService(file, new TestLogger()).Load();

        Assert.Equal(2500, settings.General.ReadPopupTimeoutMs);
        Assert.Equal(9000, settings.General.ReplacePopupTimeoutMs);
    }

    [Fact]
    public void Load_DuplicateHotkeys_RestoresIndependentDefaults()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(file, """
            {
              "Translation": { "ReadHotkey": "Ctrl+Shift+9", "ReplaceHotkey": "Ctrl+Shift+9" }
            }
            """);

        AppSettings settings = new ConfigurationService(file, new TestLogger()).Load();

        Assert.Equal("Alt+R", settings.Translation.ReadHotkey);
        Assert.Equal("Alt+T", settings.Translation.ReplaceHotkey);
    }

    [Fact]
    public void Load_AutoTargets_PreservesReadAndMigratesReplaceToSelect()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(file, """
            {
              "Translation": { "ReadTargetLanguage": "Auto", "ReplaceTargetLanguage": "Auto" }
            }
            """);

        AppSettings settings = new ConfigurationService(file, new TestLogger()).Load();

        Assert.Equal("Auto", settings.Translation.ReadTargetLanguage);
        Assert.Equal("Select", settings.Translation.ReplaceTargetLanguage);
    }

    [Fact]
    public void Load_EmptyOptionalPaletteHotkey_PreservesDisabledState()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(file, """
            { "Translation": { "ActionPaletteHotkey": "" } }
            """);

        ConfigurationService service = new(file, new TestLogger());
        AppSettings settings = service.Load();
        service.Save(settings);
        AppSettings reloaded = service.Load();

        Assert.Equal(string.Empty, reloaded.Translation.ActionPaletteHotkey);
    }

    /// <summary>
    /// K5: an upgrade reads a settings file written before the palette existed. The new key must
    /// appear with its default and every existing customisation must survive untouched.
    /// </summary>
    [Fact]
    public void Load_LegacyFileWithoutPaletteHotkey_AddsDefaultAndPreservesExistingValues()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(file, """
            {
              "General": { "ReadPopupTimeoutMs": 4200, "EnableTranslationHistory": true, "LogRetentionDays": 21 },
              "Translation": {
                "ReadHotkey": "Ctrl+Shift+R",
                "ReplaceHotkey": "Ctrl+Shift+T",
                "ReadTargetLanguage": "Japanese",
                "ReplaceFormat": "TranslationOnly",
                "Glossary": "EIS=검사 시스템",
                "PreferUiAutomation": true
              },
              "Api": { "Model": "gemini-3.5-flash-lite", "RequestTimeoutSeconds": 45 }
            }
            """);

        ConfigurationService service = new(file, new TestLogger());
        AppSettings settings = service.Load();

        Assert.Equal("Ctrl+Alt+A", settings.Translation.ActionPaletteHotkey);
        Assert.Equal("Ctrl+Shift+R", settings.Translation.ReadHotkey);
        Assert.Equal("Ctrl+Shift+T", settings.Translation.ReplaceHotkey);
        Assert.Equal("Japanese", settings.Translation.ReadTargetLanguage);
        Assert.Equal("TranslationOnly", settings.Translation.ReplaceFormat);
        Assert.Equal("EIS=검사 시스템", settings.Translation.Glossary);
        Assert.True(settings.Translation.PreferUiAutomation);
        Assert.Equal(4200, settings.General.ReadPopupTimeoutMs);
        Assert.True(settings.General.EnableTranslationHistory);
        Assert.Equal(21, settings.General.LogRetentionDays);
        Assert.Equal(45, settings.Api.RequestTimeoutSeconds);

        service.Save(settings);
        AppSettings reloaded = service.Load();
        Assert.Equal("Ctrl+Alt+A", reloaded.Translation.ActionPaletteHotkey);
        Assert.Equal("Ctrl+Shift+R", reloaded.Translation.ReadHotkey);
        Assert.Equal("EIS=검사 시스템", reloaded.Translation.Glossary);
    }

    [Fact]
    public void Load_PaletteConflictsWithCore_DisablesOnlyPalette()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(file, """
            { "Translation": { "ReadHotkey": "Ctrl+U", "ReplaceHotkey": "Ctrl+I", "ActionPaletteHotkey": "Ctrl+U" } }
            """);

        AppSettings settings = new ConfigurationService(file, new TestLogger()).Load();

        Assert.Equal("Ctrl+U", settings.Translation.ReadHotkey);
        Assert.Equal("Ctrl+I", settings.Translation.ReplaceHotkey);
        Assert.Equal(string.Empty, settings.Translation.ActionPaletteHotkey);
    }

    [Fact]
    public void Load_DamagedJson_NormalizesDefaultsAndPreservesBackup()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        const string damaged = "{ invalid json";
        File.WriteAllText(file, damaged);
        ConfigurationService service = new(file, new TestLogger());

        AppSettings settings = service.Load();

        Assert.Equal(5000, settings.General.ReadPopupTimeoutMs);
        Assert.Equal("Alt+R", settings.Translation.ReadHotkey);
        Assert.Equal(damaged, File.ReadAllText(file + ".bak"));
        Assert.NotNull(service.LastLoadWarning);
        Assert.Equal("Alt+R", new ConfigurationService(file, new TestLogger()).Load().Translation.ReadHotkey);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
