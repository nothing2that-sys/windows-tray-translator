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

    public string? LastLoadWarning { get; private set; }

    public ConfigurationService(string settingsFile, ILogger logger)
    {
        this.settingsFile = settingsFile;
        this.logger = logger;
    }

    public AppSettings Load()
    {
        LastLoadWarning = null;
        if (!File.Exists(settingsFile))
        {
            AppSettings defaults = SettingsValidator.Normalize(DefaultSettingsFactory.Create());
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
            AppSettings defaults = SettingsValidator.Normalize(DefaultSettingsFactory.Create());
            try
            {
                File.Copy(settingsFile, settingsFile + ".bak", overwrite: true);
                Save(defaults);
                LastLoadWarning = "손상된 설정을 기본값으로 복구했으며 원본은 .bak 파일로 보존했습니다.";
            }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException)
            {
                logger.Error("손상된 설정 파일을 백업하거나 복구하지 못했습니다.", backupError);
                LastLoadWarning = "설정 파일을 읽지 못해 정규화된 기본값을 사용합니다. 원본 백업에는 실패했습니다.";
            }

            return defaults;
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
