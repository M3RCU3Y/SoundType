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
            Key.Snapshot => "PrintScreen",
            _ => key.ToString()
        };

        return new KeyIdentity(code, GetDisplayName(code), Categorize(key, code));
    }

    public static IReadOnlyList<KeyIdentity> CommonKeys { get; } = CreateCommonKeys();

    public static string GetDisplayName(string code) => code switch
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
        "PrintScreen" => "Print Screen",
        "PageUp" => "Page Up",
        "PageDown" => "Page Down",
        "NumLock" => "Num Lock",
        "NumPad0" => "Numpad 0",
        "NumPad1" => "Numpad 1",
        "NumPad2" => "Numpad 2",
        "NumPad3" => "Numpad 3",
        "NumPad4" => "Numpad 4",
        "NumPad5" => "Numpad 5",
        "NumPad6" => "Numpad 6",
        "NumPad7" => "Numpad 7",
        "NumPad8" => "Numpad 8",
        "NumPad9" => "Numpad 9",
        "Oem3" => "`",
        "OemMinus" => "-",
        "OemPlus" => "=",
        "OemOpenBrackets" => "[",
        "Oem6" => "]",
        "Oem5" => "\\",
        "Oem1" => ";",
        "OemQuotes" => "'",
        "OemComma" => ",",
        "OemPeriod" => ".",
        "OemQuestion" => "/",
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
        "Left" => "Left Arrow",
        "Right" => "Right Arrow",
        "Up" => "Up Arrow",
        "Down" => "Down Arrow",
        "Apps" => "Menu",
        _ => code
    };

    private static IReadOnlyList<KeyIdentity> CreateCommonKeys()
    {
        List<KeyIdentity> keys = [];

        void Add(string code, KeyCategory category) =>
            keys.Add(new KeyIdentity(code, GetDisplayName(code), category));

        Add("Escape", KeyCategory.Special);
        foreach (string key in Enumerable.Range(1, 12).Select(index => $"F{index}"))
        {
            Add(key, KeyCategory.Function);
        }

        foreach (string key in new[]
                 {
                     "Oem3", "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "D0",
                     "OemMinus", "OemPlus", "Backspace"
                 })
        {
            Add(key, key.StartsWith('D') ? KeyCategory.Number : KeyCategory.Character);
        }

        foreach (string key in new[]
                 {
                     "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P",
                     "OemOpenBrackets", "Oem6", "Oem5"
                 })
        {
            Add(key, key == "Tab" ? KeyCategory.Special : KeyCategory.Character);
        }

        foreach (string key in new[]
                 {
                     "CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L",
                     "Oem1", "OemQuotes", "Enter"
                 })
        {
            Add(key, key is "CapsLock" ? KeyCategory.Modifier : key is "Enter" ? KeyCategory.Special : KeyCategory.Character);
        }

        foreach (string key in new[]
                 {
                     "LeftShift", "Z", "X", "C", "V", "B", "N", "M",
                     "OemComma", "OemPeriod", "OemQuestion", "RightShift"
                 })
        {
            Add(key, key.Contains("Shift", StringComparison.OrdinalIgnoreCase) ? KeyCategory.Modifier : KeyCategory.Character);
        }

        foreach (string key in new[]
                 {
                     "LeftCtrl", "LeftWindows", "LeftAlt", "Space", "RightAlt", "RightWindows", "Apps", "RightCtrl"
                 })
        {
            Add(key, key is "Space" ? KeyCategory.Character : KeyCategory.Modifier);
        }

        foreach (string key in new[]
                 {
                     "PrintScreen", "Scroll", "Pause", "Insert", "Home", "PageUp", "Delete", "End", "PageDown",
                     "Up", "Left", "Down", "Right"
                 })
        {
            Add(key, key is "Up" or "Left" or "Down" or "Right" or "Insert" or "Home" or "PageUp" or "Delete" or "End" or "PageDown"
                ? KeyCategory.Navigation
                : KeyCategory.Special);
        }

        foreach (string key in new[]
                 {
                     "NumLock", "Divide", "Multiply", "Subtract", "NumPad7", "NumPad8", "NumPad9", "Add",
                     "NumPad4", "NumPad5", "NumPad6", "NumPad1", "NumPad2", "NumPad3", "NumPad0", "Decimal"
                 })
        {
            Add(key, key.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) ? KeyCategory.Numpad : KeyCategory.Special);
        }

        return keys;
    }

    private static KeyCategory Categorize(Key key, string code)
    {
        if (code.Contains("Shift", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("Ctrl", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("Alt", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
            code == "CapsLock" ||
            code == "Apps")
        {
            return KeyCategory.Modifier;
        }

        if (key >= Key.A && key <= Key.Z) return KeyCategory.Character;
        if (key >= Key.D0 && key <= Key.D9) return KeyCategory.Number;
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return KeyCategory.Numpad;
        if (key >= Key.F1 && key <= Key.F24) return KeyCategory.Function;
        if (key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Insert or Key.Delete)
        {
            return KeyCategory.Navigation;
        }

        if (code.StartsWith("Oem", StringComparison.OrdinalIgnoreCase) || code == "Space")
        {
            return KeyCategory.Character;
        }

        return code is "Enter" or "Backspace" or "Tab" or "Escape" or "PrintScreen" or "Scroll" or "Pause" or
                "NumLock" or "Divide" or "Multiply" or "Subtract" or "Add" or "Decimal"
            ? KeyCategory.Special
            : KeyCategory.Unknown;
    }
}
