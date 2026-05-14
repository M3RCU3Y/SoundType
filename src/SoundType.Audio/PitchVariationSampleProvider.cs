using NAudio.Wave;

namespace SoundType.Audio;

public sealed class PitchVariationSampleProvider : ISampleProvider
{
    private readonly float[] _source;
    private readonly int _channels;
    private readonly int _frameCount;
    private readonly double _speedFactor;
    private double _position;

    public PitchVariationSampleProvider(ISampleProvider source, double speedFactor)
    {
        if (speedFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speedFactor), "Speed factor must be greater than zero.");
        }

        WaveFormat = source.WaveFormat;
        _channels = WaveFormat.Channels;
        _speedFactor = speedFactor;
        _source = ReadAll(source);
        _frameCount = _source.Length / _channels;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_frameCount == 0)
        {
            return 0;
        }

        int framesRequested = count / _channels;
        int framesWritten = 0;
        while (framesWritten < framesRequested && _position < _frameCount)
        {
            int leftFrame = (int)_position;
            int rightFrame = Math.Min(leftFrame + 1, _frameCount - 1);
            float blend = (float)(_position - leftFrame);

            for (int channel = 0; channel < _channels; channel++)
            {
                float left = _source[leftFrame * _channels + channel];
                float right = _source[rightFrame * _channels + channel];
                buffer[offset + framesWritten * _channels + channel] = left + (right - left) * blend;
            }

            framesWritten++;
            _position += _speedFactor;
        }

        return framesWritten * _channels;
    }

    private static float[] ReadAll(ISampleProvider source)
    {
        List<float> samples = [];
        float[] buffer = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels / 10];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            samples.AddRange(buffer.Take(read));
        }

        return samples.ToArray();
    }
}
