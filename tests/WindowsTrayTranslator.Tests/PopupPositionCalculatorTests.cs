using System.Drawing;
using WindowsTrayTranslator.UI;

namespace WindowsTrayTranslator.Tests;

public sealed class PopupPositionCalculatorTests
{
    [Fact]
    public void Calculate_NormalPosition_UsesLowerRightOffset()
    {
        Point result = PopupPositionCalculator.Calculate(
            new Point(100, 100),
            new Size(200, 100),
            new Rectangle(0, 0, 1920, 1080));

        Assert.Equal(new Point(116, 116), result);
    }

    [Fact]
    public void Calculate_NearBottomRight_KeepsPopupInsideWorkingArea()
    {
        Rectangle workingArea = new(-1920, 0, 1920, 1040);

        Point result = PopupPositionCalculator.Calculate(
            new Point(-10, 1030),
            new Size(400, 200),
            workingArea);

        Assert.True(result.X >= workingArea.Left);
        Assert.True(result.Y >= workingArea.Top);
        Assert.True(result.X + 400 <= workingArea.Right);
        Assert.True(result.Y + 200 <= workingArea.Bottom);
    }
}
