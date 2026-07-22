namespace WindowsTrayTranslator.Clipboard;

public interface IClipboardService
{
    ClipboardSnapshot CaptureSnapshot();
    void Restore(ClipboardSnapshot snapshot);
    uint GetSequenceNumber();
    Task PasteTextAsync(
        string text,
        ClipboardSnapshot snapshotToRestore,
        int restoreDelayMs,
        CancellationToken cancellationToken);
}
