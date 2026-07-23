using Microsoft.Win32;

namespace WindowsTrayTranslator.Startup;

public sealed class StartupRegistrationService
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "WindowsTrayTranslator";

    private readonly string executablePath;

    public StartupRegistrationService(string executablePath)
    {
        this.executablePath = executablePath;
    }

    public bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return string.Equals(key?.GetValue(ValueName) as string, BuildCommand(executablePath), StringComparison.Ordinal);
    }

    public void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Windows 시작 프로그램 레지스트리를 열지 못했습니다.");

        if (enabled)
        {
            key.SetValue(ValueName, BuildCommand(executablePath), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    internal static string BuildCommand(string path) => $"\"{path}\"";
}
