namespace WindowsTrayTranslator.Clipboard;

public interface IClipboardService
{
    ClipboardSnapshot CaptureSnapshot();
    void Restore(ClipboardSnapshot snapshot);
    void Clear() => throw new NotSupportedException();
    bool ContainsText() => throw new NotSupportedException();
    string GetText() => throw new NotSupportedException();
    uint GetSequenceNumber();
    Task<PasteTextResult> PasteTextAsync(
        string text,
        ClipboardSnapshot snapshotToRestore,
        int restoreDelayMs,
        Func<bool> isTargetStillActive,
        CancellationToken cancellationToken);

    async Task<PasteTextOutcome> PasteTextWithOutcomeAsync(
        string text,
        ClipboardSnapshot snapshotToRestore,
        int restoreDelayMs,
        Func<bool> isTargetStillActive,
        CancellationToken cancellationToken) =>
        new(await PasteTextAsync(
            text,
            snapshotToRestore,
            restoreDelayMs,
            isTargetStillActive,
            cancellationToken));
}

public enum PasteTextResult
{
    Success,
    TargetChanged,
    ClipboardChanged
}

public sealed record PasteTextOutcome(PasteTextResult Status, bool ClipboardRestored = true);
