using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class AudioEngine : IAsyncDisposable
{
    private readonly Random _random = new();
    private readonly object _packLock = new();
    private readonly object _mixerLock = new();
    private readonly Dictionary<string, int> _roundRobin = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LoadedSoundPack> _packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly WaveFormat _playbackFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    private readonly MixingSampleProvider _mixer;
    private readonly WaveOutEvent _output;
    private LoadedSoundPack? _activePack;
    private bool _disposed;

    public AudioEngine()
    {
        _mixer = new MixingSampleProvider(_playbackFormat) { ReadFully = true };
        _output = new WaveOutEvent
        {
            DesiredLatency = 18,
            NumberOfBuffers = 2
        };
        _output.Init(_mixer);
        _output.Play();
    }

    public double MasterVolume { get; set; } = 0.75;
    public double PitchVariation { get; set; } = 0.02;
    public EqSettings Eq { get; set; } = new();
    public PanSettings Pan { get; set; } = new();

    public void LoadPack(LoadedSoundPack pack, bool makeActive = true)
    {
        lock (_packLock)
        {
            _packs[pack.Metadata.Id] = pack;
            if (makeActive)
            {
                _activePack = pack;
                lock (_roundRobin)
                {
                    _roundRobin.Clear();
                }
            }
        }
    }

    public bool SetActivePack(string soundPackId)
    {
        lock (_packLock)
        {
            if (!_packs.TryGetValue(soundPackId, out LoadedSoundPack? pack))
            {
                return false;
            }

            _activePack = pack;
            lock (_roundRobin)
            {
                _roundRobin.Clear();
            }
            return true;
        }
    }

    public bool TryEnqueue(PlaybackRequest request) => TryPlay(request);

    public bool TryPlay(PlaybackRequest request)
    {
        if (_disposed)
        {
            return false;
        }

        try
        {
            PlayNow(request);
            return true;
        }
        catch
        {
            // Audio failures should never destabilize the keyboard hook or UI.
            return false;
        }
    }

    public void Preview(string soundGroup = "normal")
    {
        TryPlay(new PlaybackRequest
        {
            SoundGroup = soundGroup,
            Key = new KeyIdentity("Preview", "Preview", KeyCategory.Special)
        });
    }

    private void PlayNow(PlaybackRequest request)
    {
        LoadedSoundPack? pack = ResolvePack(request.SoundPackId);
        if (pack is null)
        {
            return;
        }

        if (!pack.Samples.TryGetValue(request.SoundGroup, out IReadOnlyList<LoadedSoundSample>? samples) || samples.Count == 0)
        {
            if (!pack.Samples.TryGetValue("normal", out samples) || samples.Count == 0)
            {
                return;
            }
        }

        LoadedSoundSample sample = SelectSample(pack.Metadata, request.SoundGroup, samples);
        if (sample.DecodedSamples.Length == 0)
        {
            return;
        }

        ISampleProvider sampleProvider = new LoadedSoundSampleProvider(sample);
        double pitchFactor = ResolvePitchFactor(pack.Metadata.Defaults.PitchVariation);
        if (Math.Abs(pitchFactor - 1.0) > 0.001)
        {
            sampleProvider = new PitchVariationSampleProvider(sampleProvider, pitchFactor);
        }

        if (Eq.Enabled)
        {
            sampleProvider = new MultiBandEqSampleProvider(sampleProvider, Eq);
        }

        double pan = ResolvePan(request.Key);
        if (Math.Abs(pan) > 0.001)
        {
            sampleProvider = new StereoPanSampleProvider(sampleProvider, pan);
        }

        sampleProvider = new VolumeSampleProvider(sampleProvider)
        {
            Volume = (float)Math.Clamp(
                MasterVolume * pack.Metadata.Defaults.Volume * request.VolumeMultiplier * EqOutputTrim(),
                0.0,
                1.0)
        };
        sampleProvider = new LimiterSampleProvider(sampleProvider);

        lock (_mixerLock)
        {
            _mixer.AddMixerInput(sampleProvider);
        }
    }

    private LoadedSoundPack? ResolvePack(string? soundPackId)
    {
        lock (_packLock)
        {
            if (!string.IsNullOrWhiteSpace(soundPackId) &&
                _packs.TryGetValue(soundPackId, out LoadedSoundPack? requestedPack))
            {
                return requestedPack;
            }

            return _activePack;
        }
    }

    private LoadedSoundSample SelectSample(SoundPackMetadata metadata, string group, IReadOnlyList<LoadedSoundSample> samples)
    {
        if (metadata.Defaults.Randomize)
        {
            lock (_random)
            {
                return samples[_random.Next(samples.Count)];
            }
        }

        string roundRobinKey = $"{metadata.Id}:{group}";
        lock (_roundRobin)
        {
            int next = _roundRobin.TryGetValue(roundRobinKey, out int value) ? value : 0;
            _roundRobin[roundRobinKey] = (next + 1) % samples.Count;
            return samples[next];
        }
    }

    private double ResolvePitchFactor(double packVariation)
    {
        double variation = Math.Clamp(Math.Max(PitchVariation, packVariation), 0.0, 0.12);
        if (variation <= 0.0001)
        {
            return 1.0;
        }

        lock (_random)
        {
            return 1.0 + ((_random.NextDouble() * 2.0) - 1.0) * variation;
        }
    }

    private double EqOutputTrim()
    {
        if (!Eq.Enabled)
        {
            return 1.0;
        }

        Eq.Normalize();
        double maxGain = Eq.BandGainsDb.DefaultIfEmpty(0).Max();
        return maxGain <= 0 ? 1.0 : Math.Clamp(1.0 - maxGain / 48.0, 0.65, 1.0);
    }

    private double ResolvePan(KeyIdentity key)
    {
        Pan.Normalize();
        if (!Pan.Enabled || Pan.Strength <= 0)
        {
            return 0;
        }

        double pan = Pan.Mode == PanMode.Random
            ? NextRandomPan()
            : KeyboardPanResolver.Resolve(key);
        return Math.Clamp(pan * Pan.Strength, -1.0, 1.0);
    }

    private double NextRandomPan()
    {
        lock (_random)
        {
            return _random.NextDouble() * 2.0 - 1.0;
        }
    }

    public bool TryGetLoadedPack(string soundPackId, out LoadedSoundPack? pack)
    {
        lock (_packLock)
        {
            return _packs.TryGetValue(soundPackId, out pack);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await Task.Yield();
        lock (_mixerLock)
        {
            _output.Stop();
            _output.Dispose();
        }
    }
}
