using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundType.Core.Models;

namespace SoundType.Audio;

internal static class SoundPackAudioProcessor
{
    private const double TargetPackMedianPeak = 0.68;
    private const double MinimumNormalizationGain = 0.35;
    private const double MaximumNormalizationGain = 4.0;
    private const double LoudSamplePeakWarning = 0.98;
    private const double QuietSamplePeakWarning = 0.08;

    public static readonly WaveFormat PlaybackWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    public static void AddQualityWarning(
        SoundPackValidationResult result,
        string group,
        string relativePath,
        string absolutePath,
        SoundSampleFormat format)
    {
        try
        {
            byte[] data = File.ReadAllBytes(absolutePath);
            float[] decoded = DecodeToPlaybackFormat(format, data);
            if (decoded.Length == 0)
            {
                result.Warnings.Add($"{group}: {relativePath} could not be decoded for loudness analysis.");
                return;
            }

            double peak = FindPeak(decoded);
            if (peak >= LoudSamplePeakWarning)
            {
                result.Warnings.Add($"{group}: {relativePath} is very loud and may clip before SoundType normalizes it.");
            }
            else if (peak <= QuietSamplePeakWarning)
            {
                result.Warnings.Add($"{group}: {relativePath} is very quiet and may sound inconsistent with other packs.");
            }
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or EndOfStreamException or IOException or InvalidOperationException)
        {
            result.Warnings.Add($"{group}: {relativePath} could not be decoded for loudness analysis.");
        }
    }

    public static float[] DecodeToPlaybackFormat(SoundSampleFormat format, byte[] data)
    {
        try
        {
            using MemoryStream stream = new(data, writable: false);
            using WaveStream reader = format switch
            {
                SoundSampleFormat.Wav => new WaveFileReader(stream),
                SoundSampleFormat.Mp3 => new Mp3FileReader(stream),
                _ => throw new InvalidOperationException("Unsupported audio format.")
            };

            ISampleProvider provider = reader.ToSampleProvider();
            provider = EnsureStereo(provider);
            if (provider.WaveFormat.SampleRate != PlaybackWaveFormat.SampleRate)
            {
                provider = new WdlResamplingSampleProvider(provider, PlaybackWaveFormat.SampleRate);
            }

            List<float> samples = [];
            float[] buffer = new float[PlaybackWaveFormat.SampleRate / 10 * PlaybackWaveFormat.Channels];
            int read;
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    samples.Add(buffer[i]);
                }
            }

            float[] trimmed = AudioSampleTrimmer.TrimSilence(samples.ToArray(), PlaybackWaveFormat.Channels);
            CenterStereoSamples(trimmed);
            return trimmed;
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or EndOfStreamException or IOException)
        {
            return [];
        }
    }

    public static void NormalizePackSamples(IEnumerable<LoadedSoundSample> samples)
    {
        List<LoadedSoundSample> decodedSamples = samples
            .Where(sample => sample.DecodedSamples.Length > 0)
            .ToList();
        if (decodedSamples.Count == 0)
        {
            return;
        }

        List<double> peaks = decodedSamples
            .Select(sample => FindPeak(sample.DecodedSamples))
            .Where(peak => peak > 0.0001)
            .Order()
            .ToList();
        if (peaks.Count == 0)
        {
            return;
        }

        double medianPeak = peaks[peaks.Count / 2];
        double gain = Math.Clamp(TargetPackMedianPeak / medianPeak, MinimumNormalizationGain, MaximumNormalizationGain);
        if (Math.Abs(gain - 1.0) < 0.001)
        {
            return;
        }

        foreach (LoadedSoundSample sample in decodedSamples)
        {
            ApplyGain(sample.DecodedSamples, gain);
        }
    }

    private static ISampleProvider EnsureStereo(ISampleProvider provider)
    {
        return provider.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(provider),
            2 => provider,
            _ => throw new InvalidOperationException("SoundType supports mono or stereo samples.")
        };
    }

    private static void CenterStereoSamples(float[] samples)
    {
        for (int i = 0; i + 1 < samples.Length; i += PlaybackWaveFormat.Channels)
        {
            float centered = (samples[i] + samples[i + 1]) * 0.5f;
            samples[i] = centered;
            samples[i + 1] = centered;
        }
    }

    private static double FindPeak(float[] samples)
    {
        double peak = 0;
        foreach (float sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        return peak;
    }

    private static void ApplyGain(float[] samples, double gain)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)Math.Clamp(samples[i] * gain, -1.0, 1.0);
        }
    }
}
