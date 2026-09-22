using WindowsTrayTranslator.Security;

namespace WindowsTrayTranslator.Tests;

public sealed class DpapiSecretStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"WindowsTrayTranslator.Secrets.{Guid.NewGuid():N}");

    [Fact]
    public void WriteReadDelete_RoundTripsWithoutPlaintext()
    {
        string path = Path.Combine(directory, "secrets.dat");
        DpapiSecretStore store = new(path);

        store.WriteSecret(SecretNames.GeminiApiKey, "super-secret-api-key");

        Assert.True(store.HasSecret(SecretNames.GeminiApiKey));
        Assert.Equal("super-secret-api-key", store.ReadSecret(SecretNames.GeminiApiKey));
        Assert.DoesNotContain("super-secret-api-key", Convert.ToBase64String(File.ReadAllBytes(path)), StringComparison.Ordinal);

        store.DeleteSecret(SecretNames.GeminiApiKey);
        Assert.False(store.HasSecret(SecretNames.GeminiApiKey));
    }

    [Fact]
    public void Operations_WithUnknownKeyName_AreRejected()
    {
        DpapiSecretStore store = new(Path.Combine(directory, "secrets.dat"));

        Assert.Throws<ArgumentOutOfRangeException>(() => store.WriteSecret("OtherKey", "value"));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.ReadSecret("OtherKey"));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.DeleteSecret("OtherKey"));
        Assert.False(store.HasSecret("OtherKey"));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
