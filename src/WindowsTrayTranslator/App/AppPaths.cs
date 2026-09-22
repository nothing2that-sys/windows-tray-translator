namespace WindowsTrayTranslator.App;

public sealed record AppPaths(
    string RootDirectory,
    string ConfigurationDirectory,
    string SettingsFile,
    string SecretFile,
    string LogDirectory,
    string HistoryDirectory)
{
    public static AppPaths CreateDefault()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsTrayTranslator");

        return new AppPaths(
            root,
            Path.Combine(root, "config"),
            Path.Combine(root, "config", "appsettings.json"),
            Path.Combine(root, "secrets.dat"),
            Path.Combine(root, "logs"),
            Path.Combine(root, "history"));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ConfigurationDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(HistoryDirectory);
        MigrateLegacyHistoryFiles();
    }

    private void MigrateLegacyHistoryFiles()
    {
        try
        {
            foreach (string legacyFile in Directory.EnumerateFiles(LogDirectory, "history-*.jsonl"))
            {
                string destination = Path.Combine(HistoryDirectory, Path.GetFileName(legacyFile));
                if (File.Exists(destination))
                {
                    destination = Path.Combine(
                        HistoryDirectory,
                        $"history-legacy-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.jsonl");
                }

                File.Move(legacyFile, destination);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preserve the legacy file in place if migration cannot be completed.
        }
    }
}
