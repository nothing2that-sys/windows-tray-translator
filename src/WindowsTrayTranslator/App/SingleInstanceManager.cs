namespace WindowsTrayTranslator.App;

public sealed class SingleInstanceManager : IDisposable
{
    private readonly Mutex mutex;

    public SingleInstanceManager(string applicationId)
    {
        mutex = new Mutex(initiallyOwned: true, $"Local\\{applicationId}", out bool createdNew);
        IsPrimaryInstance = createdNew;
    }

    public bool IsPrimaryInstance { get; }

    public void Dispose()
    {
        if (IsPrimaryInstance)
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was not owned during an abnormal shutdown path.
            }
        }

        mutex.Dispose();
    }
}
