using WindowsTrayTranslator.App;

namespace WindowsTrayTranslator.Tests;

public sealed class LanguageSelectionRequestedEventArgsTests
{
    [Fact]
    public async Task Complete_ReturnsSelectedLanguage()
    {
        LanguageSelectionRequestedEventArgs request = new();
        request.MarkHandled();

        request.Complete("Vietnamese");

        Assert.True(request.WasHandled);
        Assert.Equal("Vietnamese", await request.Selection);
    }

    [Fact]
    public async Task Complete_WhenCancelled_ReturnsNull()
    {
        LanguageSelectionRequestedEventArgs request = new();

        request.Complete(null);

        Assert.Null(await request.Selection);
    }
}
