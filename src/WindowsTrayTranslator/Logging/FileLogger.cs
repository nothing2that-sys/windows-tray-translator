using System.Text;

namespace WindowsTrayTranslator.Logging;

public sealed class FileLogger : ILogger, IDisposable
{
    private readonly object sync = new();
    private readonly string logDirectory;
    private bool enabled;
    private int retentionDays;

    public FileLogger(string logDirectory, bool enabled, int retentionDays)
    {
        this.logDirectory = logDirectory;
        this.enabled = enabled;
        this.retentionDays = retentionDays;
        Directory.CreateDirectory(logDirectory);
        DeleteExpiredLogs();
    }

    public void Configure(bool isEnabled, int days)
    {
        enabled = isEnabled;
        retentionDays = Math.Clamp(days, 1, 365);
        DeleteExpiredLogs();
    }

    public void Information(string message) => Write("Information", message, null);
    public void Warning(string message) => Write("Warning", message, null);
    public void Error(string message, Exception? exception = null) => Write("Error", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        if (!enabled)
        {
            return;
        }

        string sanitized = Sanitize(message);
        string line = $"{DateTimeOffset.Now:O} [{level}] {sanitized}";
        if (exception is not null)
        {
            line += $" | {exception.GetType().Name}: {Sanitize(exception.Message)}";
        }

        lock (sync)
        {
            Directory.CreateDirectory(logDirectory);
            string path = Path.Combine(logDirectory, $"translator-{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    internal static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string sanitized = System.Text.RegularExpressions.Regex.Replace(
            value,
            "(?i)(x-goog-api-key|authorization)\\s*[:=]\\s*[^\\s,;]+",
            "$1=[REDACTED]");
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            "(?i)([?&]key=)[^&\\s]+",
            "$1[REDACTED]");
        return sanitized;
    }

    private void DeleteExpiredLogs()
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                return;
            }

            DateTime cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            foreach (string file in Directory.EnumerateFiles(logDirectory, "translator-*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Logging cleanup must never prevent application startup.
        }
    }

    public void Dispose()
    {
    }
}
