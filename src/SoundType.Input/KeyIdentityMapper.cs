using System.Windows.Input;
using SoundType.Core.Models;

namespace SoundType.Input;

public static class KeyIdentityMapper
{
    public static KeyIdentity FromVirtualKey(int virtualKey)
    {
        Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
        string code = key switch
        {
            Key.Return => "Enter",
            Key.Space => "Space",
            Key.Back => "Backspace",
            Key.Tab => "Tab",
            Key.LeftShift => "LeftShift",
            Key.RightShift => "RightShift",
            Key.LeftCtrl => "LeftCtrl",
            Key.RightCtrl => "RightCtrl",
            Key.LeftAlt => "LeftAlt",
            Key.RightAlt => "RightAlt",
            Key.LWin => "LeftWindows",
            Key.RWin => "RightWindows",
            Key.CapsLock => "CapsLock",
            Key.Escape => "Escape",
            _ => key.ToString()
        };

        return new KeyIdentity(code, ToDisplayName(code), Categorize(key, code));
    }

    public static IReadOnlyList<KeyIdentity> CommonKeys { get; } =
    [
        new("A", "A", KeyCategory.Character),
        new("B", "B", KeyCategory.Character),
        new("C", "C", KeyCategory.Character),
        new("D", "D", KeyCategory.Character),
        new("E", "E", KeyCategory.Character),
        new("F", "F", KeyCategory.Character),
        new("G", "G", KeyCategory.Character),
        new("H", "H", KeyCategory.Character),
        new("I", "I", KeyCategory.Character),
        new("J", "J", KeyCategory.Character),
        new("K", "K", KeyCategory.Character),
        new("L", "L", KeyCategory.Character),
        new("M", "M", KeyCategory.Character),
        new("N", "N", KeyCategory.Character),
        new("O", "O", KeyCategory.Character),
        new("P", "P", KeyCategory.Character),
        new("Q", "Q", KeyCategory.Character),
        new("R", "R", KeyCategory.Character),
        new("S", "S", KeyCategory.Character),
        new("T", "T", KeyCategory.Character),
        new("U", "U", KeyCategory.Character),
        new("V", "V", KeyCategory.Character),
        new("W", "W", KeyCategory.Character),
        new("X", "X", KeyCategory.Character),
        new("Y", "Y", KeyCategory.Character),
        new("Z", "Z", KeyCategory.Character),
        new("D0", "0", KeyCategory.Number),
        new("D1", "1", KeyCategory.Number),
        new("D2", "2", KeyCategory.Number),
        new("D3", "3", KeyCategory.Number),
        new("D4", "4", KeyCategory.Number),
        new("D5", "5", KeyCategory.Number),
        new("D6", "6", KeyCategory.Number),
        new("D7", "7", KeyCategory.Number),
        new("D8", "8", KeyCategory.Number),
        new("D9", "9", KeyCategory.Number),
        new("Enter", "Enter", KeyCategory.Special),
        new("Space", "Space", KeyCategory.Character),
        new("Backspace", "Backspace", KeyCategory.Special),
        new("Tab", "Tab", KeyCategory.Special),
        new("LeftShift", "Left Shift", KeyCategory.Modifier),
        new("RightShift", "Right Shift", KeyCategory.Modifier),
        new("LeftCtrl", "Left Ctrl", KeyCategory.Modifier),
        new("RightCtrl", "Right Ctrl", KeyCategory.Modifier),
        new("LeftAlt", "Left Alt", KeyCategory.Modifier),
        new("RightAlt", "Right Alt", KeyCategory.Modifier),
        new("LeftWindows", "Left Windows", KeyCategory.Modifier),
        new("RightWindows", "Right Windows", KeyCategory.Modifier),
        new("CapsLock", "Caps Lock", KeyCategory.Modifier),
        new("Escape", "Escape", KeyCategory.Special),
        new("Left", "Left Arrow", KeyCategory.Navigation),
        new("Right", "Right Arrow", KeyCategory.Navigation),
        new("Up", "Up Arrow", KeyCategory.Navigation),
        new("Down", "Down Arrow", KeyCategory.Navigation),
        new("F1", "F1", KeyCategory.Function),
        new("F2", "F2", KeyCategory.Function),
        new("F3", "F3", KeyCategory.Function),
        new("F4", "F4", KeyCategory.Function),
        new("F5", "F5", KeyCategory.Function),
        new("F6", "F6", KeyCategory.Function),
        new("F7", "F7", KeyCategory.Function),
        new("F8", "F8", KeyCategory.Function),
        new("F9", "F9", KeyCategory.Function),
        new("F10", "F10", KeyCategory.Function),
        new("F11", "F11", KeyCategory.Function),
        new("F12", "F12", KeyCategory.Function)
    ];

    private static string ToDisplayName(string code) => code switch
    {
        "LeftShift" => "Left Shift",
        "RightShift" => "Right Shift",
        "LeftCtrl" => "Left Ctrl",
        "RightCtrl" => "Right Ctrl",
        "LeftAlt" => "Left Alt",
        "RightAlt" => "Right Alt",
        "LeftWindows" => "Left Windows",
        "RightWindows" => "Right Windows",
        "CapsLock" => "Caps Lock",
        "D0" => "0",
        "D1" => "1",
        "D2" => "2",
        "D3" => "3",
        "D4" => "4",
        "D5" => "5",
        "D6" => "6",
        "D7" => "7",
        "D8" => "8",
        "D9" => "9",
        _ => code
    };

    private static KeyCategory Categorize(Key key, string code)
    {
        if (code.Contains("Shift", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("Ctrl", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("Alt", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            code == "CapsLock")
        {
            return KeyCategory.Modifier;
        }

        if (key >= Key.A && key <= Key.Z) return KeyCategory.Character;
        if (key >= Key.D0 && key <= Key.D9) return KeyCategory.Number;
        if (key >= Key.F1 && key <= Key.F24) return KeyCategory.Function;
        if (key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            return KeyCategory.Navigation;
        }

        return code is "Enter" or "Space" or "Backspace" or "Tab" or "Escape"
            ? KeyCategory.Special
            : KeyCategory.Unknown;
    }
}
