namespace WindowsTrayTranslator.Windows;

public sealed record WindowIdentity(
    IntPtr ForegroundWindow,
    uint ProcessId,
    string ProcessName,
    IntPtr FocusedWindow);
