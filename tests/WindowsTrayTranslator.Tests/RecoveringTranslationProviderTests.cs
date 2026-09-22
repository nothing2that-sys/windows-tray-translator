using WindowsTrayTranslator.Translation;

namespace WindowsTrayTranslator.Tests;

public sealed class RecoveringTranslationProviderTests
{
    private static readonly TranslationRequest Request = new("Hello", "Korean");

    [Fact]
    public async Task TranslateAsync_ThirdRecoverableFailure_ResetsAndRetriesCurrentRequest()
    {
        Queue<ITranslationProvider> sessions = new(
        [
            new SequenceProvider(
                Failure(TranslationFailureKind.Network),
                Failure(TranslationFailureKind.Timeout),
                Failure(TranslationFailureKind.Server)),
            new SequenceProvider(TranslationResult.Success("안녕하세요"))
        ]);
        int created = 0;
        using RecoveringTranslationProvider provider = new(
            () =>
            {
                created++;
                return sessions.Dequeue();
            },
            new TestLogger());

        Assert.False((await provider.TranslateAsync(Request, CancellationToken.None)).IsSuccess);
        Assert.False((await provider.TranslateAsync(Request, CancellationToken.None)).IsSuccess);
        TranslationResult recovered = await provider.TranslateAsync(Request, CancellationToken.None);

        Assert.True(recovered.IsSuccess);
        Assert.Equal("안녕하세요", recovered.TranslatedText);
        Assert.Equal(2, created);
    }

    [Fact]
    public async Task TranslateAsync_Success_ResetsConsecutiveFailureCount()
    {
        SequenceProvider session = new(
            Failure(TranslationFailureKind.Network),
            TranslationResult.Success("성공"),
            Failure(TranslationFailureKind.Timeout),
            Failure(TranslationFailureKind.Server));
        int created = 0;
        using RecoveringTranslationProvider provider = new(
            () =>
            {
                created++;
                return session;
            },
            new TestLogger());

        await provider.TranslateAsync(Request, CancellationToken.None);
        await provider.TranslateAsync(Request, CancellationToken.None);
        await provider.TranslateAsync(Request, CancellationToken.None);
        await provider.TranslateAsync(Request, CancellationToken.None);

        Assert.Equal(1, created);
    }

    [Theory]
    [InlineData(TranslationFailureKind.MissingApiKey)]
    [InlineData(TranslationFailureKind.Authentication)]
    [InlineData(TranslationFailureKind.RateLimit)]
    [InlineData(TranslationFailureKind.SafetyBlocked)]
    [InlineData(TranslationFailureKind.ModelUnavailable)]
    [InlineData(TranslationFailureKind.InvalidRequest)]
    [InlineData(TranslationFailureKind.Cancelled)]
    public async Task TranslateAsync_NonRecoverableFailure_DoesNotReset(
        TranslationFailureKind failureKind)
    {
        int created = 0;
        using RecoveringTranslationProvider provider = new(
            () =>
            {
                created++;
                return new RepeatingProvider(Failure(failureKind));
            },
            new TestLogger());

        for (int attempt = 0; attempt < 4; attempt++)
        {
            await provider.TranslateAsync(Request, CancellationToken.None);
        }

        Assert.Equal(1, created);
    }

    [Theory]
    [InlineData(TranslationFailureKind.Network)]
    [InlineData(TranslationFailureKind.Timeout)]
    [InlineData(TranslationFailureKind.Server)]
    [InlineData(TranslationFailureKind.EmptyResponse)]
    [InlineData(TranslationFailureKind.InvalidResponse)]
    public void IsRecoverable_TransientTransportFailures_ReturnTrue(
        TranslationFailureKind failureKind)
    {
        Assert.True(RecoveringTranslationProvider.IsRecoverable(failureKind));
    }

    private static TranslationResult Failure(TranslationFailureKind kind) =>
        TranslationResult.Failure(kind, "failed");

    private sealed class SequenceProvider(params TranslationResult[] results) : ITranslationProvider
    {
        private readonly Queue<TranslationResult> remaining = new(results);

        public Task<TranslationResult> TranslateAsync(
            TranslationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(remaining.Dequeue());
    }

    private sealed class RepeatingProvider(TranslationResult result) : ITranslationProvider
    {
        public Task<TranslationResult> TranslateAsync(
            TranslationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
