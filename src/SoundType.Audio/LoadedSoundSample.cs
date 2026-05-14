namespace SoundType.Audio;

public enum SoundSampleFormat
{
    Wav,
    Mp3
}

public sealed class LoadedSoundSample
{
    public LoadedSoundSample(string relativePath, SoundSampleFormat format, byte[] data)
    {
        RelativePath = relativePath;
        Format = format;
        Data = data;
    }

    public string RelativePath { get; }
    public SoundSampleFormat Format { get; }
    public byte[] Data { get; }
}
