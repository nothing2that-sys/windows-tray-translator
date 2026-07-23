using WindowsTrayTranslator.History;

namespace WindowsTrayTranslator.Tests;

public sealed class TranslationHistoryStoreTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), $"WindowsTrayTranslator.History.Tests.{Guid.NewGuid():N}");

    [Fact]
    public void AppendAndLoadRecent_ReturnsNewestEntryFirst()
    {
        TranslationHistoryStore store = new(directory, enabled: true, retentionDays: 14, new TestLogger());
        TranslationHistoryEntry older = new(
            new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero),
            TranslationHistoryKind.Read,
            "Korean",
            "Hello",
            "안녕하세요");
        TranslationHistoryEntry newer = new(
            new DateTimeOffset(2026, 7, 22, 11, 0, 0, TimeSpan.Zero),
            TranslationHistoryKind.Replace,
            "English",
            "감사합니다",
            "Thank you");

        store.Append(older);
        store.Append(newer);

        IReadOnlyList<TranslationHistoryEntry> loaded = store.LoadRecent();
        Assert.Equal([newer, older], loaded);
    }

    [Fact]
    public void Append_WhenDisabled_DoesNotStoreText()
    {
        TranslationHistoryStore store = new(directory, enabled: false, retentionDays: 14, new TestLogger());
        store.Append(new TranslationHistoryEntry(
            DateTimeOffset.Now,
            TranslationHistoryKind.Read,
            "Korean",
            "secret",
            "비밀"));

        Assert.Empty(store.LoadRecent());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
