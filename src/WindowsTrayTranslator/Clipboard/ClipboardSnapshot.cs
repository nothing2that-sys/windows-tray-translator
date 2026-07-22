namespace WindowsTrayTranslator.Clipboard;

public sealed class ClipboardSnapshot : IDisposable
{
    public ClipboardSnapshot(IReadOnlyDictionary<string, object> formats)
    {
        Formats = formats;
    }

    public IReadOnlyDictionary<string, object> Formats { get; }
    public bool IsEmpty => Formats.Count == 0;

    public void Dispose()
    {
        foreach (object value in Formats.Values)
        {
            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
