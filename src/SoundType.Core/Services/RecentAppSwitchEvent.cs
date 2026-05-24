namespace SoundType.Core.Services;

public sealed record RecentAppSwitchEvent(
    string ProcessName,
    DateTimeOffset SeenUtc,
    int Lane);
