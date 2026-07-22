using System.Collections.Specialized;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Clipboard;

public sealed class ClipboardService : IClipboardService
{
    private readonly int retryCount;
    private readonly int retryDelayMs;
    private readonly KeyboardInputService keyboard;
    private readonly ILogger logger;

    public ClipboardService(int retryCount, int retryDelayMs, KeyboardInputService keyboard, ILogger logger)
    {
        this.retryCount = retryCount;
        this.retryDelayMs = retryDelayMs;
        this.keyboard = keyboard;
        this.logger = logger;
    }

    public ClipboardSnapshot CaptureSnapshot()
    {
        EnsureStaThread();
        IDataObject? source = Retry(() => System.Windows.Forms.Clipboard.GetDataObject());
        if (source is null)
        {
            return new ClipboardSnapshot(new Dictionary<string, object>());
        }

        Dictionary<string, object> formats = new(StringComparer.Ordinal);
        List<string> unsupportedFormats = [];
        foreach (string format in source.GetFormats(autoConvert: false))
        {
            try
            {
                object? value = source.GetData(format, autoConvert: false);
                object? clone = CloneSupportedValue(value);
                if (clone is not null)
                {
                    formats[format] = clone;
                }
                else
                {
                    logger.Warning($"구체화할 수 없는 클립보드 형식을 건너뜁니다. Format={format}");
                    unsupportedFormats.Add(format);
                }
            }
            catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
            {
                logger.Warning($"클립보드 형식을 백업하지 못했습니다. Format={format}");
                unsupportedFormats.Add(format);
            }
        }

        if (unsupportedFormats.Count > 0)
        {
            foreach (IDisposable value in formats.Values.OfType<IDisposable>())
            {
                value.Dispose();
            }

            throw new InvalidOperationException("안전하게 백업할 수 없는 클립보드 형식이 있어 작업을 취소했습니다.");
        }

        return new ClipboardSnapshot(formats);
    }

    public void Restore(ClipboardSnapshot snapshot)
    {
        EnsureStaThread();
        if (snapshot.IsEmpty)
        {
            Retry(() =>
            {
                System.Windows.Forms.Clipboard.Clear();
                return true;
            });
            return;
        }

        DataObject restored = new();
        foreach ((string format, object value) in snapshot.Formats)
        {
            restored.SetData(format, autoConvert: false, value);
        }

        Retry(() =>
        {
            System.Windows.Forms.Clipboard.SetDataObject(restored, copy: true, retryCount, retryDelayMs);
            return true;
        });
    }

    public void Clear()
    {
        EnsureStaThread();
        Retry(() =>
        {
            System.Windows.Forms.Clipboard.Clear();
            return true;
        });
    }

    public bool ContainsText()
    {
        EnsureStaThread();
        return Retry(() => System.Windows.Forms.Clipboard.ContainsText(TextDataFormat.UnicodeText));
    }

    public string GetText()
    {
        EnsureStaThread();
        return Retry(() => System.Windows.Forms.Clipboard.GetText(TextDataFormat.UnicodeText));
    }

    public void SetText(string text)
    {
        EnsureStaThread();
        Retry(() =>
        {
            System.Windows.Forms.Clipboard.SetText(text, TextDataFormat.UnicodeText);
            return true;
        });
    }

    public uint GetSequenceNumber() => NativeClipboardMethods.GetClipboardSequenceNumber();

    public async Task PasteTextAsync(
        string text,
        ClipboardSnapshot snapshotToRestore,
        int restoreDelayMs,
        CancellationToken cancellationToken)
    {
        SetText(text);
        uint expectedSequence = GetSequenceNumber();
        try
        {
            if (!await keyboard.WaitForModifiersReleasedAsync(1500, cancellationToken))
            {
                throw new InvalidOperationException("단축키 modifier가 해제되지 않아 붙여넣기를 취소했습니다.");
            }
            keyboard.SendPaste();
            await Task.Delay(restoreDelayMs, cancellationToken);
        }
        finally
        {
            if (GetSequenceNumber() == expectedSequence)
            {
                Restore(snapshotToRestore);
            }
            else
            {
                logger.Warning("붙여넣기 대기 중 클립보드가 변경되어 기존 스냅샷 복원을 생략합니다.");
            }
        }
    }

    internal static object? CloneSupportedValue(object? value) => value switch
    {
        null => null,
        string text => text,
        string[] files => files.ToArray(),
        byte[] bytes => bytes.ToArray(),
        Bitmap bitmap => bitmap.Clone(),
        Image image => image.Clone(),
        MemoryStream stream => new MemoryStream(stream.ToArray(), writable: false),
        StringCollection collection => CloneStringCollection(collection),
        _ when value.GetType().IsValueType => value,
        _ => null
    };

    private static StringCollection CloneStringCollection(StringCollection source)
    {
        StringCollection clone = new();
        clone.AddRange(source.Cast<string>().ToArray());
        return clone;
    }

    private T Retry<T>(Func<T> action)
    {
        Exception? lastError = null;
        for (int attempt = 1; attempt <= retryCount; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
            {
                lastError = ex;
                if (attempt < retryCount)
                {
                    Thread.Sleep(retryDelayMs);
                }
            }
        }

        throw new InvalidOperationException("Windows 클립보드에 접근하지 못했습니다.", lastError);
    }

    private static void EnsureStaThread()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("클립보드 작업은 STA 스레드에서 실행해야 합니다.");
        }
    }

    private static class NativeClipboardMethods
    {
        [DllImport("user32.dll")]
        internal static extern uint GetClipboardSequenceNumber();
    }
}
