using WindowsTrayTranslator.Hotkeys;

namespace WindowsTrayTranslator.Tests;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void Register_WhenNewReplaceRegistrationFails_RestoresBothPreviousHotkeys()
    {
        FakeRegistrar registrar = new();
        using GlobalHotkeyService service = new(_ => { }, new TestLogger(), registrar);
        service.Register("Alt+R", "Alt+T");
        registrar.FailNextVirtualKey = (uint)'I';

        Assert.ThrowsAny<Exception>(() => service.Register("Ctrl+U", "Ctrl+I"));

        Assert.Equal(HotkeyDefinition.Parse("Alt+R"), service.CurrentRead);
        Assert.Equal(HotkeyDefinition.Parse("Alt+T"), service.CurrentReplace);
        Assert.Equal((uint)'R', registrar.Registered[GlobalHotkeyService.ReadHotkeyId]);
        Assert.Equal((uint)'T', registrar.Registered[GlobalHotkeyService.ReplaceHotkeyId]);
    }

    [Fact]
    public void RegisterStartup_WhenReplaceFails_KeepsReadRegistered()
    {
        FakeRegistrar registrar = new() { FailNextVirtualKey = (uint)'T' };
        using GlobalHotkeyService service = new(_ => { }, new TestLogger(), registrar);

        IReadOnlyList<string> failures = service.RegisterStartup("Alt+R", "Alt+T", "Ctrl+Alt+A");

        Assert.Single(failures);
        Assert.Equal((uint)'R', registrar.Registered[GlobalHotkeyService.ReadHotkeyId]);
        Assert.False(registrar.Registered.ContainsKey(GlobalHotkeyService.ReplaceHotkeyId));
        Assert.Equal((uint)'A', registrar.Registered[GlobalHotkeyService.ActionPaletteHotkeyId]);
    }

    [Fact]
    public void RegisterStartup_WhenPaletteFails_KeepsCoreRegistered()
    {
        FakeRegistrar registrar = new() { FailNextVirtualKey = (uint)'A' };
        using GlobalHotkeyService service = new(_ => { }, new TestLogger(), registrar);

        IReadOnlyList<string> failures = service.RegisterStartup("Alt+R", "Alt+T", "Ctrl+Alt+A");

        Assert.Single(failures);
        Assert.Equal((uint)'R', registrar.Registered[GlobalHotkeyService.ReadHotkeyId]);
        Assert.Equal((uint)'T', registrar.Registered[GlobalHotkeyService.ReplaceHotkeyId]);
        Assert.False(registrar.Registered.ContainsKey(GlobalHotkeyService.ActionPaletteHotkeyId));
    }

    /// <summary>
    /// A real startup with an occupied Ctrl+Alt+A logged only the failure, so there was no way to
    /// tell from the log whether Alt+R and Alt+T had registered. The summary must state every key.
    /// </summary>
    [Fact]
    public void RegisterStartup_LogsOutcomeForEveryHotkey()
    {
        FakeRegistrar registrar = new() { FailNextVirtualKey = (uint)'A' };
        TestLogger logger = new();
        using GlobalHotkeyService service = new(_ => { }, logger, registrar);

        service.RegisterStartup("Alt+R", "Alt+T", "Ctrl+Alt+A");

        string summary = Assert.Single(logger.Entries, entry => entry.Contains("시작 시 전역 단축키 등록 결과", StringComparison.Ordinal));
        Assert.Contains("Read=Alt+R(성공)", summary, StringComparison.Ordinal);
        Assert.Contains("Replace=Alt+T(성공)", summary, StringComparison.Ordinal);
        Assert.Contains("Palette=Ctrl+Alt+A(실패)", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterStartup_WithoutPaletteHotkey_ReportsItAsUnused()
    {
        FakeRegistrar registrar = new();
        TestLogger logger = new();
        using GlobalHotkeyService service = new(_ => { }, logger, registrar);

        service.RegisterStartup("Alt+R", "Alt+T", "");

        string summary = Assert.Single(logger.Entries, entry => entry.Contains("시작 시 전역 단축키 등록 결과", StringComparison.Ordinal));
        Assert.Contains("Read=Alt+R(성공)", summary, StringComparison.Ordinal);
        Assert.Contains("Palette=미사용", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_WhenPaletteRegistrationFails_RestoresPreviousRegistrationSet()
    {
        FakeRegistrar registrar = new();
        using GlobalHotkeyService service = new(_ => { }, new TestLogger(), registrar);
        service.Register("Alt+R", "Alt+T", "Ctrl+Alt+A");
        registrar.FailNextVirtualKey = (uint)'P';

        Assert.ThrowsAny<Exception>(() => service.Register("Ctrl+U", "Ctrl+I", "Ctrl+Alt+P"));

        Assert.Equal((uint)'R', registrar.Registered[GlobalHotkeyService.ReadHotkeyId]);
        Assert.Equal((uint)'T', registrar.Registered[GlobalHotkeyService.ReplaceHotkeyId]);
        Assert.Equal((uint)'A', registrar.Registered[GlobalHotkeyService.ActionPaletteHotkeyId]);
    }

    private sealed class FakeRegistrar : IHotkeyRegistrar
    {
        public Dictionary<int, uint> Registered { get; } = [];
        public uint? FailNextVirtualKey { get; set; }

        public bool Register(IntPtr windowHandle, int id, uint modifiers, uint virtualKey)
        {
            if (FailNextVirtualKey == virtualKey)
            {
                FailNextVirtualKey = null;
                return false;
            }

            Registered[id] = virtualKey;
            return true;
        }

        public void Unregister(IntPtr windowHandle, int id) => Registered.Remove(id);
    }
}
