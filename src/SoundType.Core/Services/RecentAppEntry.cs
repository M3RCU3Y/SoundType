namespace SoundType.Core.Services;

public sealed record RecentAppEntry(
    string ProcessName,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    int SeenCount);
