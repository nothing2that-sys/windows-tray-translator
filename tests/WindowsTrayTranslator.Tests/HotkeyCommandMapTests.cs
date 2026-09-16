using WindowsTrayTranslator.Hotkeys;

namespace WindowsTrayTranslator.Tests;

public sealed class HotkeyCommandMapTests
{
    [Theory]
    [InlineData(GlobalHotkeyService.ReadHotkeyId, 0)]
    [InlineData(GlobalHotkeyService.ReplaceHotkeyId, 1)]
    [InlineData(GlobalHotkeyService.ActionPaletteHotkeyId, 2)]
    public void TryResolve_KnownId_ReturnsExplicitCommand(int id, int expected)
    {
        Assert.True(HotkeyCommandMap.TryResolve(id, out HotkeyCommand actual));
        Assert.Equal((HotkeyCommand)expected, actual);
    }

    [Fact]
    public void TryResolve_UnknownId_IsRejected()
    {
        Assert.False(HotkeyCommandMap.TryResolve(999, out _));
    }
}
