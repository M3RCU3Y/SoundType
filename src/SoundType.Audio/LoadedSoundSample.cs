using NAudio.Wave;

namespace SoundType.Audio;

public enum SoundSampleFormat
{
    Wav,
    Mp3
}

public sealed class LoadedSoundSample
{
    public LoadedSoundSample(string relativePath, SoundSampleFormat format, byte[] data)
        : this(relativePath, format, data, [], WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
    {
    }

    public LoadedSoundSample(string relativePath, SoundSampleFormat format, byte[] data, float[] decodedSamples, WaveFormat waveFormat)
    {
        RelativePath = relativePath;
        Format = format;
        Data = data;
        DecodedSamples = decodedSamples;
        WaveFormat = waveFormat;
    }

    public string RelativePath { get; }
    public SoundSampleFormat Format { get; }
    public byte[] Data { get; }
    public float[] DecodedSamples { get; }
    public WaveFormat WaveFormat { get; }
}
