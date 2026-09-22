using System.Windows.Automation;
using WindowsTrayTranslator.Selection;

namespace WindowsTrayTranslator.Tests;

public sealed class UiAutomationSelectedTextProviderTests
{
    [Fact]
    public void IsPasswordPropertyValue_OnlyTreatsBooleanTrueAsPassword()
    {
        Assert.True(UiAutomationSelectedTextProvider.IsPasswordPropertyValue(true));
        Assert.False(UiAutomationSelectedTextProvider.IsPasswordPropertyValue(false));
        Assert.False(UiAutomationSelectedTextProvider.IsPasswordPropertyValue(AutomationElement.NotSupported));
        Assert.False(UiAutomationSelectedTextProvider.IsPasswordPropertyValue(new object()));
        Assert.False(UiAutomationSelectedTextProvider.IsPasswordPropertyValue(null));
    }
}
