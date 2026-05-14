using SoundType.Core.Models;

namespace SoundType.Core.Services;

public sealed class RecentAppTracker
{
    public const int DefaultLimit = 20;

    private readonly Dictionary<string, RecentAppEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();
    private readonly int _limit;
    private DateTime _lastTimestampUtc = DateTime.MinValue;

    public RecentAppTracker(int limit = DefaultLimit)
    {
        _limit = Math.Max(0, limit);
    }

    public void Record(string? processName)
    {
        string? normalizedProcessName = NormalizeProcessName(processName);
        if (normalizedProcessName is null)
        {
            return;
        }

        lock (_syncRoot)
        {
            DateTime seenUtc = GetNextTimestampUtc();
            if (_entries.TryGetValue(normalizedProcessName, out RecentAppEntry? existing))
            {
                existing.LastSeenUtc = seenUtc;
                existing.SeenCount++;
            }
            else
            {
                _entries[normalizedProcessName] = new RecentAppEntry
                {
                    ProcessName = normalizedProcessName,
                    FirstSeenUtc = seenUtc,
                    LastSeenUtc = seenUtc,
                    SeenCount = 1
                };
            }

            PruneToLimit();
        }
    }

    public IReadOnlyList<RecentAppEntry> ListRecentApps()
    {
        lock (_syncRoot)
        {
            return _entries.Values
                .OrderByDescending(entry => entry.LastSeenUtc)
                .Select(Clone)
                .ToList();
        }
    }

    private static string? NormalizeProcessName(string? processName)
    {
        string? trimmedProcessName = processName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedProcessName))
        {
            return null;
        }

        return trimmedProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmedProcessName
            : $"{trimmedProcessName}.exe";
    }

    private DateTime GetNextTimestampUtc()
    {
        DateTime timestampUtc = DateTime.UtcNow;
        if (timestampUtc <= _lastTimestampUtc)
        {
            timestampUtc = _lastTimestampUtc.AddTicks(1);
        }

        _lastTimestampUtc = timestampUtc;
        return timestampUtc;
    }

    private void PruneToLimit()
    {
        if (_entries.Count <= _limit)
        {
            return;
        }

        foreach (RecentAppEntry entry in _entries.Values.OrderByDescending(entry => entry.LastSeenUtc).Skip(_limit).ToList())
        {
            _entries.Remove(entry.ProcessName);
        }
    }

    private static RecentAppEntry Clone(RecentAppEntry entry) =>
        new()
        {
            ProcessName = entry.ProcessName,
            FirstSeenUtc = entry.FirstSeenUtc,
            LastSeenUtc = entry.LastSeenUtc,
            SeenCount = entry.SeenCount
        };
}
