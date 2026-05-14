namespace SoundType.Audio;

public static class AudioSampleTrimmer
{
    public static float[] TrimLeadingSilence(float[] samples, int channels, float threshold = 0.001f)
    {
        if (samples.Length == 0 || channels <= 0)
        {
            return samples;
        }

        int firstAudibleFrame = 0;
        int frameCount = samples.Length / channels;
        for (; firstAudibleFrame < frameCount; firstAudibleFrame++)
        {
            bool audible = false;
            int frameOffset = firstAudibleFrame * channels;
            for (int channel = 0; channel < channels; channel++)
            {
                if (Math.Abs(samples[frameOffset + channel]) >= threshold)
                {
                    audible = true;
                    break;
                }
            }

            if (audible)
            {
                break;
            }
        }

        if (firstAudibleFrame == 0 || firstAudibleFrame >= frameCount)
        {
            return samples;
        }

        int trimSamples = firstAudibleFrame * channels;
        float[] trimmed = new float[samples.Length - trimSamples];
        Array.Copy(samples, trimSamples, trimmed, 0, trimmed.Length);
        return trimmed;
    }
}
