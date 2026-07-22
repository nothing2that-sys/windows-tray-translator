using System.Security.Cryptography;
using System.Text;

namespace WindowsTrayTranslator.Security;

public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("WindowsTrayTranslator.v1");
    private readonly string secretFile;

    public DpapiSecretStore(string secretFile)
    {
        this.secretFile = secretFile;
    }

    public bool HasSecret(string key) => key == SecretNames.GeminiApiKey && File.Exists(secretFile);

    public string? ReadSecret(string key)
    {
        ValidateKey(key);
        if (!File.Exists(secretFile))
        {
            return null;
        }

        byte[] protectedBytes = File.ReadAllBytes(secretFile);
        byte[] clearBytes = ProtectedData.Unprotect(protectedBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
        try
        {
            return Encoding.UTF8.GetString(clearBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    public void WriteSecret(string key, string value)
    {
        ValidateKey(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("API 키가 비어 있습니다.", nameof(value));
        }

        byte[] clearBytes = Encoding.UTF8.GetBytes(value.Trim());
        byte[] protectedBytes = ProtectedData.Protect(clearBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
        try
        {
            string? directory = Path.GetDirectoryName(secretFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryFile = secretFile + ".tmp";
            File.WriteAllBytes(temporaryFile, protectedBytes);
            File.Move(temporaryFile, secretFile, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public void DeleteSecret(string key)
    {
        ValidateKey(key);
        if (File.Exists(secretFile))
        {
            File.Delete(secretFile);
        }
    }

    private static void ValidateKey(string key)
    {
        if (key != SecretNames.GeminiApiKey)
        {
            throw new ArgumentOutOfRangeException(nameof(key), "지원하지 않는 비밀 키입니다.");
        }
    }
}

public static class SecretNames
{
    public const string GeminiApiKey = "GeminiApiKey";
}
