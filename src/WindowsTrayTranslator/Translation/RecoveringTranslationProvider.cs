using WindowsTrayTranslator.Logging;

namespace WindowsTrayTranslator.Translation;

public sealed class RecoveringTranslationProvider : ITranslationProvider, IDisposable
{
    private const int ResetThreshold = 3;
    private readonly Func<ITranslationProvider> providerFactory;
    private readonly ILogger logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private ITranslationProvider provider;
    private int consecutiveRecoverableFailures;
    private bool disposed;

    public RecoveringTranslationProvider(
        Func<ITranslationProvider> providerFactory,
        ILogger logger)
    {
        this.providerFactory = providerFactory;
        this.logger = logger;
        provider = providerFactory();
    }

    public async Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken);
        try
        {
            TranslationResult result = await provider.TranslateAsync(request, cancellationToken);
            if (result.IsSuccess)
            {
                ResetFailureCountAfterSuccess();
                return result;
            }

            if (!IsRecoverable(result.FailureKind))
            {
                consecutiveRecoverableFailures = 0;
                return result;
            }

            consecutiveRecoverableFailures++;
            logger.Warning(
                $"복구 가능한 번역 실패가 연속 발생했습니다. Kind={result.FailureKind}, Count={consecutiveRecoverableFailures}/{ResetThreshold}");
            if (consecutiveRecoverableFailures < ResetThreshold)
            {
                return result;
            }

            if (!TryResetProvider(result.FailureKind))
            {
                return result;
            }

            TranslationResult retry = await provider.TranslateAsync(request, cancellationToken);
            if (retry.IsSuccess)
            {
                consecutiveRecoverableFailures = 0;
                logger.Information("번역 통신 초기화 후 자동 재시도에 성공했습니다.");
            }
            else
            {
                consecutiveRecoverableFailures = IsRecoverable(retry.FailureKind) ? 1 : 0;
                logger.Warning(
                    $"번역 통신 초기화 후 자동 재시도에 실패했습니다. Kind={retry.FailureKind}");
            }

            return retry;
        }
        finally
        {
            gate.Release();
        }
    }

    internal static bool IsRecoverable(TranslationFailureKind kind) => kind is
        TranslationFailureKind.Network or
        TranslationFailureKind.Timeout or
        TranslationFailureKind.Server or
        TranslationFailureKind.EmptyResponse or
        TranslationFailureKind.InvalidResponse;

    private void ResetFailureCountAfterSuccess()
    {
        if (consecutiveRecoverableFailures > 0)
        {
            logger.Information(
                $"번역 성공으로 연속 실패 횟수를 초기화했습니다. PreviousCount={consecutiveRecoverableFailures}");
            consecutiveRecoverableFailures = 0;
        }
    }

    private bool TryResetProvider(TranslationFailureKind trigger)
    {
        logger.Warning($"연속 번역 실패로 통신 객체를 초기화합니다. Trigger={trigger}");
        try
        {
            ITranslationProvider replacement = providerFactory();
            DisposeProvider(provider);
            provider = replacement;
            consecutiveRecoverableFailures = 0;
            return true;
        }
        catch (Exception ex)
        {
            consecutiveRecoverableFailures = 0;
            logger.Error("번역 통신 객체를 초기화하지 못했습니다.", ex);
            return false;
        }
    }

    private static void DisposeProvider(ITranslationProvider candidate)
    {
        if (candidate is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DisposeProvider(provider);
        gate.Dispose();
    }
}
