namespace SoundType.Audio;

public static class AudioSampleTrimmer
{
    public static float[] TrimLeadingSilence(float[] samples, int channels, float threshold = 0.001f)
    {
        return TrimSilence(samples, channels, leadingThreshold: threshold, trailingThreshold: 0f);
    }

    public static float[] TrimSilence(
        float[] samples,
        int channels,
        float leadingThreshold = 0.003f,
        float trailingThreshold = 0.0005f)
    {
        if (samples.Length == 0 || channels <= 0)
        {
            return samples;
        }

        leadingThreshold = Math.Max(0f, leadingThreshold);
        trailingThreshold = Math.Max(0f, trailingThreshold);

        int firstAudibleFrame = 0;
        int frameCount = samples.Length / channels;
        for (; firstAudibleFrame < frameCount; firstAudibleFrame++)
        {
            if (FrameExceedsThreshold(samples, channels, firstAudibleFrame, leadingThreshold))
            {
                break;
            }
        }

        if (firstAudibleFrame >= frameCount)
        {
            return [];
        }

        int lastAudibleFrame = frameCount - 1;
        if (trailingThreshold > 0)
        {
            for (; lastAudibleFrame > firstAudibleFrame; lastAudibleFrame--)
            {
                if (FrameExceedsThreshold(samples, channels, lastAudibleFrame, trailingThreshold))
                {
                    break;
                }
            }
        }

        int trimSamples = firstAudibleFrame * channels;
        int sampleCount = ((lastAudibleFrame - firstAudibleFrame) + 1) * channels;
        if (trimSamples == 0 && sampleCount == samples.Length)
        {
            return samples;
        }

        float[] trimmed = new float[sampleCount];
        Array.Copy(samples, trimSamples, trimmed, 0, sampleCount);
        return trimmed;
    }

    private static bool FrameExceedsThreshold(float[] samples, int channels, int frame, float threshold)
    {
        int frameOffset = frame * channels;
        for (int channel = 0; channel < channels; channel++)
        {
            if (Math.Abs(samples[frameOffset + channel]) >= threshold)
            {
                return true;
            }
        }

        return false;
    }
}
