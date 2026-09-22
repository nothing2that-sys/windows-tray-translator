namespace WindowsTrayTranslator.Hotkeys;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private readonly Action<int> callback;

    public HotkeyWindow(Action<int> callback)
    {
        this.callback = callback;
        CreateHandle(new CreateParams
        {
            Caption = "WindowsTrayTranslator.HotkeyWindow",
            Parent = new IntPtr(-3)
        });
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey)
        {
            callback(m.WParam.ToInt32());
        }

        base.WndProc(ref m);
    }

    public void Dispose() => DestroyHandle();
}
