using System.ComponentModel;
using System.Runtime.InteropServices;
using WindowsTrayTranslator.Logging;

namespace WindowsTrayTranslator.Hotkeys;

public sealed class GlobalHotkeyService : IDisposable
{
    public const int ReadHotkeyId = 1;
    public const int ReplaceHotkeyId = 2;

    private readonly HotkeyWindow window;
    private readonly ILogger logger;
    private HotkeyDefinition? currentRead;
    private HotkeyDefinition? currentReplace;

    public GlobalHotkeyService(Action<int> callback, ILogger logger)
    {
        window = new HotkeyWindow(callback);
        this.logger = logger;
    }

    public void Register(string readHotkey, string replaceHotkey)
    {
        HotkeyDefinition read = HotkeyDefinition.Parse(readHotkey);
        HotkeyDefinition replace = HotkeyDefinition.Parse(replaceHotkey);
        if (read == replace)
        {
            throw new InvalidOperationException("읽기 번역과 작성 번역에 같은 단축키를 사용할 수 없습니다.");
        }

        HotkeyDefinition? previousRead = currentRead;
        HotkeyDefinition? previousReplace = currentReplace;
        UnregisterCurrent();

        try
        {
            RegisterOne(ReadHotkeyId, read);
            currentRead = read;
            RegisterOne(ReplaceHotkeyId, replace);
            currentReplace = replace;
            logger.Information($"전역 단축키를 등록했습니다. Read={readHotkey}, Replace={replaceHotkey}");
        }
        catch
        {
            UnregisterCurrent();
            TryRestore(previousRead, previousReplace);
            throw;
        }
    }

    private void RegisterOne(int id, HotkeyDefinition definition)
    {
        uint modifiers = (uint)(definition.Modifiers | HotkeyModifiers.NoRepeat);
        if (!NativeHotkeyMethods.RegisterHotKey(window.Handle, id, modifiers, definition.VirtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "해당 단축키는 다른 프로그램에서 사용 중입니다.");
        }
    }

    private void TryRestore(HotkeyDefinition? read, HotkeyDefinition? replace)
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
            NativeHotkeyMethods.UnregisterHotKey(window.Handle, ReadHotkeyId);
            currentRead = null;
        }

        if (currentReplace is not null)
        {
            NativeHotkeyMethods.UnregisterHotKey(window.Handle, ReplaceHotkeyId);
            currentReplace = null;
        }
    }

    public void Dispose()
    {
        UnregisterCurrent();
        window.Dispose();
    }
}
