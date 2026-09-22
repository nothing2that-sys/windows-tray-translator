using System.Runtime.InteropServices;

namespace WindowsTrayTranslator.UI;

public sealed class HotkeyTextBox : TextBox
{
    public HotkeyTextBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        if (e.KeyCode is Keys.Back or Keys.Delete)
        {
            Text = string.Empty;
            return;
        }

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return;
        }

        string? key = FormatKey(e.KeyCode);
        List<string> parts = [];
        if (e.Control) parts.Add("Ctrl");
        if (e.Alt) parts.Add("Alt");
        if (e.Shift) parts.Add("Shift");
        if (IsWinPressed()) parts.Add("Win");
        if (key is null || parts.Count == 0)
        {
            return;
        }

        parts.Add(key);
        Text = string.Join('+', parts);
    }

    private static string? FormatKey(Keys key) => key switch
    {
        >= Keys.A and <= Keys.Z => key.ToString(),
        >= Keys.D0 and <= Keys.D9 => ((int)key - (int)Keys.D0).ToString(),
        >= Keys.F1 and <= Keys.F12 => key.ToString(),
        _ => null
    };

    private static bool IsWinPressed() =>
        (GetKeyState((int)Keys.LWin) & 0x8000) != 0 || (GetKeyState((int)Keys.RWin) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}
