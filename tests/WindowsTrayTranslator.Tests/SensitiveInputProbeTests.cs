using WindowsTrayTranslator.Clipboard;
using WindowsTrayTranslator.Selection;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Tests;

public sealed class SensitiveInputProbeTests
{
    private static readonly WindowIdentity Window = new(new IntPtr(1), 2, "test", new IntPtr(3));

    [Fact]
    public async Task Capture_Probe1Sensitive_MakesNoClipboardOrCopyCallAndBlocksFallback()
    {
        CountingClipboard clipboard = new();
        CountingKeyboard keyboard = new(() => clipboard.Sequence++);
        using ClipboardMutationCoordinator coordinator = new();
        ClipboardSelectedTextProvider capture = new(
            clipboard,
            coordinator,
            ScriptedProbe(SensitiveProbeResult.Sensitive),
            keyboard,
            new StableWindow(),
            new TestLogger());
        CountingProvider fallback = new(SelectedTextResult.Success("fallback", Window));
        CompositeSelectedTextProvider composite = new(capture, fallback, new TestLogger());

        SelectedTextResult result = await composite.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.CanFallback);
        Assert.Equal(SelectionMessages.SensitiveInput, result.ErrorMessage);
        Assert.Equal(0, clipboard.SnapshotCount);
        Assert.Equal(0, clipboard.ClearCount);
        Assert.Equal(0, clipboard.RestoreCount);
        Assert.Equal(0, keyboard.CopyCount);
        Assert.Equal(0, fallback.CallCount);
        Assert.True(result.ClipboardRestored);
    }

    [Fact]
    public async Task Capture_Probe1Safe_KeepsExistingCaptureFlow()
    {
        CountingClipboard clipboard = new() { Text = "selected" };
        CountingKeyboard keyboard = new(() => clipboard.Sequence++);
        using ClipboardMutationCoordinator coordinator = new();
        ClipboardSelectedTextProvider capture = new(
            clipboard,
            coordinator,
            ScriptedProbe(SensitiveProbeResult.Safe),
            keyboard,
            new StableWindow(),
            new TestLogger());

        SelectedTextResult result = await capture.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("selected", result.Text);
        Assert.Equal(1, clipboard.SnapshotCount);
        Assert.Equal(1, clipboard.ClearCount);
        Assert.Equal(1, clipboard.RestoreCount);
        Assert.Equal(1, keyboard.CopyCount);
    }

    [Fact]
    public async Task Capture_Probe1Unknown_ProceedsForCompatibility()
    {
        CountingClipboard clipboard = new() { Text = "selected" };
        CountingKeyboard keyboard = new(() => clipboard.Sequence++);
        using ClipboardMutationCoordinator coordinator = new();
        TestLogger logger = new();
        ClipboardSelectedTextProvider capture = new(
            clipboard,
            coordinator,
            ScriptedProbe(SensitiveProbeResult.Unknown),
            keyboard,
            new StableWindow(),
            logger);

        SelectedTextResult result = await capture.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, keyboard.CopyCount);
        Assert.DoesNotContain(logger.Entries, message => message.Contains("Result=Safe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Capture_Probe2SensitiveOnSecondAttempt_SkipsCopyAndRestoresSnapshot()
    {
        // The clipboard sequence never moves, so attempt 1 times out and attempt 2 is reached.
        CountingClipboard clipboard = new();
        CountingKeyboard keyboard = new(() => { });
        using ClipboardMutationCoordinator coordinator = new();
        ClipboardSelectedTextProvider capture = new(
            clipboard,
            coordinator,
            ScriptedProbe(
                SensitiveProbeResult.Safe,
                SensitiveProbeResult.Safe,
                SensitiveProbeResult.Sensitive),
            keyboard,
            new StableWindow(),
            new TestLogger());

        SelectedTextResult result = await capture.GetSelectedTextAsync(Window, 50, 1000, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.CanFallback);
        Assert.Equal(SelectionMessages.SensitiveInput, result.ErrorMessage);
        Assert.Equal(1, keyboard.CopyCount);
        Assert.Equal(1, clipboard.SnapshotCount);
        Assert.Equal(1, clipboard.RestoreCount);
        Assert.True(result.ClipboardRestored);
    }

    [Fact]
    public async Task UiAutomation_PasswordField_ReturnsProtectedFailureWithoutClipboardFallback()
    {
        UiAutomationSelectedTextProvider uiAutomation = new(
            new StableWindow(),
            new TestLogger(),
            () => (true, (string?)null));
        CountingProvider clipboardFallback = new(SelectedTextResult.Success("clipboard", Window));
        CompositeSelectedTextProvider composite = new(uiAutomation, clipboardFallback, new TestLogger());

        SelectedTextResult result = await composite.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.CanFallback);
        Assert.Equal(SelectionMessages.SensitiveInput, result.ErrorMessage);
        Assert.Equal(0, clipboardFallback.CallCount);
    }

    [Fact]
    public async Task UiAutomation_EmptySelectionWithoutPassword_StillFallsBack()
    {
        UiAutomationSelectedTextProvider uiAutomation = new(
            new StableWindow(),
            new TestLogger(),
            () => (false, (string?)null));
        CountingProvider clipboardFallback = new(SelectedTextResult.Success("clipboard", Window));
        CompositeSelectedTextProvider composite = new(uiAutomation, clipboardFallback, new TestLogger());

        SelectedTextResult result = await composite.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("clipboard", result.Text);
        Assert.Equal(1, clipboardFallback.CallCount);
    }

    [Fact]
    public async Task Probe_Timeout_ReturnsUnknownAndDoesNotStartAnotherWorker()
    {
        using ManualResetEventSlim release = new(false);
        SensitiveInputProbe probe = new(
            () =>
            {
                release.Wait(TimeSpan.FromSeconds(5));
                return SensitiveProbeResult.Safe;
            },
            new TestLogger(),
            20);

        Assert.Equal(SensitiveProbeResult.Unknown, await probe.ProbeAsync(CancellationToken.None));
        Assert.Equal(1, probe.WorkerStartCount);
        Assert.True(probe.HasWorkerInFlight);

        Assert.Equal(SensitiveProbeResult.Unknown, await probe.ProbeAsync(CancellationToken.None));
        Assert.Equal(1, probe.WorkerStartCount);

        release.Set();
        Assert.True(await WaitForAsync(() => !probe.HasWorkerInFlight));
        Assert.Equal(SensitiveProbeResult.Safe, await probe.ProbeAsync(CancellationToken.None));
        Assert.Equal(2, probe.WorkerStartCount);
    }

    [Fact]
    public async Task Probe_ConcurrentRequests_KeepAtMostOneWorker()
    {
        using ManualResetEventSlim release = new(false);
        int concurrent = 0;
        int peakConcurrent = 0;
        SensitiveInputProbe probe = new(
            () =>
            {
                int running = Interlocked.Increment(ref concurrent);
                InterlockedMax(ref peakConcurrent, running);
                release.Wait(TimeSpan.FromSeconds(5));
                Interlocked.Decrement(ref concurrent);
                return SensitiveProbeResult.Safe;
            },
            new TestLogger(),
            20);

        SensitiveProbeResult[] results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => probe.ProbeAsync(CancellationToken.None)));

        release.Set();
        Assert.True(await WaitForAsync(() => !probe.HasWorkerInFlight));
        Assert.Equal(1, probe.WorkerStartCount);
        Assert.True(Volatile.Read(ref peakConcurrent) <= 1);
        Assert.All(results, result => Assert.Equal(SensitiveProbeResult.Unknown, result));
    }

    [Fact]
    public async Task Probe_Cancellation_PropagatesAndClearsInFlightWhenWorkerEnds()
    {
        using ManualResetEventSlim release = new(false);
        SensitiveInputProbe probe = new(
            () =>
            {
                release.Wait(TimeSpan.FromSeconds(5));
                return SensitiveProbeResult.Safe;
            },
            new TestLogger(),
            5000);
        using CancellationTokenSource cancellation = new();

        Task<SensitiveProbeResult> pending = probe.ProbeAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(probe.HasWorkerInFlight);

        release.Set();
        Assert.True(await WaitForAsync(() => !probe.HasWorkerInFlight));
    }

    [Fact]
    public async Task Probe_ReaderThrows_ReturnsUnknownWithoutFaultingWorker()
    {
        SensitiveInputProbe probe = new(
            () => throw new InvalidOperationException("uia unavailable"),
            new TestLogger(),
            500);

        Assert.Equal(SensitiveProbeResult.Unknown, await probe.ProbeAsync(CancellationToken.None));
        Assert.True(await WaitForAsync(() => !probe.HasWorkerInFlight));
    }

    [Fact]
    public async Task Probe_LogsOnlyResultAndElapsed()
    {
        TestLogger logger = new();
        SensitiveInputProbe probe = new(() => SensitiveProbeResult.Sensitive, logger, 500);

        await probe.ProbeAsync(CancellationToken.None);

        string entry = Assert.Single(logger.Entries, message => message.Contains("Result=", StringComparison.Ordinal));
        Assert.Contains("Result=Sensitive", entry, StringComparison.Ordinal);
        Assert.Contains("ElapsedMs=", entry, StringComparison.Ordinal);
    }

    private static SensitiveInputProbe ScriptedProbe(params SensitiveProbeResult[] results)
    {
        int index = -1;
        return new SensitiveInputProbe(
            () => results[Math.Min(Interlocked.Increment(ref index), results.Length - 1)],
            new TestLogger(),
            500);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current = Volatile.Read(ref target);
        while (value > current)
        {
            int previous = Interlocked.CompareExchange(ref target, value, current);
            if (previous == current)
            {
                return;
            }

            current = previous;
        }
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return condition();
    }

    private sealed class CountingClipboard : IClipboardService
    {
        public uint Sequence { get; set; } = 1;
        public string Text { get; set; } = string.Empty;
        public int SnapshotCount { get; private set; }
        public int ClearCount { get; private set; }
        public int RestoreCount { get; private set; }

        public ClipboardSnapshot CaptureSnapshot()
        {
            SnapshotCount++;
            return new ClipboardSnapshot(new Dictionary<string, object>());
        }

        public void Restore(ClipboardSnapshot snapshot) => RestoreCount++;
        public void Clear() => ClearCount++;
        public bool ContainsText() => !string.IsNullOrEmpty(Text);
        public string GetText() => Text;
        public uint GetSequenceNumber() => Sequence;

        public Task<PasteTextResult> PasteTextAsync(
            string text,
            ClipboardSnapshot snapshotToRestore,
            int restoreDelayMs,
            Func<bool> isTargetStillActive,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CountingKeyboard(Action onCopy) : IKeyboardInputService
    {
        public int CopyCount { get; private set; }

        public Task<bool> WaitForModifiersReleasedAsync(int timeoutMs, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public void SendCopy()
        {
            CopyCount++;
            onCopy();
        }

        public void SendPaste()
        {
        }
    }

    private sealed class CountingProvider(SelectedTextResult result) : ISelectedTextProvider
    {
        public int CallCount { get; private set; }

        public Task<SelectedTextResult> GetSelectedTextAsync(
            WindowIdentity sourceWindow,
            int timeoutMs,
            int maxCharacters,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class StableWindow : IActiveWindowService
    {
        public WindowIdentity Capture() => Window;
        public bool IsStillActive(WindowIdentity expected) => true;
    }
}
