using NAudio.Wave;

namespace SoundType.Audio;

public sealed class LimiterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _threshold;

    public LimiterSampleProvider(ISampleProvider source, float threshold = 0.98f)
    {
        _source = source;
        _threshold = Math.Clamp(threshold, 0.1f, 1.0f);
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        for (int n = 0; n < read; n++)
        {
            float value = buffer[offset + n];
            buffer[offset + n] = Math.Clamp(value, -_threshold, _threshold);
        }

        return read;
    }
}
