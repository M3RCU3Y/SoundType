using NAudio.Wave;

namespace SoundType.Audio;

public sealed class StereoPanSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _leftGain;
    private readonly float _rightGain;

    public StereoPanSampleProvider(ISampleProvider source, double pan)
    {
        if (source.WaveFormat.Channels != 2)
        {
            throw new ArgumentException("Stereo panning requires a stereo sample provider.", nameof(source));
        }

        _source = source;
        WaveFormat = source.WaveFormat;
        double clampedPan = Math.Clamp(pan, -1.0, 1.0);
        double angle = (clampedPan + 1.0) * Math.PI / 4.0;
        _leftGain = (float)Math.Cos(angle);
        _rightGain = (float)Math.Sin(angle);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        for (int n = 0; n < read; n += 2)
        {
            buffer[offset + n] *= _leftGain;
            if (n + 1 < read)
            {
                buffer[offset + n + 1] *= _rightGain;
            }
        }

        return read;
    }
}
