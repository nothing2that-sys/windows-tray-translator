using System.Globalization;

namespace WindowsTrayTranslator.Hotkeys;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    NoRepeat = 0x4000
}

public sealed record HotkeyDefinition(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public static HotkeyDefinition Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("단축키가 비어 있습니다.");
        }

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        uint? virtualKey = null;
        foreach (string rawPart in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string part = rawPart.ToUpperInvariant();
            switch (part)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "ALT":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "SHIFT":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= HotkeyModifiers.Win;
                    break;
                default:
                    if (virtualKey is not null)
                    {
                        throw new FormatException("단축키에는 일반 키를 하나만 지정할 수 있습니다.");
                    }

                    virtualKey = ParseVirtualKey(part);
                    break;
            }
        }

        if (modifiers == HotkeyModifiers.None)
        {
            throw new FormatException("Ctrl, Alt, Shift, Win 중 하나 이상이 필요합니다.");
        }

        if (virtualKey is null)
        {
            throw new FormatException("문자, 숫자 또는 기능 키가 필요합니다.");
        }

        return new HotkeyDefinition(modifiers, virtualKey.Value);
    }

    private static uint ParseVirtualKey(string value)
    {
        if (value.Length == 1)
        {
            char key = value[0];
            if (key is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                return key;
            }
        }

        if (value.StartsWith('F') &&
            int.TryParse(value.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int functionKey) &&
            functionKey is >= 1 and <= 12)
        {
            return (uint)(0x70 + functionKey - 1);
        }

        throw new FormatException($"지원하지 않는 단축키입니다: {value}");
    }
}
