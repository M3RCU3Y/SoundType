using SoundType.Core.Models;

namespace SoundType.Audio;

public static class KeyboardPanResolver
{
    private static readonly IReadOnlyDictionary<string, double> KeyPositions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        ["`"] = 0, ["1"] = 1, ["2"] = 2, ["3"] = 3, ["4"] = 4, ["5"] = 5, ["6"] = 6, ["7"] = 7, ["8"] = 8, ["9"] = 9, ["0"] = 10, ["-"] = 11, ["="] = 12, ["Backspace"] = 13,
        ["Tab"] = 0.5, ["Q"] = 1.5, ["W"] = 2.5, ["E"] = 3.5, ["R"] = 4.5, ["T"] = 5.5, ["Y"] = 6.5, ["U"] = 7.5, ["I"] = 8.5, ["O"] = 9.5, ["P"] = 10.5, ["["] = 11.5, ["]"] = 12.5, ["\\"] = 13.5,
        ["CapsLock"] = 0.75, ["A"] = 1.75, ["S"] = 2.75, ["D"] = 3.75, ["F"] = 4.75, ["G"] = 5.75, ["H"] = 6.75, ["J"] = 7.75, ["K"] = 8.75, ["L"] = 9.75, [";"] = 10.75, ["'"] = 11.75, ["Enter"] = 13,
        ["LeftShift"] = 0.5, ["Z"] = 2, ["X"] = 3, ["C"] = 4, ["V"] = 5, ["B"] = 6, ["N"] = 7, ["M"] = 8, [","] = 9, ["."] = 10, ["/"] = 11, ["RightShift"] = 13,
        ["LeftCtrl"] = 0, ["LeftWindows"] = 1.2, ["LeftAlt"] = 2.4, ["Space"] = 6.5, ["RightAlt"] = 10.6, ["RightWindows"] = 11.8, ["RightCtrl"] = 13,
        ["Insert"] = 14.5, ["Home"] = 15.5, ["PageUp"] = 16.5, ["Delete"] = 14.5, ["End"] = 15.5, ["PageDown"] = 16.5,
        ["Left"] = 14.5, ["Down"] = 15.5, ["Right"] = 16.5, ["Up"] = 15.5
    };

    private const double MaxPosition = 16.5;

    public static double Resolve(KeyIdentity key)
    {
        if (!KeyPositions.TryGetValue(key.Code, out double x) &&
            !KeyPositions.TryGetValue(key.DisplayName, out x))
        {
            return 0;
        }

        return x / MaxPosition * 2.0 - 1.0;
    }
}
