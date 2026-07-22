using System.Text.Json;
using WindowsTrayTranslator.Logging;

namespace WindowsTrayTranslator.Configuration;

public sealed class ConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string settingsFile;
    private readonly ILogger logger;

    public ConfigurationService(string settingsFile, ILogger logger)
    {
        this.settingsFile = settingsFile;
        this.logger = logger;
    }

    public AppSettings Load()
    {
        if (!File.Exists(settingsFile))
        {
            AppSettings defaults = DefaultSettingsFactory.Create();
            Save(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(settingsFile);
            AppSettings? parsed = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return SettingsValidator.Normalize(parsed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.Error("설정 파일을 읽지 못해 기본값을 사용합니다.", ex);
            return DefaultSettingsFactory.Create();
        }
    }

    public void Save(AppSettings settings)
    {
        SettingsValidator.Normalize(settings);
        string? directory = Path.GetDirectoryName(settingsFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryFile = settingsFile + ".tmp";
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(temporaryFile, json);
        File.Move(temporaryFile, settingsFile, overwrite: true);
    }
}
