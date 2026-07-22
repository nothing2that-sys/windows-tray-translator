using WindowsTrayTranslator.Logging;

namespace WindowsTrayTranslator.Tests;

public sealed class FileLoggerTests
{
    [Theory]
    [InlineData("x-goog-api-key: secret-value")]
    [InlineData("Authorization=Bearer-secret")]
    [InlineData("https://example.test/path?key=secret-value&x=1")]
    public void Sanitize_RemovesSensitiveValues(string input)
    {
        string sanitized = FileLogger.Sanitize(input);

        Assert.DoesNotContain("secret-value", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer-secret", sanitized, StringComparison.OrdinalIgnoreCase);
    }
}
