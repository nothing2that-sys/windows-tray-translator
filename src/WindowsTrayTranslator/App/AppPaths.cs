namespace WindowsTrayTranslator.App;

public sealed record AppPaths(
    string RootDirectory,
    string ConfigurationDirectory,
    string SettingsFile,
    string SecretFile,
    string LogDirectory)
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
            Path.Combine(root, "logs"));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ConfigurationDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
