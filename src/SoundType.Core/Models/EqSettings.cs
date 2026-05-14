namespace SoundType.Core.Models;

public sealed class EqSettings
{
    public const int BandCount = 10;

    public static readonly int[] Frequencies = [60, 170, 310, 600, 1000, 3000, 6000, 12000, 14000, 16000];

    public bool Enabled { get; set; }
    public double BassGainDb { get; set; }
    public double MidGainDb { get; set; }
    public double TrebleGainDb { get; set; }
    public List<double> BandGainsDb { get; set; } = [];
    public string PresetName { get; set; } = "Flat";

    public void Normalize()
    {
        if (BandGainsDb is null || BandGainsDb.Count == 0)
        {
            BandGainsDb =
            [
                BassGainDb,
                BassGainDb,
                MidGainDb,
                MidGainDb,
                MidGainDb,
                MidGainDb,
                TrebleGainDb,
                TrebleGainDb,
                TrebleGainDb,
                TrebleGainDb
            ];
        }

        if (BandGainsDb.Count < BandCount)
        {
            BandGainsDb.AddRange(Enumerable.Repeat(0.0, BandCount - BandGainsDb.Count));
        }
        else if (BandGainsDb.Count > BandCount)
        {
            BandGainsDb = BandGainsDb.Take(BandCount).ToList();
        }

        for (int i = 0; i < BandGainsDb.Count; i++)
        {
            BandGainsDb[i] = Math.Clamp(BandGainsDb[i], -12.0, 12.0);
        }

        BassGainDb = AverageBands(0, 2);
        MidGainDb = AverageBands(2, 4);
        TrebleGainDb = AverageBands(6, 4);
    }

    public double GetBandGainDb(int index)
    {
        Normalize();
        return BandGainsDb[index];
    }

    public void SetBandGainDb(int index, double gainDb)
    {
        Normalize();
        BandGainsDb[index] = Math.Clamp(gainDb, -12.0, 12.0);
        BassGainDb = AverageBands(0, 2);
        MidGainDb = AverageBands(2, 4);
        TrebleGainDb = AverageBands(6, 4);
    }

    public void SetPreset(string name, IReadOnlyList<double> gainsDb)
    {
        PresetName = name;
        BandGainsDb = gainsDb.Take(BandCount).Select(gain => Math.Clamp(gain, -12.0, 12.0)).ToList();
        Normalize();
        Enabled = BandGainsDb.Any(gain => Math.Abs(gain) > 0.001);
    }

    private double AverageBands(int start, int count) =>
        BandGainsDb.Skip(start).Take(count).DefaultIfEmpty(0).Average();
}
