using WindowsTrayTranslator.Hotkeys;

namespace WindowsTrayTranslator.Tests;

public sealed class HotkeyDefinitionTests
{
    [Theory]
    [InlineData("Alt+R", HotkeyModifiers.Alt, 0x52u)]
    [InlineData("Ctrl+Shift+9", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x39u)]
    [InlineData("Win+F12", HotkeyModifiers.Win, 0x7Bu)]
    public void Parse_ValidCombination_ReturnsDefinition(
        string value,
        HotkeyModifiers expectedModifiers,
        uint expectedKey)
    {
        HotkeyDefinition definition = HotkeyDefinition.Parse(value);

        Assert.Equal(expectedModifiers, definition.Modifiers);
        Assert.Equal(expectedKey, definition.VirtualKey);
    }

    [Theory]
    [InlineData("R")]
    [InlineData("Alt")]
    [InlineData("Alt+F13")]
    [InlineData("Alt+R+T")]
    public void Parse_InvalidCombination_Throws(string value) =>
        Assert.Throws<FormatException>(() => HotkeyDefinition.Parse(value));

    [Fact]
    public void ReadAndReplace_Defaults_AreIndependent()
    {
        Assert.NotEqual(HotkeyDefinition.Parse("Alt+R"), HotkeyDefinition.Parse("Alt+T"));
    }
}
