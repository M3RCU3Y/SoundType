namespace SoundType.Core.Models;

public sealed class EqSettings
{
    public bool Enabled { get; set; }
    public double BassGainDb { get; set; }
    public double MidGainDb { get; set; }
    public double TrebleGainDb { get; set; }
    public string PresetName { get; set; } = "Flat";
}
