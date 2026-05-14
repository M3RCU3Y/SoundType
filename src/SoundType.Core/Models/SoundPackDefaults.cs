namespace SoundType.Core.Models;

public sealed class SoundPackDefaults
{
    public double Volume { get; set; } = 0.75;
    public double PitchVariation { get; set; }
    public bool Randomize { get; set; } = true;
}
