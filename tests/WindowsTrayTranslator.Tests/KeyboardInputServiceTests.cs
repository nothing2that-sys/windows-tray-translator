using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Tests;

public sealed class KeyboardInputServiceTests
{
    [Fact]
    public void NativeInputSize_MatchesWindowsAbi()
    {
        int expected = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(expected, KeyboardInputService.NativeInputSize);
    }
}
