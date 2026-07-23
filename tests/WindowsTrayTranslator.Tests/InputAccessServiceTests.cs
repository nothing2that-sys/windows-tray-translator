using WindowsTrayTranslator.Windows;

namespace WindowsTrayTranslator.Tests;

public sealed class InputAccessServiceTests
{
    [Theory]
    [InlineData(0x2000u, 0x1000u, true)]
    [InlineData(0x2000u, 0x2000u, true)]
    [InlineData(0x2000u, 0x3000u, false)]
    public void CanSendInput_RequiresEqualOrHigherIntegrity(uint current, uint target, bool expected)
    {
        Assert.Equal(expected, InputAccessService.CanSendInput(current, target));
    }
}
