using System.Collections.Specialized;
using System.Drawing;
using System.Runtime.InteropServices;
using ComTypes = System.Runtime.InteropServices.ComTypes;
using WindowsTrayTranslator.Logging;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Clipboard;

public class ClipboardService : IClipboardService
{
    private int retryCount;
    private int retryDelayMs;
    private readonly IKeyboardInputService keyboard;
    private readonly ILogger logger;

    public ClipboardService(int retryCount, int retryDelayMs, IKeyboardInputService keyboard, ILogger logger)
    {
        this.retryCount = retryCount;
        this.retryDelayMs = retryDelayMs;
        this.keyboard = keyboard;
        this.logger = logger;
    }

    public ClipboardService(int retryCount, int retryDelayMs, KeyboardInputService keyboard, ILogger logger)
        : this(retryCount, retryDelayMs, (IKeyboardInputService)keyboard, logger)
    {
    }

    public void Configure(int newRetryCount, int newRetryDelayMs)
    {
        retryCount = Math.Clamp(newRetryCount, 1, 20);
        retryDelayMs = Math.Clamp(newRetryDelayMs, 10, 1000);
    }

    public virtual ClipboardSnapshot CaptureSnapshot()
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

    public virtual void Restore(ClipboardSnapshot snapshot)
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

    public virtual void Clear()
    {
        EnsureStaThread();
        Retry(() =>
        {
            System.Windows.Forms.Clipboard.Clear();
            return true;
        });
    }

    public virtual bool ContainsText()
    {
        EnsureStaThread();
        return Retry(() => System.Windows.Forms.Clipboard.ContainsText(TextDataFormat.UnicodeText));
    }

    public virtual string GetText()
    {
        EnsureStaThread();
        return Retry(() => System.Windows.Forms.Clipboard.GetText(TextDataFormat.UnicodeText));
    }

    public virtual void SetText(string text)
    {
        EnsureStaThread();
        Retry(() =>
        {
            System.Windows.Forms.Clipboard.SetText(text, TextDataFormat.UnicodeText);
            return true;
        });
    }

    public virtual uint GetSequenceNumber() => NativeClipboardMethods.GetClipboardSequenceNumber();

    public async Task<PasteTextResult> PasteTextAsync(
        string text,
        ClipboardSnapshot snapshotToRestore,
        int restoreDelayMs,
        Func<bool> isTargetStillActive,
        CancellationToken cancellationToken) =>
        (await PasteTextWithOutcomeAsync(
            text,
            snapshotToRestore,
            restoreDelayMs,
            isTargetStillActive,
            cancellationToken)).Status;

    public async Task<PasteTextOutcome> PasteTextWithOutcomeAsync(
        string text,
        ClipboardSnapshot snapshotToRestore,
        int restoreDelayMs,
        Func<bool> isTargetStillActive,
        CancellationToken cancellationToken)
    {
        if (!await keyboard.WaitForModifiersReleasedAsync(1500, cancellationToken))
        {
            throw new InvalidOperationException("단축키 modifier가 해제되지 않아 붙여넣기를 취소했습니다.");
        }

        if (!isTargetStillActive())
        {
            return new PasteTextOutcome(PasteTextResult.TargetChanged);
        }

        uint expectedSequence = 0;
        bool clipboardWriteAttempted = false;
        PasteTextResult status = PasteTextResult.Success;
        bool restoreOk = true;
        try
        {
            clipboardWriteAttempted = true;
            SetText(text);
            expectedSequence = GetSequenceNumber();
            if (!isTargetStillActive())
            {
                status = PasteTextResult.TargetChanged;
            }
            else if (GetSequenceNumber() != expectedSequence)
            {
                logger.Warning("붙여넣기 전에 클립보드가 외부에서 변경되어 자동 치환을 취소합니다.");
                status = PasteTextResult.ClipboardChanged;
            }
            else
            {
                keyboard.SendPaste();
                await Task.Delay(restoreDelayMs, cancellationToken);
            }
        }
        finally
        {
            if ((expectedSequence == 0 && clipboardWriteAttempted) ||
                ClipboardRestoreGuard.ShouldRestore(expectedSequence, GetSequenceNumber()))
            {
                try
                {
                    Restore(snapshotToRestore);
                }
                catch (Exception ex)
                {
                    restoreOk = false;
                    logger.Error("붙여넣기 후 기존 클립보드를 복원하지 못했습니다.", ex);
                }
            }
            else if (expectedSequence != 0)
            {
                logger.Warning("붙여넣기 대기 중 클립보드가 변경되어 기존 스냅샷 복원을 생략합니다.");
            }
        }

        return new PasteTextOutcome(status, restoreOk);
    }

    internal static class ClipboardRestoreGuard
    {
        public static bool ShouldRestore(uint expectedSequence, uint currentSequence) =>
            expectedSequence != 0 && expectedSequence == currentSequence;
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
        ComTypes.IStream stream => CloneComStream(stream),
        _ when value.GetType().IsValueType => value,
        _ => null
    };

    private static StringCollection CloneStringCollection(StringCollection source)
    {
        StringCollection clone = new();
        clone.AddRange(source.Cast<string>().ToArray());
        return clone;
    }

    private static MemoryStream CloneComStream(ComTypes.IStream source)
    {
        nint positionPointer = Marshal.AllocCoTaskMem(sizeof(long));
        nint bytesReadPointer = Marshal.AllocCoTaskMem(sizeof(int));
        try
        {
            source.Seek(0, 1, positionPointer);
            long originalPosition = Marshal.ReadInt64(positionPointer);
            try
            {
                source.Seek(0, 0, nint.Zero);
                MemoryStream clone = new();
                byte[] buffer = new byte[81920];
                while (true)
                {
                    Marshal.WriteInt32(bytesReadPointer, 0);
                    source.Read(buffer, buffer.Length, bytesReadPointer);
                    int bytesRead = Marshal.ReadInt32(bytesReadPointer);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    clone.Write(buffer, 0, bytesRead);
                }

                clone.Position = 0;
                return clone;
            }
            finally
            {
                source.Seek(originalPosition, 0, nint.Zero);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(bytesReadPointer);
            Marshal.FreeCoTaskMem(positionPointer);
        }
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
