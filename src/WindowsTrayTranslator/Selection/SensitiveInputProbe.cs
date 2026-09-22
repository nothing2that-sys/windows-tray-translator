using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using WindowsTrayTranslator.Logging;

namespace WindowsTrayTranslator.Selection;

internal enum SensitiveProbeResult
{
    Safe,
    Sensitive,
    Unknown
}

/// <summary>
/// Reads only the focused element's IsPassword state. The probe never reads selected text and
/// never reports an indeterminate answer as safe.
/// </summary>
internal sealed class SensitiveInputProbe
{
    internal const int SensitiveProbeTimeoutMs = 300;

    private readonly Func<SensitiveProbeResult> read;
    private readonly ILogger logger;
    private readonly int timeoutMs;
    private int inFlight;
    private int workerStartCount;

    public SensitiveInputProbe(ILogger logger)
        : this(ReadFocusedElementState, logger, SensitiveProbeTimeoutMs)
    {
    }

    internal SensitiveInputProbe(Func<SensitiveProbeResult> read, ILogger logger, int timeoutMs)
    {
        this.read = read;
        this.logger = logger;
        this.timeoutMs = timeoutMs;
    }

    internal int WorkerStartCount => Volatile.Read(ref workerStartCount);

    internal bool HasWorkerInFlight => Volatile.Read(ref inFlight) != 0;

    /// <summary>
    /// A timed out probe keeps its worker marked as in flight until the worker itself finishes, so a
    /// hung UI Automation call cannot be multiplied by repeated probe requests.
    /// </summary>
    public async Task<SensitiveProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        long startedTicks = Stopwatch.GetTimestamp();
        if (Interlocked.CompareExchange(ref inFlight, 1, 0) != 0)
        {
            Log(SensitiveProbeResult.Unknown, startedTicks);
            return SensitiveProbeResult.Unknown;
        }

        Interlocked.Increment(ref workerStartCount);
        Task<SensitiveProbeResult> worker = Task.Run(RunProbe, CancellationToken.None);

        try
        {
            SensitiveProbeResult result = await worker.WaitAsync(
                TimeSpan.FromMilliseconds(timeoutMs),
                cancellationToken);
            Log(result, startedTicks);
            return result;
        }
        catch (TimeoutException)
        {
            Log(SensitiveProbeResult.Unknown, startedTicks);
            return SensitiveProbeResult.Unknown;
        }
    }

    private SensitiveProbeResult RunProbe()
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            // The worker never faults so an abandoned probe cannot surface as an unobserved exception.
            return SensitiveProbeResult.Unknown;
        }
        finally
        {
            Volatile.Write(ref inFlight, 0);
        }
    }

    private void Log(SensitiveProbeResult result, long startedTicks) =>
        logger.Information(
            $"보호된 입력 여부를 확인했습니다. Result={result}, ElapsedMs={Stopwatch.GetElapsedTime(startedTicks).TotalMilliseconds:F0}");

    private static SensitiveProbeResult ReadFocusedElementState()
    {
        try
        {
            AutomationElement? focused = AutomationElement.FocusedElement;
            if (focused is null)
            {
                return SensitiveProbeResult.Unknown;
            }

            object? value = focused.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true);
            if (UiAutomationSelectedTextProvider.IsPasswordPropertyValue(value))
            {
                return SensitiveProbeResult.Sensitive;
            }

            // NotSupported and any other shape stays Unknown; only an explicit false is safe.
            return value is bool ? SensitiveProbeResult.Safe : SensitiveProbeResult.Unknown;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or InvalidCastException or COMException)
        {
            return SensitiveProbeResult.Unknown;
        }
    }
}
