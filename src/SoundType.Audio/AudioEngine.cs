using System.Collections.Concurrent;
using System.Threading.Channels;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class AudioEngine : IAsyncDisposable
{
    private readonly Channel<PlaybackRequest> _queue = Channel.CreateUnbounded<PlaybackRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentBag<WaveOutEvent> _activeOutputs = [];
    private readonly Random _random = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Dictionary<string, int> _roundRobin = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task _worker;
    private LoadedSoundPack? _activePack;

    public AudioEngine()
    {
        _worker = Task.Run(ProcessQueueAsync);
    }

    public double MasterVolume { get; set; } = 0.75;
    public EqSettings Eq { get; set; } = new();

    public void LoadPack(LoadedSoundPack pack)
    {
        _activePack = pack;
        _roundRobin.Clear();
    }

    public bool TryEnqueue(PlaybackRequest request) => _queue.Writer.TryWrite(request);

    public void Preview(string soundGroup = "normal")
    {
        TryEnqueue(new PlaybackRequest
        {
            SoundGroup = soundGroup,
            Key = new KeyIdentity("Preview", "Preview", KeyCategory.Special)
        });
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (PlaybackRequest request in _queue.Reader.ReadAllAsync(_shutdown.Token))
            {
                try
                {
                    PlayNow(request);
                }
                catch
                {
                    // Audio failures should never destabilize the keyboard hook or UI.
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void PlayNow(PlaybackRequest request)
    {
        LoadedSoundPack? pack = _activePack;
        if (pack is null)
        {
            return;
        }

        if (!pack.Samples.TryGetValue(request.SoundGroup, out IReadOnlyList<byte[]>? samples) || samples.Count == 0)
        {
            if (!pack.Samples.TryGetValue("normal", out samples) || samples.Count == 0)
            {
                return;
            }
        }

        byte[] sample = SelectSample(pack.Metadata, request.SoundGroup, samples);
        MemoryStream stream = new(sample, writable: false);
        WaveFileReader reader = new(stream);
        ISampleProvider sampleProvider = reader.ToSampleProvider();
        if (Eq.Enabled)
        {
            sampleProvider = new ThreeBandEqSampleProvider(sampleProvider, Eq);
        }

        sampleProvider = new VolumeSampleProvider(sampleProvider)
        {
            Volume = (float)Math.Clamp(
                MasterVolume * pack.Metadata.Defaults.Volume * request.VolumeMultiplier * EqOutputTrim(),
                0.0,
                1.0)
        };
        sampleProvider = new LimiterSampleProvider(sampleProvider);

        WaveOutEvent output = new() { DesiredLatency = 60 };
        output.PlaybackStopped += (_, _) =>
        {
            output.Dispose();
            reader.Dispose();
            stream.Dispose();
        };

        _activeOutputs.Add(output);
        output.Init(sampleProvider.ToWaveProvider());
        output.Play();
    }

    private byte[] SelectSample(SoundPackMetadata metadata, string group, IReadOnlyList<byte[]> samples)
    {
        if (metadata.Defaults.Randomize)
        {
            lock (_random)
            {
                return samples[_random.Next(samples.Count)];
            }
        }

        int next = _roundRobin.TryGetValue(group, out int value) ? value : 0;
        _roundRobin[group] = (next + 1) % samples.Count;
        return samples[next];
    }

    private double EqOutputTrim()
    {
        if (!Eq.Enabled)
        {
            return 1.0;
        }

        double maxGain = Math.Max(Eq.BassGainDb, Math.Max(Eq.MidGainDb, Eq.TrebleGainDb));
        return maxGain <= 0 ? 1.0 : Math.Clamp(1.0 - maxGain / 48.0, 0.65, 1.0);
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        _shutdown.Cancel();
        try
        {
            await _worker.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Shutdown is best-effort.
        }

        foreach (WaveOutEvent output in _activeOutputs)
        {
            output.Dispose();
        }

        _shutdown.Dispose();
    }
}
