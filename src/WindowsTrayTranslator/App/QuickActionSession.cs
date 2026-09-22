using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.App;

/// <summary>Captured before the palette is shown, kept in memory only, never written to disk.</summary>
internal sealed record CapturedSelection(string Text, WindowIdentity SourceWindow);

internal enum PaletteAdmission
{
    Started,
    Busy,
    TargetChanged,
    Stale
}

internal sealed class QuickActionSession(CapturedSelection selection)
{
    public CapturedSelection Selection { get; } = selection;

    public bool Completed { get; private set; }

    /// <summary>
    /// Raised synchronously so the palette closes inside the same UI callback that claimed the
    /// session, before the generation task reaches its first await.
    /// </summary>
    public event EventHandler? Completing;

    public bool TryComplete()
    {
        if (Completed)
        {
            return false;
        }

        Completed = true;
        Completing?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
