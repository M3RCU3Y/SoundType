namespace SoundType.Core.Models;

public sealed class RecentAppEntry
{
    public string ProcessName { get; set; } = "";
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public int SeenCount { get; set; }
}
