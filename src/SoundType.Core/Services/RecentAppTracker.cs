namespace SoundType.Core.Services;

public sealed class RecentAppTracker
{
    public const int DefaultLimit = 20;
    private static readonly TimeSpan DefaultActivityWindow = TimeSpan.FromMinutes(30);

    private readonly int _limit;
    private readonly Dictionary<string, RecentAppEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RecentAppSwitchEvent> _switchEvents = [];
    private readonly object _syncRoot = new();
    private DateTimeOffset _lastTimestampUtc = DateTimeOffset.MinValue;
    private int _nextLane;

    public RecentAppTracker(int limit = DefaultLimit)
    {
        _limit = Math.Max(1, limit);
    }

    public void Record(string? processName)
    {
        string? normalizedName = NormalizeProcessName(processName);
        if (normalizedName is null)
        {
            return;
        }

        lock (_syncRoot)
        {
            DateTimeOffset now = GetNextTimestampUtc();
            string key = normalizedName.ToUpperInvariant();
            if (_entries.TryGetValue(key, out RecentAppEntry? existing))
            {
                _entries[key] = existing with
                {
                    LastSeenUtc = now,
                    SeenCount = existing.SeenCount + 1
                };
            }
            else
            {
                _entries[key] = new RecentAppEntry(normalizedName, now, now, 1);
            }

            _switchEvents.Add(new RecentAppSwitchEvent(normalizedName, now, _nextLane));
            _nextLane = (_nextLane + 1) % 2;
            Prune();
        }
    }

    public IReadOnlyList<RecentAppEntry> ListRecentApps()
    {
        lock (_syncRoot)
        {
            return _entries.Values
                .OrderByDescending(app => app.LastSeenUtc)
                .ThenBy(app => app.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<RecentAppSwitchEvent> ListSwitchEvents(TimeSpan? window = null)
    {
        lock (_syncRoot)
        {
            DateTimeOffset cutoffUtc = DateTimeOffset.UtcNow - (window ?? DefaultActivityWindow);
            return _switchEvents
                .Where(app => app.SeenUtc >= cutoffUtc)
                .OrderBy(app => app.SeenUtc)
                .ToList();
        }
    }

    private void Prune()
    {
        foreach (RecentAppEntry app in _entries.Values.OrderByDescending(app => app.LastSeenUtc).Skip(_limit).ToList())
        {
            _entries.Remove(app.ProcessName.ToUpperInvariant());
        }

        DateTimeOffset cutoffUtc = DateTimeOffset.UtcNow - DefaultActivityWindow;
        _switchEvents.RemoveAll(app => app.SeenUtc < cutoffUtc);
    }

    private DateTimeOffset GetNextTimestampUtc()
    {
        DateTimeOffset timestampUtc = DateTimeOffset.UtcNow;
        if (timestampUtc <= _lastTimestampUtc)
        {
            timestampUtc = _lastTimestampUtc.AddTicks(1);
        }

        _lastTimestampUtc = timestampUtc;
        return timestampUtc;
    }

    private static string? NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        string trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}.exe";
    }
}
