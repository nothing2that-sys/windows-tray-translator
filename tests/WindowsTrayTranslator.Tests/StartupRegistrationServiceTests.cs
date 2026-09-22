using WindowsTrayTranslator.Startup;

namespace WindowsTrayTranslator.Tests;

public sealed class StartupRegistrationServiceTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Translator\Translator.exe", "\"C:\\Program Files\\Translator\\Translator.exe\"")]
    [InlineData(@"D:\Apps\Translator.exe", "\"D:\\Apps\\Translator.exe\"")]
    public void BuildCommand_AlwaysQuotesExecutablePath(string path, string expected)
    {
        Assert.Equal(expected, StartupRegistrationService.BuildCommand(path));
    }
}
