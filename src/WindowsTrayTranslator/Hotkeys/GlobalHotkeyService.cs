using System.ComponentModel;
using System.Runtime.InteropServices;
using WindowsTrayTranslator.Logging;

namespace WindowsTrayTranslator.Hotkeys;

public sealed class GlobalHotkeyService : IDisposable
{
    public const int ReadHotkeyId = 1;
    public const int ReplaceHotkeyId = 2;
    public const int ActionPaletteHotkeyId = 3;

    private readonly HotkeyWindow window;
    private readonly ILogger logger;
    private readonly IHotkeyRegistrar registrar;
    private HotkeyDefinition? currentRead;
    private HotkeyDefinition? currentReplace;
    private HotkeyDefinition? currentPalette;

    public GlobalHotkeyService(Action<int> callback, ILogger logger)
        : this(callback, logger, new NativeHotkeyRegistrar())
    {
    }

    internal GlobalHotkeyService(Action<int> callback, ILogger logger, IHotkeyRegistrar registrar)
    {
        window = new HotkeyWindow(callback);
        this.logger = logger;
        this.registrar = registrar;
    }

    public IReadOnlyList<string> RegisterStartup(string readHotkey, string replaceHotkey, string? paletteHotkey)
    {
        UnregisterCurrent();
        List<string> failures = [];
        TryRegisterStartupOne(ReadHotkeyId, "읽기 단축키", readHotkey, value => currentRead = value, failures);
        TryRegisterStartupOne(ReplaceHotkeyId, "작성 단축키", replaceHotkey, value => currentReplace = value, failures);
        if (!string.IsNullOrWhiteSpace(paletteHotkey))
        {
            TryRegisterStartupOne(ActionPaletteHotkeyId, "Quick Action 단축키", paletteHotkey, value => currentPalette = value, failures);
        }

        // Startup used to log only failures, so a working set left no evidence at all and the most
        // common diagnostic question - "is Alt+R actually registered?" - had no answer in the log.
        logger.Information(
            "시작 시 전역 단축키 등록 결과. " +
            $"Read={DescribeOutcome(readHotkey, currentRead)}, " +
            $"Replace={DescribeOutcome(replaceHotkey, currentReplace)}, " +
            $"Palette={DescribeOutcome(paletteHotkey, currentPalette)}");
        return failures;
    }

    private static string DescribeOutcome(string? configured, HotkeyDefinition? current) =>
        string.IsNullOrWhiteSpace(configured)
            ? "미사용"
            : $"{configured}({(current is null ? "실패" : "성공")})";

    public void Register(string readHotkey, string replaceHotkey, string? paletteHotkey = null)
    {
        HotkeyDefinition read = HotkeyDefinition.Parse(readHotkey);
        HotkeyDefinition replace = HotkeyDefinition.Parse(replaceHotkey);
        HotkeyDefinition? palette = string.IsNullOrWhiteSpace(paletteHotkey)
            ? null
            : HotkeyDefinition.Parse(paletteHotkey);
        if (read == replace)
        {
            throw new InvalidOperationException("읽기 번역과 작성 번역에 같은 단축키를 사용할 수 없습니다.");
        }

        HotkeyDefinition? previousRead = currentRead;
        HotkeyDefinition? previousReplace = currentReplace;
        HotkeyDefinition? previousPalette = currentPalette;
        UnregisterCurrent();

        try
        {
            RegisterOne(ReadHotkeyId, read);
            currentRead = read;
            RegisterOne(ReplaceHotkeyId, replace);
            currentReplace = replace;
            if (palette is not null)
            {
                RegisterOne(ActionPaletteHotkeyId, palette);
                currentPalette = palette;
            }
            logger.Information($"전역 단축키를 등록했습니다. Read={readHotkey}, Replace={replaceHotkey}, PaletteEnabled={palette is not null}");
        }
        catch
        {
            UnregisterCurrent();
            TryRestore(previousRead, previousReplace, previousPalette);
            throw;
        }
    }

    private void TryRegisterStartupOne(
        int id,
        string displayName,
        string hotkey,
        Action<HotkeyDefinition> setCurrent,
        List<string> failures)
    {
        try
        {
            HotkeyDefinition definition = HotkeyDefinition.Parse(hotkey);
            RegisterOne(id, definition);
            setCurrent(definition);
        }
        catch (Exception ex)
        {
            failures.Add($"{displayName} ({hotkey}): {ex.Message}");
            logger.Error($"{displayName}를 등록하지 못했습니다.", ex);
        }
    }

    private void RegisterOne(int id, HotkeyDefinition definition)
    {
        uint modifiers = (uint)(definition.Modifiers | HotkeyModifiers.NoRepeat);
        if (!registrar.Register(window.Handle, id, modifiers, definition.VirtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "해당 단축키는 다른 프로그램에서 사용 중입니다.");
        }
    }

    private void TryRestore(HotkeyDefinition? read, HotkeyDefinition? replace, HotkeyDefinition? palette)
    {
        try
        {
            if (read is not null)
            {
                RegisterOne(ReadHotkeyId, read);
                currentRead = read;
            }

            if (replace is not null)
            {
                RegisterOne(ReplaceHotkeyId, replace);
                currentReplace = replace;
            }

            if (palette is not null)
            {
                RegisterOne(ActionPaletteHotkeyId, palette);
                currentPalette = palette;
            }
        }
        catch (Exception ex)
        {
            logger.Error("기존 단축키를 복구하지 못했습니다.", ex);
        }
    }

    private void UnregisterCurrent()
    {
        if (currentRead is not null)
        {
            registrar.Unregister(window.Handle, ReadHotkeyId);
            currentRead = null;
        }

        if (currentReplace is not null)
        {
            registrar.Unregister(window.Handle, ReplaceHotkeyId);
            currentReplace = null;
        }


        UnregisterPalette();
    }

    public void UnregisterPalette()
    {
        if (currentPalette is not null)
        {
            registrar.Unregister(window.Handle, ActionPaletteHotkeyId);
            currentPalette = null;
        }
    }

    internal HotkeyDefinition? CurrentRead => currentRead;
    internal HotkeyDefinition? CurrentReplace => currentReplace;
    internal HotkeyDefinition? CurrentPalette => currentPalette;

    public void Dispose()
    {
        UnregisterCurrent();
        window.Dispose();
    }
}

internal interface IHotkeyRegistrar
{
    bool Register(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);
    void Unregister(IntPtr windowHandle, int id);
}

internal sealed class NativeHotkeyRegistrar : IHotkeyRegistrar
{
    public bool Register(IntPtr windowHandle, int id, uint modifiers, uint virtualKey) =>
        NativeHotkeyMethods.RegisterHotKey(windowHandle, id, modifiers, virtualKey);

    public void Unregister(IntPtr windowHandle, int id) =>
        NativeHotkeyMethods.UnregisterHotKey(windowHandle, id);
}
