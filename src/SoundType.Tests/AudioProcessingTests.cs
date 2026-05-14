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
            new Dictionary<string, IReadOnlyList<byte[]>>(StringComparer.OrdinalIgnoreCase));

        engine.LoadPack(pack, makeActive: false);

        Assert.True(engine.SetActivePack("soft-laptop"));
        Assert.False(engine.SetActivePack("missing-pack"));
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

    private static float[] CreateSineWave(int count)
    {
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            samples[i] = (float)Math.Sin(i * 0.05) * 0.4f;
        }

        return samples;
    }

    private sealed class ArraySampleProvider : ISampleProvider
    {
        private readonly float[] _samples;
        private int _position;

        public ArraySampleProvider(float[] samples)
        {
            _samples = samples;
        }

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

        public int Read(float[] buffer, int offset, int count)
        {
            int available = Math.Min(count, _samples.Length - _position);
            Array.Copy(_samples, _position, buffer, offset, available);
            _position += available;
            return available;
        }
    }
}
