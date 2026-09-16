namespace WindowsTrayTranslator.Configuration;

internal sealed record SettingsApplyStep(string Name, Action Apply, Action Rollback);

internal static class SettingsApplyTransaction
{
    public static void Execute(
        IEnumerable<SettingsApplyStep> steps,
        Action<string, Exception> rollbackFailure)
    {
        Stack<SettingsApplyStep> attempted = new();
        try
        {
            foreach (SettingsApplyStep step in steps)
            {
                attempted.Push(step);
                step.Apply();
            }
        }
        catch
        {
            while (attempted.TryPop(out SettingsApplyStep? step))
            {
                try
                {
                    step.Rollback();
                }
                catch (Exception rollbackError)
                {
                    try
                    {
                        rollbackFailure(step.Name, rollbackError);
                    }
                    catch
                    {
                        // A logging failure must not prevent the remaining rollback steps.
                    }
                }
            }

            throw;
        }
    }
}
