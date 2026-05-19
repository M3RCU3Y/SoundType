namespace SoundType.Core.Models;

public enum PanMode
{
    KeyPosition,
    Random
}

public sealed class PanSettings
{
    public bool Enabled { get; set; } = true;
    public PanMode Mode { get; set; } = PanMode.KeyPosition;
    public double Strength { get; set; } = 1.1;

    public void Normalize()
    {
        Strength = Math.Clamp(Strength, 0.0, 1.5);
    }
}
