using NAudio.Wave;

namespace SoundType.Audio;

public interface IAudioOutputDevice : IDisposable
{
    event EventHandler<StoppedEventArgs>? PlaybackStopped;

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
    private readonly WaveOutEvent output = new()
    {
        DesiredLatency = AudioEngine.OutputDesiredLatencyMs,
        NumberOfBuffers = AudioEngine.OutputBufferCount
    };

    public event EventHandler<StoppedEventArgs>? PlaybackStopped
    {
        add => output.PlaybackStopped += value;
        remove => output.PlaybackStopped -= value;
    }

    public PlaybackState PlaybackState => output.PlaybackState;

    public void Init(ISampleProvider provider) => output.Init(provider);

    public void Play() => output.Play();

    public void Stop() => output.Stop();

    public void Dispose() => output.Dispose();
}
