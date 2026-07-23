using WindowsTrayTranslator.Translation.Gemini;

namespace WindowsTrayTranslator.Tests;

public sealed class TranslationLayoutPreserverTests
{
    [Fact]
    public void Prepare_SingleLine_DoesNotAddMarkers()
    {
        TranslationLayoutPreserver layout = TranslationLayoutPreserver.Prepare("Hello world");

        Assert.False(layout.UsesMarkers);
        Assert.Equal("Hello world", layout.RequestText);
        Assert.Equal("안녕하세요", layout.Restore("안녕하세요", out bool restored));
        Assert.False(restored);
    }

    [Fact]
    public void Restore_MarkedTranslation_PreservesOriginalBreaksBlankLinesAndIndentation()
    {
        TranslationLayoutPreserver layout = TranslationLayoutPreserver.Prepare("First line\r\n\r\n  Third line\nLast line");
        string translated = """
            ⟦WTT-LINE-000001⟧ 첫 번째 줄
            ⟦WTT-LINE-000002⟧
            ⟦WTT-LINE-000003⟧ 세 번째 줄
            ⟦WTT-LINE-000004⟧ 마지막 줄
            """;

        string result = layout.Restore(translated, out bool restored);

        Assert.True(restored);
        Assert.Equal("첫 번째 줄\r\n\r\n  세 번째 줄\n마지막 줄", result);
    }

    [Fact]
    public void Restore_MissingMarker_ReturnsOriginalModelResponseAsFallback()
    {
        TranslationLayoutPreserver layout = TranslationLayoutPreserver.Prepare("First\nSecond");

        string result = layout.Restore("첫 번째 두 번째", out bool restored);

        Assert.False(restored);
        Assert.Equal("첫 번째 두 번째", result);
    }

    [Fact]
    public void Restore_WrongMarkerCount_RemovesInternalMarkersFromFallback()
    {
        TranslationLayoutPreserver layout = TranslationLayoutPreserver.Prepare("First\nSecond");

        string result = layout.Restore(
            "⟦WTT-LINE-000001⟧ 첫 번째\n⟦WTT-LINE-000002⟧ 두 번째\n⟦WTT-LINE-000003⟧ 추가",
            out bool restored);

        Assert.False(restored);
        Assert.DoesNotContain("WTT-LINE", result);
        Assert.Equal("첫 번째\n두 번째\n추가", result);
    }

    [Fact]
    public void Restore_WrongMarkerOrder_RemovesInternalMarkersFromFallback()
    {
        TranslationLayoutPreserver layout = TranslationLayoutPreserver.Prepare("First\nSecond");

        string result = layout.Restore(
            "⟦WTT-LINE-000002⟧ 두 번째\n⟦WTT-LINE-000001⟧ 첫 번째",
            out bool restored);

        Assert.False(restored);
        Assert.DoesNotContain("WTT-LINE", result);
    }
}
