using WindowsTrayTranslator.App;
using WindowsTrayTranslator.Configuration;

namespace WindowsTrayTranslator.Tests;

public sealed class ApplicationControllerPolicyTests
{
    [Fact]
    public void CopySettings_DeepClonesAllSectionsIncludingPaletteHotkey()
    {
        AppSettings source = new();
        source.Translation.ActionPaletteHotkey = "Ctrl+Shift+P";
        source.General.EnableTranslationHistory = true;
        AppSettings target = new();

        ApplicationController.CopySettings(source, target);
        source.Translation.ActionPaletteHotkey = string.Empty;
        source.General.EnableTranslationHistory = false;

        Assert.Equal("Ctrl+Shift+P", target.Translation.ActionPaletteHotkey);
        Assert.True(target.General.EnableTranslationHistory);
        Assert.NotSame(source.Translation, target.Translation);
        Assert.NotSame(source.General, target.General);
        Assert.NotSame(source.Api, target.Api);
    }

    [Fact]
    public void HistoryNotification_FollowsHistoryFlagNotLoggingFlag()
    {
        AppSettings settings = new();
        settings.General.EnableLogging = false;
        settings.General.EnableTranslationHistory = true;
        Assert.True(ApplicationController.ShouldRaiseTranslationHistoryChanged(settings));

        settings.General.EnableLogging = true;
        settings.General.EnableTranslationHistory = false;
        Assert.False(ApplicationController.ShouldRaiseTranslationHistoryChanged(settings));
    }
}
