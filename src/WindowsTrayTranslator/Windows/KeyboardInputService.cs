using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WindowsTrayTranslator.Windows;

public interface IKeyboardInputService
{
    Task<bool> WaitForModifiersReleasedAsync(int timeoutMs, CancellationToken cancellationToken);
    void SendCopy();
    void SendPaste();
}

public sealed class KeyboardInputService : IKeyboardInputService
{
    private const ushort VkControl = 0x11;
    private const ushort VkC = 0x43;
    private const ushort VkV = 0x56;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

    public async Task<bool> WaitForModifiersReleasedAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        int elapsed = 0;
        while (AreModifiersPressed() && elapsed < timeoutMs)
        {
            await Task.Delay(20, cancellationToken);
            elapsed += 20;
        }

        return !AreModifiersPressed();
    }

    public void SendCopy() => SendChord(VkControl, VkC);
    public void SendPaste() => SendChord(VkControl, VkV);

    internal static int NativeInputSize => Marshal.SizeOf<Input>();

    private static void SendChord(ushort modifier, ushort key)
    {
        Input[] inputs =
        [
            CreateKeyInput(modifier, 0),
            CreateKeyInput(key, 0),
            CreateKeyInput(key, KeyeventfKeyup),
            CreateKeyInput(modifier, KeyeventfKeyup)
        ];

        uint sent = SendInput((uint)inputs.Length, inputs, NativeInputSize);
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "키보드 입력을 대상 프로그램에 전달하지 못했습니다.");
        }
    }

    private static bool AreModifiersPressed() =>
        IsPressed(0x10) || IsPressed(0x11) || IsPressed(0x12) || IsPressed(0x5B) || IsPressed(0x5C);

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static Input CreateKeyInput(ushort virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = flags
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }
}
