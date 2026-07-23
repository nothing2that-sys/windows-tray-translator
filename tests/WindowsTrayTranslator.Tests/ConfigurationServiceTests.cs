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
        Assert.Equal("gemini-3.5-flash-lite", settings.Api.Model);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Load_InvalidValues_NormalizesSafeRanges()
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(file, """
            {
              "General": { "PopupTimeoutMs": -1, "LogRetentionDays": 1000 },
              "Translation": { "MaxInputCharacters": 0, "ReadHotkey": "R", "ReplaceHotkey": "Alt+T", "ReplaceFormat": "999" },
              "Api": { "Model": "", "RequestTimeoutSeconds": 999 }
            }
            """);

        AppSettings settings = new ConfigurationService(file, new TestLogger()).Load();

        Assert.Equal(1000, settings.General.PopupTimeoutMs);
        Assert.Equal(365, settings.General.LogRetentionDays);
        Assert.Equal(1, settings.Translation.MaxInputCharacters);
        Assert.Equal("Alt+R", settings.Translation.ReadHotkey);
        Assert.Equal("TranslationWithOriginal", settings.Translation.ReplaceFormat);
        Assert.Equal("gemini-3.5-flash-lite", settings.Api.Model);
        Assert.Equal(120, settings.Api.RequestTimeoutSeconds);
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

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
