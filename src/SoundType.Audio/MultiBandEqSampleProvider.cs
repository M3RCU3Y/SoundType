using NAudio.Dsp;
using NAudio.Wave;
using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class MultiBandEqSampleProvider : ISampleProvider
{
    private static readonly float[] BandWidths = [40, 100, 180, 350, 600, 1800, 3500, 4000, 3000, 3000];
    private readonly ISampleProvider _source;
    private readonly BiQuadFilter[][] _filters;

    public MultiBandEqSampleProvider(ISampleProvider source, EqSettings settings)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
        settings.Normalize();
        int channels = Math.Max(1, WaveFormat.Channels);
        _filters = new BiQuadFilter[EqSettings.BandCount][];

        for (int band = 0; band < EqSettings.BandCount; band++)
        {
            double gain = settings.GetBandGainDb(band);
            _filters[band] = new BiQuadFilter[channels];
            for (int channel = 0; channel < channels; channel++)
            {
                _filters[band][channel] = BiQuadFilter.PeakingEQ(
                    WaveFormat.SampleRate,
                    EqSettings.Frequencies[band],
                    ResolveQ(EqSettings.Frequencies[band], BandWidths[band]),
                    (float)gain);
            }
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
            for (int band = 0; band < _filters.Length; band++)
            {
                sample = _filters[band][channel].Transform(sample);
            }

            buffer[offset + n] = sample;
        }

        return read;
    }

    private static float ResolveQ(float frequency, float bandwidth) =>
        Math.Clamp(frequency / Math.Max(1.0f, bandwidth), 0.1f, 10.0f);
}
