using NAudio.Wave;

namespace SoundType.Audio;

public sealed class LoadedSoundSampleProvider : ISampleProvider
{
    private readonly LoadedSoundSample _sample;
    private int _position;

    public LoadedSoundSampleProvider(LoadedSoundSample sample)
    {
        _sample = sample;
    }

    public WaveFormat WaveFormat => _sample.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int available = Math.Min(count, _sample.DecodedSamples.Length - _position);
        if (available <= 0)
        {
            return 0;
        }

        Array.Copy(_sample.DecodedSamples, _position, buffer, offset, available);
        _position += available;
        return available;
    }
}
