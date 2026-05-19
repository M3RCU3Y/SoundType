namespace SoundType.Core.Models;

public enum PanMode
{
    KeyPosition,
    Random
}

public sealed class PanSettings
{
    private const double LegacyDefaultStrength = 1.1;

    public bool Enabled { get; set; }
    public PanMode Mode { get; set; } = PanMode.KeyPosition;
    public double Strength { get; set; }

    public void Normalize()
    {
        Strength = Math.Clamp(Strength, 0.0, 1.5);
    }

    public void NormalizeLegacyDefault()
    {
        if (Enabled &&
            Mode == PanMode.KeyPosition &&
            Math.Abs(Strength - LegacyDefaultStrength) < 0.001)
        {
            Enabled = false;
            Strength = 0.0;
        }

        Normalize();
    }
}
