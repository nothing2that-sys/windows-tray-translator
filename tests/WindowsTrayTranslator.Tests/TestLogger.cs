using WindowsTrayTranslator.Logging;

namespace WindowsTrayTranslator.Tests;

internal sealed class TestLogger : ILogger
{
    public List<string> Entries { get; } = [];

    public void Information(string message) => Entries.Add(message);
    public void Warning(string message) => Entries.Add(message);
    public void Error(string message, Exception? exception = null) => Entries.Add(message);
}
