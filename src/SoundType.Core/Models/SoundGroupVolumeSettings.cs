namespace SoundType.Core.Models;

public sealed class SoundGroupVolumeSettings
{
    public double Normal { get; set; } = 0.72;
    public double Enter { get; set; } = 0.80;
    public double Space { get; set; } = 0.65;
    public double Backspace { get; set; } = 0.70;
    public double Tab { get; set; } = 0.60;

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
