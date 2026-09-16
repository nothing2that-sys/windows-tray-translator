using System.Drawing;
using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Configuration;
using WindowsTrayTranslator.Translation;
using WindowsTrayTranslator.UI;

namespace WindowsTrayTranslator.Tests;

/// <summary>
/// Converts the manual matrix rows that are pure functions into real tests: palette placement on
/// negative-coordinate and scaled monitors (P11/P12) and action-neutral messages (E3).
/// </summary>
public sealed class PalettePlacementAndMessageTests
{
    // Roughly the palette's own footprint; the second entry models a 200% DPI monitor.
    private static readonly Size[] PaletteSizes = [new Size(252, 168), new Size(504, 336)];

    private static readonly Rectangle[] WorkingAreas =
    [
        new Rectangle(0, 0, 1920, 1040),                 // primary
        new Rectangle(-1920, 0, 1920, 1040),             // left secondary, negative X
        new Rectangle(-1920, -1080, 1920, 1040),         // upper-left secondary, negative X and Y
        new Rectangle(1920, -300, 2560, 1400),           // right secondary, higher resolution
        new Rectangle(0, 0, 800, 600)                    // small monitor
    ];

    [Theory]
    [MemberData(nameof(PlacementCases))]
    public void Calculate_KeepsPaletteInsideWorkingArea(Rectangle workingArea, Size palette, Point cursor)
    {
        Point result = PopupPositionCalculator.Calculate(cursor, palette, workingArea);

        Assert.True(result.X >= workingArea.Left, $"left overflow: {result.X} < {workingArea.Left}");
        Assert.True(result.Y >= workingArea.Top, $"top overflow: {result.Y} < {workingArea.Top}");
        Assert.True(result.X + palette.Width <= workingArea.Right, $"right overflow: {result.X + palette.Width} > {workingArea.Right}");
        Assert.True(result.Y + palette.Height <= workingArea.Bottom, $"bottom overflow: {result.Y + palette.Height} > {workingArea.Bottom}");
    }

    public static TheoryData<Rectangle, Size, Point> PlacementCases
    {
        get
        {
            TheoryData<Rectangle, Size, Point> data = [];
            foreach (Rectangle area in WorkingAreas)
            {
                foreach (Size size in PaletteSizes)
                {
                    // Each corner, the centre, and one pixel outside each edge.
                    foreach (Point cursor in new[]
                    {
                        new Point(area.Left, area.Top),
                        new Point(area.Right - 1, area.Top),
                        new Point(area.Left, area.Bottom - 1),
                        new Point(area.Right - 1, area.Bottom - 1),
                        new Point(area.Left + area.Width / 2, area.Top + area.Height / 2),
                        new Point(area.Left - 1, area.Top - 1),
                        new Point(area.Right + 1, area.Bottom + 1)
                    })
                    {
                        data.Add(area, size, cursor);
                    }
                }
            }

            return data;
        }
    }

    [Fact]
    public void Calculate_WhenPaletteIsLargerThanMonitor_StillAnchorsInsideTopLeft()
    {
        Rectangle tiny = new(-1920, -1080, 300, 200);

        Point result = PopupPositionCalculator.Calculate(new Point(-1800, -1000), new Size(600, 400), tiny);

        Assert.Equal(tiny.Left, result.X);
        Assert.Equal(tiny.Top, result.Y);
    }

    [Theory]
    [InlineData(TranslationFailureKind.Network)]
    [InlineData(TranslationFailureKind.Server)]
    [InlineData(TranslationFailureKind.Timeout)]
    [InlineData(TranslationFailureKind.RateLimit)]
    [InlineData(TranslationFailureKind.Authentication)]
    [InlineData(TranslationFailureKind.InvalidRequest)]
    [InlineData(TranslationFailureKind.InvalidResponse)]
    [InlineData(TranslationFailureKind.EmptyResponse)]
    [InlineData(TranslationFailureKind.SafetyBlocked)]
    [InlineData(TranslationFailureKind.ModelUnavailable)]
    [InlineData(TranslationFailureKind.MissingApiKey)]
    public void ActionPopup_FailureMessages_AreNotTranslationSpecific(TranslationFailureKind kind)
    {
        AppSettings settings = new();
        string message = MessageFor(kind);

        (TranslationPopupKind popupKind, string shown, string? copyText) =
            ApplicationController.ResolveActionPopup(TranslationResult.Failure(kind, message), settings);

        Assert.Equal(TranslationPopupKind.Error, popupKind);
        Assert.Null(copyText);
        Assert.DoesNotContain("번역", shown);
    }

    [Fact]
    public void ActionPopup_DefaultFailureMessage_IsNotTranslationSpecific()
    {
        AppSettings settings = new();

        (_, string shown, _) = ApplicationController.ResolveActionPopup(
            new TranslationResult(false, null, TranslationFailureKind.Server, null),
            settings);

        Assert.DoesNotContain("번역", shown);
        Assert.False(string.IsNullOrWhiteSpace(shown));
    }

    /// <summary>
    /// Mirrors the strings the Gemini client and parser actually produce for each failure kind, so a
    /// future edit that reintroduces a translation-only word fails here.
    /// </summary>
    private static string MessageFor(TranslationFailureKind kind) => kind switch
    {
        TranslationFailureKind.Network => "네트워크 연결을 확인해 주세요.",
        TranslationFailureKind.Server => "서버에서 오류가 발생했습니다.",
        TranslationFailureKind.Timeout => "요청 시간이 초과되었습니다.",
        TranslationFailureKind.RateLimit => "API 호출 한도를 초과했습니다. 사용량과 요금제를 확인하거나 잠시 후 다시 시도해 주세요.",
        TranslationFailureKind.Authentication => "Gemini API 키가 올바르지 않습니다.",
        TranslationFailureKind.InvalidRequest => "요청을 처리하지 못했습니다.",
        TranslationFailureKind.InvalidResponse => "서버 응답을 해석하지 못했습니다.",
        TranslationFailureKind.EmptyResponse => "결과가 비어 있습니다.",
        TranslationFailureKind.SafetyBlocked => "안전 정책으로 인해 결과를 받을 수 없습니다.",
        TranslationFailureKind.ModelUnavailable => "설정한 Gemini 모델을 사용할 수 없습니다. 설정에서 모델 목록을 새로고침해 주세요.",
        TranslationFailureKind.MissingApiKey => "Gemini API 키를 설정해 주세요.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
