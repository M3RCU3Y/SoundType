namespace SoundType.Core.Models;

public sealed class SoundGroupVolumeSettings
{
    public double Normal { get; set; } = 1.0;
    public double Enter { get; set; } = 1.0;
    public double Space { get; set; } = 1.0;
    public double Backspace { get; set; } = 1.0;
    public double Tab { get; set; } = 1.0;

    public double GetVolumeForGroup(string? group) =>
        NormalizeGroup(group) switch
        {
            "enter" => Enter,
            "space" => Space,
            "backspace" => Backspace,
            "tab" => Tab,
            _ => Normal
        };

    public void Clamp()
    {
        Normal = ClampVolume(Normal);
        Enter = ClampVolume(Enter);
        Space = ClampVolume(Space);
        Backspace = ClampVolume(Backspace);
        Tab = ClampVolume(Tab);
    }

    private static string NormalizeGroup(string? group) =>
        string.IsNullOrWhiteSpace(group) ? "normal" : group.Trim().ToLowerInvariant();

    private static double ClampVolume(double value) => Math.Clamp(value, 0.0, 1.5);
}
