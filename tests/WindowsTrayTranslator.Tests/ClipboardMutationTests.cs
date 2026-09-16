using WindowsTrayTranslator.Clipboard;
using WindowsTrayTranslator.Selection;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Tests;

public sealed class ClipboardMutationTests
{
    private static readonly WindowIdentity Window = new(new IntPtr(1), 2, "test", new IntPtr(3));

    [Fact]
    public async Task Coordinator_DoesNotInterleaveCriticalSections()
    {
        using ClipboardMutationCoordinator coordinator = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool secondEntered = false;

        Task first = coordinator.RunAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
            return true;
        }, CancellationToken.None);
        await entered.Task;
        Task second = coordinator.RunAsync(async () =>
        {
            secondEntered = true;
            await Task.Yield();
            return true;
        }, CancellationToken.None);

        await Task.Delay(25);
        Assert.False(secondEntered);
        release.SetResult();
        await Task.WhenAll(first, second);
        Assert.True(secondEntered);
    }

    [Fact]
    public async Task Capture_SuccessWithRestoreFailure_KeepsSuccessAndReportsWarningState()
    {
        FakeClipboard clipboard = new() { Text = "selected", ThrowOnRestore = true };
        FakeKeyboard keyboard = new(() => clipboard.Sequence++);
        using ClipboardMutationCoordinator coordinator = new();
        ClipboardSelectedTextProvider provider = new(
            clipboard, coordinator, SafeProbe(), keyboard, new FakeActiveWindow(), new TestLogger());

        SelectedTextResult result = await provider.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("selected", result.Text);
        Assert.False(result.ClipboardRestored);
        Assert.Equal(1, clipboard.RestoreCount);
    }

    [Fact]
    public async Task Capture_SuccessWithRestoreSuccess_ReportsRestored()
    {
        FakeClipboard clipboard = new() { Text = "selected" };
        FakeKeyboard keyboard = new(() => clipboard.Sequence++);
        using ClipboardMutationCoordinator coordinator = new();
        ClipboardSelectedTextProvider provider = new(
            clipboard, coordinator, SafeProbe(), keyboard, new FakeActiveWindow(), new TestLogger());

        SelectedTextResult result = await provider.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.ClipboardRestored);
    }

    [Fact]
    public async Task Capture_Cancellation_RestoresBeforePropagating()
    {
        FakeClipboard clipboard = new();
        FakeKeyboard keyboard = new(() => { });
        using ClipboardMutationCoordinator coordinator = new();
        ClipboardSelectedTextProvider provider = new(
            clipboard, coordinator, SafeProbe(), keyboard, new FakeActiveWindow(), new TestLogger());
        using CancellationTokenSource cancellation = new(30);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetSelectedTextAsync(Window, 1000, 1000, cancellation.Token));

        Assert.Equal(1, clipboard.RestoreCount);
    }

    [Fact]
    public async Task Paste_SuccessWithRestoreFailure_RemainsSuccessful()
    {
        FakeKeyboard keyboard = new(() => { });
        TestClipboardService clipboard = new(keyboard) { ThrowOnRestore = true };
        using ClipboardSnapshot snapshot = new(new Dictionary<string, object>());

        PasteTextOutcome result = await clipboard.PasteTextWithOutcomeAsync(
            "replacement", snapshot, 1, () => true, CancellationToken.None);

        Assert.Equal(PasteTextResult.Success, result.Status);
        Assert.False(result.ClipboardRestored);
        Assert.Equal(1, keyboard.PasteCount);
    }

    [Fact]
    public async Task Paste_Cancellation_RestoresBeforePropagating()
    {
        FakeKeyboard keyboard = new(() => { });
        TestClipboardService clipboard = new(keyboard);
        using ClipboardSnapshot snapshot = new(new Dictionary<string, object>());
        using CancellationTokenSource cancellation = new(25);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clipboard.PasteTextWithOutcomeAsync(
            "replacement", snapshot, 1000, () => true, cancellation.Token));

        Assert.Equal(1, clipboard.RestoreCount);
    }

    [Fact]
    public async Task Paste_SetFailure_RestoresSnapshotAndPropagatesFailure()
    {
        FakeKeyboard keyboard = new(() => { });
        TestClipboardService clipboard = new(keyboard) { ThrowOnSetText = true };
        using ClipboardSnapshot snapshot = new(new Dictionary<string, object>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => clipboard.PasteTextWithOutcomeAsync(
            "replacement", snapshot, 1, () => true, CancellationToken.None));

        Assert.Equal(1, clipboard.RestoreCount);
        Assert.Equal(0, keyboard.PasteCount);
    }

    private static SensitiveInputProbe SafeProbe() =>
        new(() => SensitiveProbeResult.Safe, new TestLogger(), 100);

    private sealed class FakeClipboard : IClipboardService
    {
        public uint Sequence { get; set; } = 1;
        public string Text { get; set; } = string.Empty;
        public bool ThrowOnRestore { get; set; }
        public int RestoreCount { get; private set; }
        public ClipboardSnapshot CaptureSnapshot() => new(new Dictionary<string, object>());
        public void Restore(ClipboardSnapshot snapshot)
        {
            RestoreCount++;
            if (ThrowOnRestore) throw new InvalidOperationException("restore failed");
        }
        public void Clear() { }
        public bool ContainsText() => !string.IsNullOrEmpty(Text);
        public string GetText() => Text;
        public uint GetSequenceNumber() => Sequence;
        public Task<PasteTextResult> PasteTextAsync(string text, ClipboardSnapshot snapshotToRestore, int restoreDelayMs, Func<bool> isTargetStillActive, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeKeyboard(Action onCopy) : IKeyboardInputService
    {
        public int PasteCount { get; private set; }
        public Task<bool> WaitForModifiersReleasedAsync(int timeoutMs, CancellationToken cancellationToken) => Task.FromResult(true);
        public void SendCopy() => onCopy();
        public void SendPaste() => PasteCount++;
    }

    private sealed class FakeActiveWindow : IActiveWindowService
    {
        public WindowIdentity Capture() => Window;
        public bool IsStillActive(WindowIdentity expected) => true;
    }

    private sealed class TestClipboardService(IKeyboardInputService keyboard)
        : ClipboardService(1, 10, keyboard, new TestLogger())
    {
        private uint sequence = 1;
        public bool ThrowOnRestore { get; set; }
        public bool ThrowOnSetText { get; set; }
        public int RestoreCount { get; private set; }
        public override void SetText(string text)
        {
            if (ThrowOnSetText) throw new InvalidOperationException("set failed");
            sequence++;
        }
        public override uint GetSequenceNumber() => sequence;
        public override void Restore(ClipboardSnapshot snapshot)
        {
            RestoreCount++;
            if (ThrowOnRestore) throw new InvalidOperationException("restore failed");
        }
    }
}
