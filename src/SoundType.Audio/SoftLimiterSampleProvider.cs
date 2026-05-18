using NAudio.Wave;

namespace SoundType.Audio;

public sealed class SoftLimiterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _ceiling;

    public SoftLimiterSampleProvider(ISampleProvider source, float ceiling = 0.92f)
    {
        _source = source;
        _ceiling = Math.Clamp(ceiling, 0.1f, 1.0f);
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        for (int n = 0; n < read; n++)
        {
            int index = offset + n;
            buffer[index] = SoftLimit(buffer[index], _ceiling);
        }

        return read;
    }

    private static float SoftLimit(float value, float ceiling)
    {
        float magnitude = Math.Abs(value);
        float knee = ceiling * 0.8f;
        if (magnitude <= knee)
        {
            return value;
        }

        float kneeRange = ceiling - knee;
        float limited = knee + (kneeRange * MathF.Tanh((magnitude - knee) / kneeRange));
        return MathF.CopySign(Math.Min(limited, ceiling), value);
    }
}
