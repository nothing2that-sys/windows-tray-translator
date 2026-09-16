using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Translation;

namespace WindowsTrayTranslator.Tests;

/// <summary>
/// Covers the palette generation lifecycle contract: the operation gate is always recovered and a
/// stale or cancelled operation publishes nothing.
/// </summary>
public sealed class QuickActionGenerationLifecycleTests
{
    [Fact]
    public async Task Success_PublishesOnceAndReleasesGate()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancellation = new();

        await ApplicationController.RunPaletteGenerationAsync(
            _ => Task.FromResult(TranslationResult.Success("요약 결과")),
            cancellation,
            () => true,
            recorder.Publish,
            recorder.ReportUnexpected,
            recorder.Complete,
            recorder.Logger);

        Assert.Equal(1, recorder.PublishCount);
        Assert.Equal("요약 결과", recorder.Published!.TranslatedText);
        Assert.Equal(0, recorder.UnexpectedCount);
        Assert.Equal(1, recorder.CompleteCount);
    }

    [Fact]
    public async Task ProviderFailure_PublishesErrorAndReleasesGate()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancellation = new();

        await ApplicationController.RunPaletteGenerationAsync(
            _ => Task.FromResult(TranslationResult.Failure(TranslationFailureKind.Server, "서버에서 오류가 발생했습니다.")),
            cancellation,
            () => true,
            recorder.Publish,
            recorder.ReportUnexpected,
            recorder.Complete,
            recorder.Logger);

        Assert.Equal(1, recorder.PublishCount);
        Assert.False(recorder.Published!.IsSuccess);
        Assert.Equal(1, recorder.CompleteCount);
    }

    [Fact]
    public async Task SynchronousThrowAtEntry_RecoversGateAndReportsOnce()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancellation = new();

        await ApplicationController.RunPaletteGenerationAsync(
            _ => throw new InvalidOperationException("entry failed"),
            cancellation,
            () => true,
            recorder.Publish,
            recorder.ReportUnexpected,
            recorder.Complete,
            recorder.Logger);

        Assert.Equal(0, recorder.PublishCount);
        Assert.Equal(1, recorder.UnexpectedCount);
        Assert.Equal(1, recorder.CompleteCount);
    }

    [Fact]
    public async Task AsynchronousThrow_RecoversGateAndReportsOnce()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancellation = new();

        await ApplicationController.RunPaletteGenerationAsync(
            async _ =>
            {
                await Task.Yield();
                throw new HttpRequestException("network down");
            },
            cancellation,
            () => true,
            recorder.Publish,
            recorder.ReportUnexpected,
            recorder.Complete,
            recorder.Logger);

        Assert.Equal(0, recorder.PublishCount);
        Assert.Equal(1, recorder.UnexpectedCount);
        Assert.Equal(1, recorder.CompleteCount);
    }

    [Fact]
    public async Task Cancellation_PublishesNothingAndStillReleasesGate()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancellation = new();

        await ApplicationController.RunPaletteGenerationAsync(
            async token =>
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
                return TranslationResult.Success("도달하지 않음");
            },
            cancellation,
            () => true,
            recorder.Publish,
            recorder.ReportUnexpected,
            recorder.Complete,
            recorder.Logger);

        Assert.Equal(0, recorder.PublishCount);
        Assert.Equal(0, recorder.UnexpectedCount);
        Assert.Equal(1, recorder.CompleteCount);
    }

    [Fact]
    public async Task CancelledAfterResultArrives_SuppressesStalePopup()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancellation = new();

        await ApplicationController.RunPaletteGenerationAsync(
            async _ =>
            {
                // The popup was closed while the response was in flight.
                await cancellation.CancelAsync();
                return TranslationResult.Success("늦게 도착한 결과");
            },
            cancellation,
            () => true,
            recorder.Publish,
            recorder.ReportUnexpected,
            recorder.Complete,
            recorder.Logger);

        Assert.Equal(0, recorder.PublishCount);
        Assert.Equal(1, recorder.CompleteCount);
    }

    [Fact]
    public async Task StaleOperation_SuppressesPopupAndStillReleasesGate()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancellation = new();

        await ApplicationController.RunPaletteGenerationAsync(
            _ => Task.FromResult(TranslationResult.Success("이전 세션 결과")),
            cancellation,
            () => false,
            recorder.Publish,
            recorder.ReportUnexpected,
            recorder.Complete,
            recorder.Logger);

        Assert.Equal(0, recorder.PublishCount);
        Assert.Equal(1, recorder.CompleteCount);
    }

    [Fact]
    public async Task CancelledFailureKind_IsNotShownAsAnError()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancellation = new();

        await ApplicationController.RunPaletteGenerationAsync(
            _ => Task.FromResult(TranslationResult.Failure(TranslationFailureKind.Cancelled, "요청이 취소되었습니다.")),
            cancellation,
            () => true,
            recorder.Publish,
            recorder.ReportUnexpected,
            recorder.Complete,
            recorder.Logger);

        Assert.Equal(0, recorder.PublishCount);
        Assert.Equal(1, recorder.CompleteCount);
    }

    [Fact]
    public async Task PublishThrowing_StillReleasesGate()
    {
        Recorder recorder = new();
        using CancellationTokenSource cancellation = new();

        await ApplicationController.RunPaletteGenerationAsync(
            _ => Task.FromResult(TranslationResult.Success("결과")),
            cancellation,
            () => true,
            _ => throw new InvalidOperationException("popup failed"),
            recorder.ReportUnexpected,
            recorder.Complete,
            recorder.Logger);

        Assert.Equal(1, recorder.UnexpectedCount);
        Assert.Equal(1, recorder.CompleteCount);
    }

    private sealed class Recorder
    {
        public TestLogger Logger { get; } = new();
        public int PublishCount { get; private set; }
        public int UnexpectedCount { get; private set; }
        public int CompleteCount { get; private set; }
        public TranslationResult? Published { get; private set; }

        public void Publish(TranslationResult result)
        {
            PublishCount++;
            Published = result;
        }

        public void ReportUnexpected(Exception exception) => UnexpectedCount++;

        public void Complete() => CompleteCount++;
    }
}
