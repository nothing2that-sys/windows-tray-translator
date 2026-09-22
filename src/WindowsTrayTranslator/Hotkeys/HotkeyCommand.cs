namespace WindowsTrayTranslator.Hotkeys;

internal enum HotkeyCommand
{
    ReadTranslation,
    ReplaceTranslation,
    ShowActionPalette
}

internal static class HotkeyCommandMap
{
    public static bool TryResolve(int hotkeyId, out HotkeyCommand command)
    {
        command = hotkeyId switch
        {
            GlobalHotkeyService.ReadHotkeyId => HotkeyCommand.ReadTranslation,
            GlobalHotkeyService.ReplaceHotkeyId => HotkeyCommand.ReplaceTranslation,
            GlobalHotkeyService.ActionPaletteHotkeyId => HotkeyCommand.ShowActionPalette,
            _ => default
        };

        return hotkeyId is GlobalHotkeyService.ReadHotkeyId or
            GlobalHotkeyService.ReplaceHotkeyId or
            GlobalHotkeyService.ActionPaletteHotkeyId;
    }
}
