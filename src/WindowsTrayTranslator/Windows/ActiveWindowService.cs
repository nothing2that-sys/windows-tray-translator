using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsTrayTranslator.Windows;

public interface IActiveWindowService
{
    WindowIdentity Capture();
    bool IsStillActive(WindowIdentity expected);
}

public sealed class ActiveWindowService : IActiveWindowService
{
    public WindowIdentity Capture()
    {
        IntPtr foreground = GetForegroundWindow();
        uint threadId = GetWindowThreadProcessId(foreground, out uint processId);
        string processName = string.Empty;
        try
        {
            processName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch (Exception) when (processId != 0)
        {
            // Process may have exited between the native calls.
        }

        GuiThreadInfo info = new() { Size = Marshal.SizeOf<GuiThreadInfo>() };
        IntPtr focused = GetGUIThreadInfo(threadId, ref info) ? info.Focus : IntPtr.Zero;
        return new WindowIdentity(foreground, processId, processName, focused);
    }

    public bool IsStillActive(WindowIdentity expected)
    {
        WindowIdentity current = Capture();
        return current.ForegroundWindow == expected.ForegroundWindow &&
               current.ProcessId == expected.ProcessId &&
               current.FocusedWindow == expected.FocusedWindow;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr Active;
        public IntPtr Focus;
        public IntPtr Capture;
        public IntPtr MenuOwner;
        public IntPtr MoveSize;
        public IntPtr Caret;
        public Rect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
