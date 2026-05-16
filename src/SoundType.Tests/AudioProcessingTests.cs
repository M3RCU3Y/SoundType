using NAudio.Wave;
using SoundType.Audio;
using SoundType.Core.Models;

namespace SoundType.Tests;

public sealed class AudioProcessingTests
{
    [Fact]
    public async Task AudioEngine_SetActivePack_ReturnsWhetherPackWasPreloaded()
    {
        AudioEngine engine = new();
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
        AudioEngine engine = new() { MaxCachedPacks = 2 };

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
        AudioEngine engine = new();
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

    private static LoadedSoundPack CreateLoadedPack(string id)
    {
        WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        LoadedSoundSample sample = new(
            "normal/key.wav",
            SoundSampleFormat.Wav,
            [],
            [0.1f, -0.1f],
            format);

        return new LoadedSoundPack(
            new SoundPackMetadata { Id = id, Name = id },
            new Dictionary<string, IReadOnlyList<LoadedSoundSample>>(StringComparer.OrdinalIgnoreCase)
            {
                ["normal"] = [sample]
            });
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
