namespace SoundType.Audio;

public sealed class WaveformPeakCache
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<CacheKey, IReadOnlyList<double>> _peaks = [];

    public IReadOnlyList<double> GetPeaks(LoadedSoundSample sample, int bucketCount = 96)
    {
        CacheKey key = new(sample, bucketCount);
        lock (_syncRoot)
        {
            if (_peaks.TryGetValue(key, out IReadOnlyList<double>? cached))
            {
                return cached;
            }
        }

        IReadOnlyList<double> peaks = WaveformPeakBuilder.BuildPeaks(sample, bucketCount);
        lock (_syncRoot)
        {
            if (_peaks.TryGetValue(key, out IReadOnlyList<double>? cached))
            {
                return cached;
            }

            _peaks[key] = peaks;
            return peaks;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _peaks.Clear();
        }
    }

    private readonly record struct CacheKey(LoadedSoundSample Sample, int BucketCount);
}
