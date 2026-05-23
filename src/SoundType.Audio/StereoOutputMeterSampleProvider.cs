using NAudio.Wave;

namespace SoundType.Audio;

public readonly record struct StereoOutputLevel(float Left, float Right);

public sealed class StereoOutputMeterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private StereoOutputLevel _level;

    public StereoOutputMeterSampleProvider(ISampleProvider source)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }
    public StereoOutputLevel Level => _level;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read == 0)
        {
            _level = default;
            return 0;
        }

        float left = 0.0f;
        float right = 0.0f;
        int channels = Math.Max(1, WaveFormat.Channels);
        for (int n = 0; n < read; n++)
        {
            float magnitude = Math.Abs(buffer[offset + n]);
            if (channels == 1 || n % channels == 0)
            {
                left = Math.Max(left, magnitude);
            }
            else if (n % channels == 1)
            {
                right = Math.Max(right, magnitude);
            }
        }

        _level = channels == 1
            ? new StereoOutputLevel(left, left)
            : new StereoOutputLevel(left, right);
        return read;
    }
}
