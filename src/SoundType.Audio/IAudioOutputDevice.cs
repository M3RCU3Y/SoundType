using NAudio.Wave;

namespace SoundType.Audio;

public interface IAudioOutputDevice : IDisposable
{
    PlaybackState PlaybackState { get; }

    void Init(ISampleProvider provider);

    void Play();

    void Stop();
}

public interface IAudioOutputDeviceFactory
{
    IAudioOutputDevice Create();
}

public sealed class WaveOutAudioOutputDeviceFactory : IAudioOutputDeviceFactory
{
    public IAudioOutputDevice Create() => new WaveOutAudioOutputDevice();
}

public sealed class WaveOutAudioOutputDevice : IAudioOutputDevice
{
    private const int OutputDesiredLatencyMs = 45;
    private const int OutputBufferCount = 3;
    private readonly WaveOutEvent output = new()
    {
        DesiredLatency = OutputDesiredLatencyMs,
        NumberOfBuffers = OutputBufferCount
    };

    public PlaybackState PlaybackState => output.PlaybackState;

    public void Init(ISampleProvider provider) => output.Init(provider);

    public void Play() => output.Play();

    public void Stop() => output.Stop();

    public void Dispose() => output.Dispose();
}
