using NAudio.Wave;
using SoundType.Audio;
using SoundType.Core.Models;

namespace SoundType.Tests;

public sealed class AudioProcessingTests
{
    [Fact]
    public async Task AudioEngine_SetActivePack_ReturnsWhetherPackWasPreloaded()
    {
        AudioEngine engine = new(new FakeAudioOutputDeviceFactory());
        LoadedSoundPack pack = new(
            new SoundPackMetadata { Id = "soft-laptop", Name = "Soft Laptop" },
            new Dictionary<string, IReadOnlyList<LoadedSoundSample>>(StringComparer.OrdinalIgnoreCase));

        engine.LoadPack(pack, makeActive: false);

        Assert.True(engine.SetActivePack("soft-laptop"));
        Assert.False(engine.SetActivePack("missing-pack"));
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_PrunesInactivePacksPastCacheLimit()
    {
        AudioEngine engine = new(new FakeAudioOutputDeviceFactory()) { MaxCachedPacks = 2 };

        engine.LoadPack(CreateLoadedPack("one"), makeActive: true);
        engine.LoadPack(CreateLoadedPack("two"), makeActive: false);
        engine.LoadPack(CreateLoadedPack("three"), makeActive: false);

        Assert.Equal(2, engine.LoadedPackCount);
        Assert.True(engine.TryGetLoadedPack("one", out _));
        Assert.False(engine.TryGetLoadedPack("two", out _));
        Assert.True(engine.TryGetLoadedPack("three", out _));
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_PruneKeepsSystemPacksLoadedForHotPath()
    {
        AudioEngine engine = new(new FakeAudioOutputDeviceFactory()) { MaxCachedPacks = 2 };

        engine.LoadPack(CreateLoadedPack("system-ding", tags: ["system"]), makeActive: false);
        engine.LoadPack(CreateLoadedPack("one"), makeActive: true);
        engine.LoadPack(CreateLoadedPack("two"), makeActive: false);
        engine.LoadPack(CreateLoadedPack("three"), makeActive: false);

        Assert.True(engine.TryGetLoadedPack("system-ding", out _));
        Assert.Equal(2, engine.LoadedPackCount);
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_TryPlay_RecreatesOutputAfterUnexpectedStop()
    {
        FakeAudioOutputDeviceFactory factory = new();
        AudioEngine engine = new(factory);
        engine.LoadPack(CreateLoadedPack("device-test"));
        FakeAudioOutputDevice firstOutput = factory.CreatedDevices.Single();
        firstOutput.MarkStopped();

        bool played = engine.TryPlay(new PlaybackRequest
        {
            Key = new KeyIdentity("A", "A", KeyCategory.Character),
            SoundGroup = "normal",
            SoundPackId = "device-test"
        });

        Assert.True(played);
        Assert.Equal(2, factory.CreatedDevices.Count);
        Assert.Equal(PlaybackState.Playing, factory.CreatedDevices[^1].PlaybackState);
        await engine.DisposeAsync();
    }

    [Fact]
    public void LimiterSampleProvider_ClampsSamplesToThreshold()
    {
        ArraySampleProvider source = new([-2.0f, -0.25f, 0.25f, 2.0f]);
        LimiterSampleProvider limiter = new(source, threshold: 0.75f);
        float[] buffer = new float[4];

        int read = limiter.Read(buffer, 0, buffer.Length);

        Assert.Equal(4, read);
        Assert.Equal(-0.75f, buffer[0], precision: 5);
        Assert.Equal(-0.25f, buffer[1], precision: 5);
        Assert.Equal(0.25f, buffer[2], precision: 5);
        Assert.Equal(0.75f, buffer[3], precision: 5);
    }

    [Fact]
    public void SoftLimiterSampleProvider_ProtectsOutputBusFromSummedPeaks()
    {
        ArraySampleProvider source = new([-2.0f, -1.0f, -0.25f, 0.25f, 1.0f, 2.0f]);
        SoftLimiterSampleProvider limiter = new(source, ceiling: 0.92f);
        float[] buffer = new float[6];

        int read = limiter.Read(buffer, 0, buffer.Length);

        Assert.Equal(6, read);
        Assert.All(buffer, sample => Assert.InRange(sample, -0.92f, 0.92f));
        Assert.True(Math.Abs(buffer[0]) > Math.Abs(buffer[1]));
        Assert.True(Math.Abs(buffer[1]) > Math.Abs(buffer[2]));
        Assert.Equal(0.25f, buffer[3], precision: 5);
    }

    [Fact]
    public void StereoOutputMeterSampleProvider_ReportsChannelPeaksFromReadSamples()
    {
        ArraySampleProvider source = new([0.1f, -0.5f, -0.7f, 0.2f], channels: 2);
        StereoOutputMeterSampleProvider meter = new(source);
        float[] buffer = new float[4];

        int read = meter.Read(buffer, 0, buffer.Length);

        Assert.Equal(4, read);
        Assert.Equal(0.7f, meter.Level.Left, precision: 5);
        Assert.Equal(0.5f, meter.Level.Right, precision: 5);
        Assert.Equal(buffer, [0.1f, -0.5f, -0.7f, 0.2f]);
    }

    [Fact]
    public void StereoOutputMeterSampleProvider_DropsToSilenceWhenReadContainsNoSamples()
    {
        ArraySampleProvider source = new([0.1f, -0.5f], channels: 2);
        StereoOutputMeterSampleProvider meter = new(source);
        float[] buffer = new float[2];

        Assert.Equal(2, meter.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, meter.Read(buffer, 0, buffer.Length));

        Assert.Equal(0.0f, meter.Level.Left);
        Assert.Equal(0.0f, meter.Level.Right);
    }

    [Fact]
    public void ThreeBandEqSampleProvider_ProcessesSamplesWithoutChangingReadCount()
    {
        ArraySampleProvider source = new(CreateSineWave(512));
        EqSettings settings = new()
        {
            Enabled = true,
            BassGainDb = 3,
            MidGainDb = -2,
            TrebleGainDb = 4
        };
        ThreeBandEqSampleProvider eq = new(source, settings);
        float[] buffer = new float[512];

        int read = eq.Read(buffer, 0, buffer.Length);

        Assert.Equal(512, read);
        Assert.Contains(buffer, sample => Math.Abs(sample) > 0.0001f);
        Assert.All(buffer, sample => Assert.False(float.IsNaN(sample)));
    }

    [Fact]
    public void PitchVariationSampleProvider_SpeedsUpPlaybackWhenFactorIsAboveOne()
    {
        ArraySampleProvider source = new(CreateRamp(100));
        PitchVariationSampleProvider pitch = new(source, speedFactor: 2.0);
        float[] buffer = new float[100];

        int read = pitch.Read(buffer, 0, buffer.Length);

        Assert.InRange(read, 49, 51);
        Assert.Equal(0.0f, buffer[0], precision: 5);
        Assert.Equal(2.0f, buffer[1], precision: 5);
        Assert.Equal(4.0f, buffer[2], precision: 5);
    }

    [Fact]
    public void PitchVariationSampleProvider_SlowsPlaybackWhenFactorIsBelowOne()
    {
        ArraySampleProvider source = new(CreateRamp(10));
        PitchVariationSampleProvider pitch = new(source, speedFactor: 0.5);
        float[] buffer = new float[20];

        int read = pitch.Read(buffer, 0, buffer.Length);

        Assert.Equal(20, read);
        Assert.Equal(0.0f, buffer[0], precision: 5);
        Assert.Equal(0.5f, buffer[1], precision: 5);
        Assert.Equal(1.0f, buffer[2], precision: 5);
    }

    [Fact]
    public void PitchVariationSampleProvider_ReadsLoadedSampleWithoutSourceCopy()
    {
        WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        LoadedSoundSample sample = new(
            "normal/key.wav",
            SoundSampleFormat.Wav,
            [],
            [0f, 0f, 2f, 2f, 4f, 4f],
            format);
        PitchVariationSampleProvider pitch = new(sample, speedFactor: 0.5);
        float[] buffer = new float[12];

        int read = pitch.Read(buffer, 0, buffer.Length);

        Assert.Equal(12, read);
        Assert.Equal([0f, 0f, 1f, 1f, 2f, 2f], buffer.Take(6).ToArray());
    }

    [Fact]
    public async Task AudioEngine_TryPlay_DoesNotMutateLiveEqOrPanSettings()
    {
        AudioEngine engine = new(new FakeAudioOutputDeviceFactory());
        EqSettings eq = new()
        {
            Enabled = true,
            BassGainDb = 3,
            MidGainDb = 0,
            TrebleGainDb = -2
        };
        PanSettings pan = new()
        {
            Enabled = true,
            Strength = 2.0
        };
        engine.Eq = eq;
        engine.Pan = pan;
        engine.LoadPack(CreateLoadedPack("snapshot-test"));

        bool played = engine.TryPlay(new PlaybackRequest
        {
            Key = new KeyIdentity("A", "A", KeyCategory.Character),
            SoundGroup = "normal"
        });

        Assert.True(played);
        Assert.Empty(eq.BandGainsDb);
        Assert.Equal(2.0, pan.Strength);
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_TryPlay_AppliesEqToNormalPlaybackOutput()
    {
        LoadedSoundPack pack = CreateLoadedPack("eq-routing", CreateSineWave(1024));
        FakeAudioOutputDeviceFactory flatFactory = new();
        AudioEngine flatEngine = new(flatFactory) { MasterVolume = 1.0 };
        flatEngine.LoadPack(pack);
        FakeAudioOutputDeviceFactory eqFactory = new();
        AudioEngine eqEngine = new(eqFactory) { MasterVolume = 1.0 };
        EqSettings eq = new();
        eq.SetPreset("Crisp", [-2, -1, 0, 0, 1, 2, 4, 5, 4, 3]);
        eqEngine.Eq = eq;
        eqEngine.LoadPack(pack);

        PlaybackRequest request = new()
        {
            Key = new KeyIdentity("A", "A", KeyCategory.Character),
            SoundGroup = "normal"
        };

        Assert.True(flatEngine.TryPlay(request));
        Assert.True(eqEngine.TryPlay(request));
        float[] flatOutput = ReadOutput(flatFactory.CreatedDevices.Single().Provider, 1024);
        float[] eqOutput = ReadOutput(eqFactory.CreatedDevices.Single().Provider, 1024);

        Assert.True(eqEngine.OutputLevel.Left > 0.0f);
        Assert.True(eqEngine.OutputLevel.Right > 0.0f);
        Assert.Contains(
            eqOutput.Zip(flatOutput, (processed, flat) => Math.Abs(processed - flat)),
            delta => delta > 0.0001f);
        await flatEngine.DisposeAsync();
        await eqEngine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_TryPlay_ThrottlesRepeatedOverlaySounds()
    {
        AudioEngine engine = new(new FakeAudioOutputDeviceFactory());
        engine.LoadPack(CreateLoadedPack("overlay-test"));

        PlaybackRequest request = new()
        {
            Key = new KeyIdentity("Enter", "Enter", KeyCategory.Special),
            SoundGroup = "normal",
            SoundPackId = "overlay-test",
            MinimumPlaybackInterval = TimeSpan.FromMilliseconds(150),
            ThrottleKey = "enter-ding"
        };

        Assert.True(engine.TryPlay(request));
        Assert.False(engine.TryPlay(request));
        Assert.True(engine.TryPlay(new PlaybackRequest
        {
            Key = new KeyIdentity("Space", "Space", KeyCategory.Special),
            SoundGroup = "normal",
            SoundPackId = "overlay-test",
            MinimumPlaybackInterval = TimeSpan.FromMilliseconds(150),
            ThrottleKey = "space-ding"
        }));

        await engine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_ConsistentSampleVariation_ReusesTightSamplePool()
    {
        FakeAudioOutputDeviceFactory factory = new();
        AudioEngine engine = new(factory)
        {
            MasterVolume = 1.0,
            SampleVariationMode = SampleVariationMode.Consistent,
            SampleVariationAmount = 0.0
        };
        engine.LoadPack(CreateLoadedPackWithSamples("variation-tight", [[0.1f, 0.1f], [0.4f, 0.4f], [0.8f, 0.8f]]));

        PlaybackRequest request = new()
        {
            Key = new KeyIdentity("A", "A", KeyCategory.Character),
            SoundGroup = "normal"
        };

        Assert.True(engine.TryPlay(request));
        float[] first = ReadOutput(factory.CreatedDevices.Single().Provider, 2);
        Assert.True(engine.TryPlay(request));
        float[] second = ReadOutput(factory.CreatedDevices.Single().Provider, 2);

        Assert.Equal(first, second);
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_NaturalSampleVariation_RotatesAcrossSamples()
    {
        FakeAudioOutputDeviceFactory factory = new();
        AudioEngine engine = new(factory)
        {
            MasterVolume = 1.0,
            SampleVariationMode = SampleVariationMode.Natural,
            SampleVariationAmount = 1.0
        };
        engine.LoadPack(CreateLoadedPackWithSamples("variation-natural", [[0.1f, 0.1f], [0.4f, 0.4f], [0.8f, 0.8f]]));

        PlaybackRequest request = new()
        {
            Key = new KeyIdentity("A", "A", KeyCategory.Character),
            SoundGroup = "normal"
        };

        Assert.True(engine.TryPlay(request));
        float[] first = ReadOutput(factory.CreatedDevices.Single().Provider, 2);
        Assert.True(engine.TryPlay(request));
        float[] second = ReadOutput(factory.CreatedDevices.Single().Provider, 2);

        Assert.NotEqual(first, second);
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_NaturalSampleVariation_RotatesTightPoolEvenWhenPackRandomizes()
    {
        AudioEngine engine = new(new FakeAudioOutputDeviceFactory())
        {
            SampleVariationMode = SampleVariationMode.Natural,
            SampleVariationAmount = 1.0
        };
        LoadedSoundPack pack = CreateLoadedPackWithSamples(
            "variation-natural-randomized-pack",
            [[0.1f, 0.1f], [0.2f, 0.2f], [0.3f, 0.3f], [0.4f, 0.4f], [0.5f, 0.5f], [0.6f, 0.6f], [0.7f, 0.7f], [0.8f, 0.8f]],
            randomize: true);
        IReadOnlyList<LoadedSoundSample> samples = pack.Samples["normal"];

        IReadOnlyList<string> selectedPaths = Enumerable
            .Range(0, 8)
            .Select(_ => InvokeSelectSample(engine, pack.Metadata, "normal", samples).RelativePath)
            .ToList();

        Assert.Equal(
            [
                "normal/key-0.wav",
                "normal/key-1.wav",
                "normal/key-2.wav",
                "normal/key-3.wav",
                "normal/key-4.wav",
                "normal/key-5.wav",
                "normal/key-0.wav",
                "normal/key-1.wav"
            ],
            selectedPaths);
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_LegacySampleVariation_UsesPreviousFullGroupSelection()
    {
        AudioEngine engine = new(new FakeAudioOutputDeviceFactory())
        {
            SampleVariationMode = SampleVariationMode.Legacy,
            SampleVariationAmount = 0.0
        };
        LoadedSoundPack pack = CreateLoadedPackWithSamples(
            "variation-legacy",
            [[0.1f, 0.1f], [0.4f, 0.4f], [0.8f, 0.8f]],
            randomize: false);
        IReadOnlyList<LoadedSoundSample> samples = pack.Samples["normal"];

        LoadedSoundSample first = InvokeSelectSample(engine, pack.Metadata, "normal", samples);
        LoadedSoundSample second = InvokeSelectSample(engine, pack.Metadata, "normal", samples);
        LoadedSoundSample third = InvokeSelectSample(engine, pack.Metadata, "normal", samples);
        LoadedSoundSample fourth = InvokeSelectSample(engine, pack.Metadata, "normal", samples);

        Assert.Equal("normal/key-0.wav", first.RelativePath);
        Assert.Equal("normal/key-1.wav", second.RelativePath);
        Assert.Equal("normal/key-2.wav", third.RelativePath);
        Assert.Equal("normal/key-0.wav", fourth.RelativePath);
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task AudioEngine_BuiltInMultiSampleGroups_UseDifferentSamplesWithVariation()
    {
        string packsRoot = Path.Combine(FindRepositoryRoot(), "assets", "packs");
        SoundPackLoader loader = new();
        AudioEngine engine = new(new FakeAudioOutputDeviceFactory())
        {
            SampleVariationMode = SampleVariationMode.Natural,
            SampleVariationAmount = 1.0
        };

        IReadOnlyList<SoundPackMetadata> metadata = loader.DiscoverPacks(packsRoot);
        List<string> multiSampleGroups = [];

        foreach (SoundPackMetadata packMetadata in metadata)
        {
            LoadedSoundPack pack = loader.Load(packMetadata);
            foreach ((string group, IReadOnlyList<LoadedSoundSample> samples) in pack.Samples)
            {
                if (samples.Count <= 1)
                {
                    continue;
                }

                multiSampleGroups.Add($"{packMetadata.Id}:{group}");
                LoadedSoundSample first = InvokeSelectSample(engine, packMetadata, group, samples);
                LoadedSoundSample second = InvokeSelectSample(engine, packMetadata, group, samples);

                Assert.NotEqual(first.RelativePath, second.RelativePath);
            }
        }

        Assert.NotEmpty(multiSampleGroups);
        await engine.DisposeAsync();
    }

    [Theory]
    [InlineData(SampleVariationMode.Legacy)]
    [InlineData(SampleVariationMode.Consistent)]
    [InlineData(SampleVariationMode.Natural)]
    [InlineData(SampleVariationMode.Random)]
    public async Task AudioEngine_BuiltInMultiSampleGroups_SelectValidSamplesForEveryVariationMode(SampleVariationMode mode)
    {
        string packsRoot = Path.Combine(FindRepositoryRoot(), "assets", "packs");
        SoundPackLoader loader = new();
        AudioEngine engine = new(new FakeAudioOutputDeviceFactory())
        {
            SampleVariationMode = mode,
            SampleVariationAmount = 0.6
        };

        IReadOnlyList<SoundPackMetadata> metadata = loader.DiscoverPacks(packsRoot);
        List<string> multiSampleGroups = [];

        foreach (SoundPackMetadata packMetadata in metadata)
        {
            LoadedSoundPack pack = loader.Load(packMetadata);
            foreach ((string group, IReadOnlyList<LoadedSoundSample> samples) in pack.Samples)
            {
                if (samples.Count <= 1)
                {
                    continue;
                }

                multiSampleGroups.Add($"{packMetadata.Id}:{group}");
                LoadedSoundSample selected = InvokeSelectSample(engine, packMetadata, group, samples);

                Assert.Contains(samples, sample => sample.RelativePath == selected.RelativePath);
            }
        }

        Assert.NotEmpty(multiSampleGroups);
        await engine.DisposeAsync();
    }

    [Fact]
    public void MultiBandEqSampleProvider_ProcessesTenBandsWithoutChangingReadCount()
    {
        ArraySampleProvider source = new(CreateSineWave(512));
        EqSettings settings = new()
        {
            Enabled = true
        };
        settings.SetPreset("Test", [3, 2, 1, 0, -1, -2, 2, 3, 1, -1]);
        MultiBandEqSampleProvider eq = new(source, settings);
        float[] buffer = new float[512];

        int read = eq.Read(buffer, 0, buffer.Length);

        Assert.Equal(512, read);
        Assert.Contains(buffer, sample => Math.Abs(sample) > 0.0001f);
        Assert.All(buffer, sample => Assert.False(float.IsNaN(sample)));
    }

    [Fact]
    public void MultiBandEqSampleProvider_ChangesSamplesWhenEqIsEnabled()
    {
        float[] input = CreateSineWave(512);
        ArraySampleProvider source = new(input);
        EqSettings settings = new();
        settings.SetPreset("Crisp", [-2, -1, 0, 0, 1, 2, 4, 5, 4, 3]);
        MultiBandEqSampleProvider eq = new(source, settings);
        float[] output = new float[input.Length];

        int read = eq.Read(output, 0, output.Length);

        Assert.Equal(input.Length, read);
        Assert.Contains(
            output.Zip(input, (processed, original) => Math.Abs(processed - original)),
            delta => delta > 0.0001f);
    }

    [Fact]
    public void StereoPanSampleProvider_PansTowardRightChannel()
    {
        ArraySampleProvider source = new([1f, 1f], channels: 2);
        StereoPanSampleProvider pan = new(source, pan: 1.0);
        float[] buffer = new float[2];

        int read = pan.Read(buffer, 0, buffer.Length);

        Assert.Equal(2, read);
        Assert.True(buffer[0] < 0.01f);
        Assert.True(buffer[1] > 0.99f);
    }

    [Fact]
    public void WaveformPeakBuilder_NormalizesBuckets()
    {
        float[] samples = [0.1f, -0.1f, 0.4f, -0.2f, 0.8f, -0.1f, 0.2f, -0.2f];

        IReadOnlyList<double> peaks = WaveformPeakBuilder.BuildPeaks(samples, channels: 2, bucketCount: 4);

        Assert.Equal(4, peaks.Count);
        Assert.Equal(1.0, peaks.Max(), precision: 5);
        Assert.All(peaks, peak => Assert.InRange(peak, 0.0, 1.0));
    }

    [Fact]
    public void AudioSampleTrimmer_RemovesLeadingSilentFrames()
    {
        float[] samples =
        [
            0f, 0f,
            0.0001f, -0.0001f,
            0.08f, -0.07f,
            0.04f, -0.03f
        ];

        float[] trimmed = AudioSampleTrimmer.TrimLeadingSilence(samples, channels: 2, threshold: 0.001f);

        Assert.Equal([0.08f, -0.07f, 0.04f, -0.03f], trimmed);
    }

    [Fact]
    public void AudioSampleTrimmer_RemovesTrailingQuietFrames()
    {
        float[] samples =
        [
            0f, 0f,
            0.08f, -0.07f,
            0.04f, -0.03f,
            0.0001f, -0.0001f
        ];

        float[] trimmed = AudioSampleTrimmer.TrimSilence(samples, channels: 2, leadingThreshold: 0.001f, trailingThreshold: 0.001f);

        Assert.Equal([0.08f, -0.07f, 0.04f, -0.03f], trimmed);
    }

    [Fact]
    public void LoadedSoundSampleProvider_StartsEachPlaybackFromBeginning()
    {
        WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        LoadedSoundSample sample = new(
            "normal/key.wav",
            SoundSampleFormat.Wav,
            [1, 2, 3],
            [0.2f, -0.2f, 0.4f, -0.4f],
            format);

        LoadedSoundSampleProvider first = new(sample);
        LoadedSoundSampleProvider second = new(sample);
        float[] firstBuffer = new float[2];
        float[] secondBuffer = new float[2];

        int firstRead = first.Read(firstBuffer, 0, firstBuffer.Length);
        int secondRead = second.Read(secondBuffer, 0, secondBuffer.Length);

        Assert.Equal(2, firstRead);
        Assert.Equal(2, secondRead);
        Assert.Equal(firstBuffer, secondBuffer);
    }

    private static float[] CreateSineWave(int count)
    {
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            samples[i] = (float)Math.Sin(i * 0.05) * 0.4f;
        }

        return samples;
    }

    private static float[] CreateRamp(int count)
    {
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            samples[i] = i;
        }

        return samples;
    }

    private static float[] ReadOutput(ISampleProvider provider, int count)
    {
        float[] buffer = new float[count];
        int read = provider.Read(buffer, 0, buffer.Length);
        Assert.Equal(count, read);
        return buffer;
    }

    private static LoadedSoundPack CreateLoadedPack(string id, IReadOnlyList<string>? tags = null)
    {
        return CreateLoadedPack(id, [0.1f, -0.1f], tags);
    }

    private static LoadedSoundPack CreateLoadedPack(string id, float[] decodedSamples, IReadOnlyList<string>? tags = null)
    {
        WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        LoadedSoundSample sample = new(
            "normal/key.wav",
            SoundSampleFormat.Wav,
            [],
            decodedSamples,
            format);

        return new LoadedSoundPack(
            new SoundPackMetadata { Id = id, Name = id, Tags = tags?.ToList() ?? [] },
            new Dictionary<string, IReadOnlyList<LoadedSoundSample>>(StringComparer.OrdinalIgnoreCase)
            {
                ["normal"] = [sample]
            });
    }

    private static LoadedSoundPack CreateLoadedPackWithSamples(
        string id,
        IReadOnlyList<float[]> decodedSamples,
        bool randomize = true)
    {
        WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        IReadOnlyList<LoadedSoundSample> samples = decodedSamples
            .Select((sample, index) => new LoadedSoundSample(
                $"normal/key-{index}.wav",
                SoundSampleFormat.Wav,
                [],
                sample,
                format))
            .ToList();

        return new LoadedSoundPack(
            new SoundPackMetadata { Id = id, Name = id, Defaults = new SoundPackDefaults { Randomize = randomize } },
            new Dictionary<string, IReadOnlyList<LoadedSoundSample>>(StringComparer.OrdinalIgnoreCase)
            {
                ["normal"] = samples
            });
    }

    private static LoadedSoundSample InvokeSelectSample(
        AudioEngine engine,
        SoundPackMetadata metadata,
        string group,
        IReadOnlyList<LoadedSoundSample> samples)
    {
        System.Reflection.MethodInfo? method = typeof(AudioEngine).GetMethod(
            "SelectSample",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        return Assert.IsType<LoadedSoundSample>(method.Invoke(engine, [metadata, group, samples]));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SoundType.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SoundType repository root.");
    }

    private sealed class FakeAudioOutputDeviceFactory : IAudioOutputDeviceFactory
    {
        public List<FakeAudioOutputDevice> CreatedDevices { get; } = [];

        public IAudioOutputDevice Create()
        {
            FakeAudioOutputDevice device = new();
            CreatedDevices.Add(device);
            return device;
        }
    }

    private sealed class FakeAudioOutputDevice : IAudioOutputDevice
    {
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
        public ISampleProvider Provider { get; private set; } = new ArraySampleProvider([], channels: 2);

        public void Init(ISampleProvider provider)
        {
            Provider = provider;
        }

        public void Play() => PlaybackState = PlaybackState.Playing;

        public void Stop() => PlaybackState = PlaybackState.Stopped;

        public void MarkStopped() => PlaybackState = PlaybackState.Stopped;

        public void Dispose() => PlaybackState = PlaybackState.Stopped;
    }

    private sealed class ArraySampleProvider : ISampleProvider
    {
        private readonly float[] _samples;
        private int _position;

        public ArraySampleProvider(float[] samples, int channels = 1)
        {
            _samples = samples;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int available = Math.Min(count, _samples.Length - _position);
            Array.Copy(_samples, _position, buffer, offset, available);
            _position += available;
            return available;
        }
    }
}
