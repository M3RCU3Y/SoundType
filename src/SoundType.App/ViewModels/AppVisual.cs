using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace SoundType.App.ViewModels;

internal sealed record AppVisual(
    string IconText,
    MediaFontFamily IconFontFamily,
    MediaBrush IconForeground,
    MediaBrush IconBackground,
    double IconFontSize)
{
    private static readonly MediaFontFamily Segoe = new("Segoe UI");
    private static readonly MediaFontFamily Mdl2 = new("Segoe MDL2 Assets");

    public static AppVisual ForProcess(string? processName)
    {
        string normalized = (processName ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "code.exe" or "devenv.exe" => new("\uE8A7", Mdl2, Brush(48, 166, 255), Brush(19, 32, 42), 24),
            "discord.exe" => new("\u25CF", Segoe, Brush(255, 255, 255), Brush(88, 101, 242), 18),
            "chrome.exe" => new("\u25CF", Segoe, Brush(255, 255, 255), Brush(246, 78, 57), 18),
            "obs64.exe" => new("\u25CC", Segoe, Brush(255, 255, 255), Brush(24, 28, 32), 22),
            "spotify.exe" => new("\u25CF", Segoe, Brush(9, 16, 13), Brush(30, 215, 96), 18),
            "explorer.exe" => new("\uE8B7", Mdl2, Brush(255, 213, 74), Brush(23, 31, 39), 22),
            "powershell.exe" => new(">", Segoe, Brush(124, 166, 255), Brush(23, 31, 39), 18),
            "notepad.exe" => new("\uE70B", Mdl2, Brush(113, 220, 255), Brush(23, 31, 39), 20),
            "" or "unknown" => new("?", Segoe, Brush(78, 217, 154), Brush(19, 32, 42), 18),
            _ => new(GetInitial(normalized), Segoe, Brush(78, 217, 154), Brush(19, 32, 42), 16)
        };
    }

    private static string GetInitial(string value) =>
        string.IsNullOrWhiteSpace(value) ? "?" : value[0].ToString().ToUpperInvariant();

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(MediaColor.FromRgb(r, g, b));
}
