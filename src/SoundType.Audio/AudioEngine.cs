using System.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundType.Core.Models;

namespace SoundType.Audio;

public sealed class AudioEngine : IAsyncDisposable
{
    private const int DefaultMaxCachedPacks = 4;
    private readonly Random _random = new();
    private readonly object _packLock = new();
    private readonly object _mixerLock = new();
    private readonly Dictionary<string, int> _roundRobin = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LoadedSoundPack> _packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _packLastUsedTicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly WaveFormat _playbackFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
    private readonly MixingSampleProvider _mixer;
    private readonly WaveOutEvent _output;
    private EqSettings _eq = CreateEqSnapshot(new EqSettings());
    private PanSettings _pan = CreatePanSnapshot(new PanSettings());
    private double _eqOutputTrim = 1.0;
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
    public double PitchVariation { get; set; }
    public EqSettings Eq
    {
        get => CreateEqSnapshot(_eq);
        set
        {
            _eq = CreateEqSnapshot(value);
            _eqOutputTrim = ResolveEqOutputTrim(_eq);
        }
    }

    public PanSettings Pan
    {
        get => CreatePanSnapshot(_pan);
        set => _pan = CreatePanSnapshot(value);
    }

    public int MaxCachedPacks { get; init; } = DefaultMaxCachedPacks;
    public int LoadedPackCount
    {
        get
        {
            lock (_packLock)
            {
                return _packs.Count;
            }
        }
    }

    public void LoadPack(LoadedSoundPack pack, bool makeActive = true)
    {
        lock (_packLock)
        {
            _packs[pack.Metadata.Id] = pack;
            MarkPackUsed(pack.Metadata.Id);
            if (makeActive)
            {
                _activePack = pack;
                lock (_roundRobin)
                {
                    _roundRobin.Clear();
                }
            }

            PruneCachedPacks();
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
            MarkPackUsed(pack.Metadata.Id);
            lock (_roundRobin)
            {
                _roundRobin.Clear();
            }
            PruneCachedPacks();
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

        EqSettings eq = _eq;
        PanSettings panSettings = _pan;
        double eqOutputTrim = _eqOutputTrim;
        double masterVolume = MasterVolume;
        double pitchVariation = PitchVariation;

        double pitchFactor = ResolvePitchFactor(pitchVariation, pack.Metadata.Defaults.PitchVariation);
        ISampleProvider sampleProvider = Math.Abs(pitchFactor - 1.0) > 0.001
            ? new PitchVariationSampleProvider(sample, pitchFactor)
            : new LoadedSoundSampleProvider(sample);

        if (HasAudibleEq(eq))
        {
            sampleProvider = new MultiBandEqSampleProvider(sampleProvider, eq);
        }

        double pan = ResolvePan(panSettings, request.Key);
        if (Math.Abs(pan) > 0.001)
        {
            sampleProvider = new StereoPanSampleProvider(sampleProvider, pan);
        }

        sampleProvider = new VolumeSampleProvider(sampleProvider)
        {
            Volume = (float)Math.Clamp(
                masterVolume * pack.Metadata.Defaults.Volume * request.VolumeMultiplier * eqOutputTrim,
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
                MarkPackUsed(requestedPack.Metadata.Id);
                return requestedPack;
            }

            if (_activePack is not null)
            {
                MarkPackUsed(_activePack.Metadata.Id);
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

    private double ResolvePitchFactor(double pitchVariation, double packVariation)
    {
        double variation = Math.Clamp(Math.Max(pitchVariation, packVariation), 0.0, 0.12);
        if (variation <= 0.0001)
        {
            return 1.0;
        }

        lock (_random)
        {
            return 1.0 + ((_random.NextDouble() * 2.0) - 1.0) * variation;
        }
    }

    private static EqSettings CreateEqSnapshot(EqSettings? settings)
    {
        settings ??= new EqSettings();
        EqSettings snapshot = new()
        {
            Enabled = settings.Enabled,
            BassGainDb = settings.BassGainDb,
            MidGainDb = settings.MidGainDb,
            TrebleGainDb = settings.TrebleGainDb,
            BandGainsDb = settings.BandGainsDb?.ToList() ?? [],
            PresetName = settings.PresetName
        };
        snapshot.Normalize();
        return snapshot;
    }

    private static PanSettings CreatePanSnapshot(PanSettings? settings)
    {
        settings ??= new PanSettings();
        PanSettings snapshot = new()
        {
            Enabled = settings.Enabled,
            Mode = settings.Mode,
            Strength = settings.Strength
        };
        snapshot.Normalize();
        return snapshot;
    }

    private static bool HasAudibleEq(EqSettings eq) =>
        eq.Enabled && eq.BandGainsDb.Any(gain => Math.Abs(gain) > 0.001);

    private static double ResolveEqOutputTrim(EqSettings eq)
    {
        if (!eq.Enabled)
        {
            return 1.0;
        }

        double maxGain = eq.BandGainsDb.DefaultIfEmpty(0).Max();
        return maxGain <= 0 ? 1.0 : Math.Clamp(1.0 - maxGain / 48.0, 0.65, 1.0);
    }

    private double ResolvePan(PanSettings panSettings, KeyIdentity key)
    {
        if (!panSettings.Enabled || panSettings.Strength <= 0)
        {
            return 0;
        }

        double pan = panSettings.Mode == PanMode.Random
            ? NextRandomPan()
            : KeyboardPanResolver.Resolve(key);
        return Math.Clamp(pan * panSettings.Strength, -1.0, 1.0);
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
            bool found = _packs.TryGetValue(soundPackId, out pack);
            if (found)
            {
                MarkPackUsed(soundPackId);
            }

            return found;
        }
    }

    private void MarkPackUsed(string soundPackId) =>
        _packLastUsedTicks[soundPackId] = Stopwatch.GetTimestamp();

    private void PruneCachedPacks()
    {
        int maxCachedPacks = Math.Max(1, MaxCachedPacks);
        while (_packs.Count > maxCachedPacks)
        {
            string? activePackId = _activePack?.Metadata.Id;
            string? oldestPackId = _packLastUsedTicks
                .Where(entry => !entry.Key.Equals(activePackId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Value)
                .Select(entry => entry.Key)
                .FirstOrDefault();

            if (oldestPackId is null)
            {
                return;
            }

            _packs.Remove(oldestPackId);
            _packLastUsedTicks.Remove(oldestPackId);
            RemoveRoundRobinEntries(oldestPackId);
        }
    }

    private void RemoveRoundRobinEntries(string soundPackId)
    {
        string prefix = $"{soundPackId}:";
        lock (_roundRobin)
        {
            foreach (string key in _roundRobin.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _roundRobin.Remove(key);
            }
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
