using WindowsTrayTranslator.Selection;
using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Tests;

public sealed class CompositeSelectedTextProviderTests
{
    private static readonly WindowIdentity Window = new(new IntPtr(1), 10, "Test", new IntPtr(2));

    [Fact]
    public async Task GetSelectedTextAsync_PrimarySuccess_DoesNotCallFallback()
    {
        FakeProvider primary = new(SelectedTextResult.Success("primary", Window));
        FakeProvider fallback = new(SelectedTextResult.Success("fallback", Window));
        CompositeSelectedTextProvider provider = new(primary, fallback, new TestLogger());

        SelectedTextResult result = await provider.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.Equal("primary", result.Text);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, fallback.CallCount);
    }

    [Fact]
    public async Task GetSelectedTextAsync_PrimaryRecoverableFailure_CallsFallback()
    {
        FakeProvider primary = new(SelectedTextResult.Failure("failed", Window, canFallback: true));
        FakeProvider fallback = new(SelectedTextResult.Success("fallback", Window));
        CompositeSelectedTextProvider provider = new(primary, fallback, new TestLogger());

        SelectedTextResult result = await provider.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.Equal("fallback", result.Text);
        Assert.Equal(1, fallback.CallCount);
    }

    [Fact]
    public async Task GetSelectedTextAsync_NonRecoverableFailure_DoesNotCallFallback()
    {
        FakeProvider primary = new(SelectedTextResult.Failure("window changed", Window, canFallback: false));
        FakeProvider fallback = new(SelectedTextResult.Success("fallback", Window));
        CompositeSelectedTextProvider provider = new(primary, fallback, new TestLogger());

        SelectedTextResult result = await provider.GetSelectedTextAsync(Window, 100, 1000, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, fallback.CallCount);
    }

    private sealed class FakeProvider(SelectedTextResult result) : ISelectedTextProvider
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
}
