using WindowsTrayTranslator.App;

namespace WindowsTrayTranslator.Tests;

public sealed class AppPathsTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"WindowsTrayTranslator.AppPaths.Tests.{Guid.NewGuid():N}");

    [Fact]
    public void EnsureDirectories_MigratesLegacyHistoryOutOfDiagnosticLogs()
    {
        string logs = Path.Combine(root, "logs");
        string history = Path.Combine(root, "history");
        Directory.CreateDirectory(logs);
        string legacy = Path.Combine(logs, "history-2026-09-14.jsonl");
        File.WriteAllText(legacy, "legacy-entry");
        AppPaths paths = new(
            root,
            Path.Combine(root, "config"),
            Path.Combine(root, "config", "appsettings.json"),
            Path.Combine(root, "secrets.dat"),
            logs,
            history);

        paths.EnsureDirectories();

        Assert.False(File.Exists(legacy));
        Assert.Equal("legacy-entry", File.ReadAllText(Path.Combine(history, "history-2026-09-14.jsonl")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
