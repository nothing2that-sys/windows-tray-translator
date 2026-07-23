using WindowsTrayTranslator.Clipboard;

namespace WindowsTrayTranslator.Tests;

public sealed class ClipboardRestoreGuardTests
{
    [Fact]
    public void ShouldRestore_WhenClipboardIsUnchanged_ReturnsTrue()
    {
        Assert.True(ClipboardService.ClipboardRestoreGuard.ShouldRestore(42, 42));
    }

    [Theory]
    [InlineData(42u, 43u)]
    [InlineData(0u, 0u)]
    public void ShouldRestore_WhenClipboardWasChangedOrNotWritten_ReturnsFalse(uint expected, uint current)
    {
        Assert.False(ClipboardService.ClipboardRestoreGuard.ShouldRestore(expected, current));
    }
}
