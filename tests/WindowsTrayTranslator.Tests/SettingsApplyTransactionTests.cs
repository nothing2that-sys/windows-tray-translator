using WindowsTrayTranslator.Configuration;

namespace WindowsTrayTranslator.Tests;

public sealed class SettingsApplyTransactionTests
{
    [Fact]
    public void Execute_WhenLaterConfigureFails_RestoresAllAttemptedStatesAndContinuesAfterRollbackFailure()
    {
        int native = 1;
        int disk = 1;
        int service = 1;
        int memory = 1;
        List<string> rollbackFailures = [];
        SettingsApplyStep[] steps =
        [
            new("native", () => native = 2, () => native = 1),
            new("disk", () => disk = 2, () => disk = 1),
            new("service", () =>
            {
                service = 2;
                throw new InvalidOperationException("configure failed");
            }, () =>
            {
                service = 1;
                throw new IOException("rollback log only");
            }),
            new("memory", () => memory = 2, () => memory = 1)
        ];

        Assert.Throws<InvalidOperationException>(() => SettingsApplyTransaction.Execute(
            steps,
            (name, _) =>
            {
                rollbackFailures.Add(name);
                throw new IOException("logger failed");
            }));

        Assert.Equal(1, native);
        Assert.Equal(1, disk);
        Assert.Equal(1, service);
        Assert.Equal(1, memory);
        Assert.Equal(["service"], rollbackFailures);
    }
}
