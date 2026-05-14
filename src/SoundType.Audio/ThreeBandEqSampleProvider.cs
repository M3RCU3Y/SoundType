using NAudio.Dsp;
using NAudio.Wave;
using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class ThreeBandEqSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly BiQuadFilter[] _bassFilters;
    private readonly BiQuadFilter[] _midFilters;
    private readonly BiQuadFilter[] _trebleFilters;

    public ThreeBandEqSampleProvider(ISampleProvider source, EqSettings settings)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
        int channels = Math.Max(1, WaveFormat.Channels);
        _bassFilters = new BiQuadFilter[channels];
        _midFilters = new BiQuadFilter[channels];
        _trebleFilters = new BiQuadFilter[channels];

        for (int channel = 0; channel < channels; channel++)
        {
            _bassFilters[channel] = BiQuadFilter.LowShelf(WaveFormat.SampleRate, 180, 0.7f, (float)settings.BassGainDb);
            _midFilters[channel] = BiQuadFilter.PeakingEQ(WaveFormat.SampleRate, 1100, 1.0f, (float)settings.MidGainDb);
            _trebleFilters[channel] = BiQuadFilter.HighShelf(WaveFormat.SampleRate, 6500, 0.7f, (float)settings.TrebleGainDb);
        }
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        int channels = Math.Max(1, WaveFormat.Channels);
        for (int n = 0; n < read; n++)
        {
            int channel = n % channels;
            float sample = buffer[offset + n];
            sample = _bassFilters[channel].Transform(sample);
            sample = _midFilters[channel].Transform(sample);
            sample = _trebleFilters[channel].Transform(sample);
            buffer[offset + n] = sample;
        }

        return read;
    }
}
