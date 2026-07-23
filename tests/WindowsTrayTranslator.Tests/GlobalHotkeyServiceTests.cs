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
