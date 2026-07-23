namespace WindowsTrayTranslator.UI;

public static class PopupPositionCalculator
{
    public static Point Calculate(Point cursor, Size popup, Rectangle workingArea, int offset = 16)
    {
        int x = cursor.X + offset;
        int y = cursor.Y + offset;

        if (x + popup.Width > workingArea.Right)
        {
            x = cursor.X - popup.Width - offset;
        }

        if (y + popup.Height > workingArea.Bottom)
        {
            y = cursor.Y - popup.Height - offset;
        }

        x = Math.Clamp(x, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - popup.Width));
        y = Math.Clamp(y, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - popup.Height));
        return new Point(x, y);
    }
}
