using NAudio.Wave;
using SoundType.Audio;
using SoundType.Core.Models;
using SoundType.Core.Rules;
using SoundType.Core.Services;

namespace SoundType.Tests;

public sealed class BackendPerformanceTests
{
    [Fact]
    public async Task DebouncedAsyncAction_CoalescesRapidSchedules()
    {
        int runCount = 0;
        await using DebouncedAsyncAction action = new(
            TimeSpan.FromMilliseconds(25),
            _ =>
            {
                Interlocked.Increment(ref runCount);
                return Task.CompletedTask;
            });

        action.Schedule();
        action.Schedule();
        action.Schedule();

        await WaitUntilAsync(() => Volatile.Read(ref runCount) == 1);
        Assert.Equal(1, Volatile.Read(ref runCount));
    }

    [Fact]
    public async Task DebouncedAsyncAction_FlushRunsPendingWorkImmediately()
    {
        int runCount = 0;
        await using DebouncedAsyncAction action = new(
            TimeSpan.FromSeconds(30),
            _ =>
            {
                Interlocked.Increment(ref runCount);
                return Task.CompletedTask;
            });

        action.Schedule();

        await action.FlushAsync();

        Assert.Equal(1, Volatile.Read(ref runCount));
    }

    [Fact]
    public void RuntimePlaybackProfile_IsSnapshotOfSettings()
    {
        AppSettings settings = new();
        settings.ExcludedKeys.Add("A");
        settings.GroupVolumes.Space = 0.5;
        settings.AppRules.Add(new AppRule
        {
            ProcessName = "Code.exe",
            Mode = AppRuleMode.UseSpecificPack,
            SoundPackId = "clicky",
            VolumeOverride = 0.4
        });

        RuntimePlaybackProfile profile = RuntimePlaybackProfile.FromSettings(settings);
        settings.ExcludedKeys.Clear();
        settings.GroupVolumes.Space = 1.0;
        settings.AppRules.Clear();

        Assert.True(profile.IsKeyExcluded("A"));
        Assert.Equal(0.5, profile.GetVolumeForGroup("space"));
        Assert.True(profile.TryGetRule("code.exe", out RuntimeAppRule? rule));
        Assert.NotNull(rule);
        Assert.Equal(AppRuleMode.UseSpecificPack, rule.Mode);
        Assert.Equal("clicky", rule.SoundPackId);
        Assert.Equal(0.4, rule.VolumeOverride);
    }

    [Fact]
    public void VoiceLimiter_RejectsVoicesPastLimitUntilLeaseIsReleased()
    {
        VoiceLimiter limiter = new(maxVoices: 1);

        Assert.True(limiter.TryAcquire(out IDisposable firstLease));
        Assert.False(limiter.TryAcquire(out _));
        Assert.Equal(1, limiter.ActiveVoices);

        firstLease.Dispose();

        Assert.Equal(0, limiter.ActiveVoices);
        Assert.True(limiter.TryAcquire(out IDisposable secondLease));
        secondLease.Dispose();
    }

    [Fact]
    public void ActiveVoiceSampleProvider_ReleasesLeaseWhenSourceFinishes()
    {
        VoiceLimiter limiter = new(maxVoices: 1);
        Assert.True(limiter.TryAcquire(out IDisposable lease));
        ActiveVoiceSampleProvider provider = new(new ArraySampleProvider([0.25f]), lease);
        float[] buffer = new float[1];

        Assert.Equal(1, provider.Read(buffer, 0, buffer.Length));
        Assert.Equal(1, limiter.ActiveVoices);
        Assert.Equal(0, provider.Read(buffer, 0, buffer.Length));

        Assert.Equal(0, limiter.ActiveVoices);
    }

    [Fact]
    public void WaveformPeakCache_ReusesPeaksForSameLoadedSample()
    {
        WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        LoadedSoundSample sample = new(
            "normal/key.wav",
            SoundSampleFormat.Wav,
            [],
            [0.1f, -0.1f, 0.8f, -0.8f],
            format);
        WaveformPeakCache cache = new();

        IReadOnlyList<double> first = cache.GetPeaks(sample);
        IReadOnlyList<double> second = cache.GetPeaks(sample);

        Assert.Same(first, second);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class ArraySampleProvider : ISampleProvider
    {
        private readonly float[] _samples;
        private int _position;

        public ArraySampleProvider(float[] samples)
        {
            _samples = samples;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
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
