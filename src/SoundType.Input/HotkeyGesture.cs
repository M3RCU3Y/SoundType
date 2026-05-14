using System.Windows.Input;

namespace SoundType.Input;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
    NoRepeat = 0x4000
}

public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    public const string DefaultText = "Ctrl+Alt+K";

    public static HotkeyGesture Default { get; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4B);

    public static bool TryParse(string? value, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        string? keyText = null;
        foreach (string part in parts)
        {
            if (TryParseModifier(part, out HotkeyModifiers modifier))
            {
                modifiers |= modifier;
                continue;
            }

            if (keyText is not null)
            {
                return false;
            }

            keyText = part;
        }

        if (modifiers == HotkeyModifiers.None || string.IsNullOrWhiteSpace(keyText))
        {
            return false;
        }

        Key key;
        try
        {
            key = (Key)new KeyConverter().ConvertFromString(keyText)!;
        }
        catch
        {
            return false;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0 || IsModifierKey(key))
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, virtualKey);
        return true;
    }

    private static bool TryParseModifier(string value, out HotkeyModifiers modifier)
    {
        modifier = value.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => HotkeyModifiers.Control,
            "ALT" => HotkeyModifiers.Alt,
            "SHIFT" => HotkeyModifiers.Shift,
            "WIN" or "WINDOWS" or "WINDOWSKEY" or "CMD" => HotkeyModifiers.Windows,
            _ => HotkeyModifiers.None
        };

        return modifier != HotkeyModifiers.None;
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
}
