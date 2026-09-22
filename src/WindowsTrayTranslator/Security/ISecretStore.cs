namespace WindowsTrayTranslator.Security;

public interface ISecretStore
{
    bool HasSecret(string key);
    string? ReadSecret(string key);
    void WriteSecret(string key, string value);
    void DeleteSecret(string key);
}
