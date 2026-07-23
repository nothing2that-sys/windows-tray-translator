namespace WindowsTrayTranslator.Clipboard;

public interface IClipboardService
{
    ClipboardSnapshot CaptureSnapshot();
    void Restore(ClipboardSnapshot snapshot);
    uint GetSequenceNumber();
    Task<PasteTextResult> PasteTextAsync(
        string text,
        ClipboardSnapshot snapshotToRestore,
        int restoreDelayMs,
        Func<bool> isTargetStillActive,
        CancellationToken cancellationToken);
}

public enum PasteTextResult
{
    Success,
    TargetChanged,
    ClipboardChanged
}
