namespace SoundType.Audio;

public static class WaveformPeakBuilder
{
    public static IReadOnlyList<double> BuildPeaks(LoadedSoundSample sample, int bucketCount = 96) =>
        BuildPeaks(sample.DecodedSamples, sample.WaveFormat.Channels, bucketCount);

    public static IReadOnlyList<double> BuildPeaks(float[] samples, int channels, int bucketCount = 96)
    {
        if (samples.Length == 0 || channels <= 0 || bucketCount <= 0)
        {
            return [];
        }

        int frameCount = samples.Length / channels;
        if (frameCount == 0)
        {
            return [];
        }

        double[] peaks = new double[bucketCount];
        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            int startFrame = bucket * frameCount / bucketCount;
            int endFrame = Math.Max(startFrame + 1, (bucket + 1) * frameCount / bucketCount);
            float peak = 0;
            for (int frame = startFrame; frame < endFrame && frame < frameCount; frame++)
            {
                int sampleOffset = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    peak = Math.Max(peak, Math.Abs(samples[sampleOffset + channel]));
                }
            }

            peaks[bucket] = Math.Clamp(peak, 0.0f, 1.0f);
        }

        double max = peaks.Max();
        if (max <= 0.0001)
        {
            return peaks;
        }

        for (int i = 0; i < peaks.Length; i++)
        {
            peaks[i] = Math.Clamp(peaks[i] / max, 0.0, 1.0);
        }

        return peaks;
    }
}
