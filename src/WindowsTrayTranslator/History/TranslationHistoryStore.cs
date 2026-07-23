using System.Text;
using System.Text.Json;
using WindowsTrayTranslator.Logging;

namespace WindowsTrayTranslator.History;

public sealed class TranslationHistoryStore
{
    private const int MaximumLoadedEntries = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object sync = new();
    private readonly string directory;
    private readonly ILogger logger;
    private bool enabled;
    private int retentionDays;

    public TranslationHistoryStore(string directory, bool enabled, int retentionDays, ILogger logger)
    {
        this.directory = directory;
        this.enabled = enabled;
        this.retentionDays = Math.Clamp(retentionDays, 1, 365);
        this.logger = logger;
        Directory.CreateDirectory(directory);
        DeleteExpiredFiles();
    }

    public void Configure(bool isEnabled, int days)
    {
        lock (sync)
        {
            enabled = isEnabled;
            retentionDays = Math.Clamp(days, 1, 365);
            DeleteExpiredFiles();
        }
    }

    public void Append(TranslationHistoryEntry entry)
    {
        lock (sync)
        {
            if (!enabled)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"history-{entry.Timestamp:yyyy-MM-dd}.jsonl");
                string json = JsonSerializer.Serialize(entry, JsonOptions);
                File.AppendAllText(path, json + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.Error("번역 히스토리를 저장하지 못했습니다.", ex);
            }
        }
    }

    public IReadOnlyList<TranslationHistoryEntry> LoadRecent()
    {
        lock (sync)
        {
            DeleteExpiredFiles();
            List<TranslationHistoryEntry> entries = [];
            try
            {
                foreach (string file in Directory
                    .EnumerateFiles(directory, "history-*.jsonl")
                    .OrderByDescending(Path.GetFileName))
                {
                    foreach (string line in File.ReadLines(file, Encoding.UTF8).Reverse())
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        try
                        {
                            TranslationHistoryEntry? entry =
                                JsonSerializer.Deserialize<TranslationHistoryEntry>(line, JsonOptions);
                            if (entry is not null)
                            {
                                entries.Add(entry);
                            }
                        }
                        catch (JsonException)
                        {
                            // Skip a damaged entry while keeping the rest of the history available.
                        }

                        if (entries.Count >= MaximumLoadedEntries)
                        {
                            return entries;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.Error("번역 히스토리를 읽지 못했습니다.", ex);
            }

            return entries;
        }
    }

    private void DeleteExpiredFiles()
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            DateTime cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            foreach (string file in Directory.EnumerateFiles(directory, "history-*.jsonl"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // History cleanup must never prevent translation or application startup.
        }
    }
}
