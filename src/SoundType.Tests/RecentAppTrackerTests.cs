using SoundType.Core.Services;

namespace SoundType.Tests;

public sealed class RecentAppTrackerTests
{
    [Fact]
    public void Record_NormalizesProcessNames_ToStableExeDisplay()
    {
        RecentAppTracker tracker = new();

        tracker.Record("  Code  ");
        tracker.Record("notepad.exe");

        IReadOnlyList<RecentAppEntry> apps = tracker.ListRecentApps();

        Assert.Contains(apps, app => app.ProcessName == "Code.exe");
        Assert.Contains(apps, app => app.ProcessName == "notepad.exe");
    }

    [Fact]
    public void Record_UpdatesKnownApp_WithoutDuplicating()
    {
        RecentAppTracker tracker = new();

        tracker.Record("Code");
        RecentAppEntry first = Assert.Single(tracker.ListRecentApps());

        tracker.Record("code.exe");
        RecentAppEntry updated = Assert.Single(tracker.ListRecentApps());

        Assert.Equal("Code.exe", updated.ProcessName);
        Assert.Equal(2, updated.SeenCount);
        Assert.Equal(first.FirstSeenUtc, updated.FirstSeenUtc);
        Assert.True(updated.LastSeenUtc >= first.LastSeenUtc);
    }

    [Fact]
    public void Record_PrunesOldestApps_WhenLimitIsExceeded()
    {
        RecentAppTracker tracker = new(limit: 2);

        tracker.Record("First");
        tracker.Record("Second");
        tracker.Record("Third");

        IReadOnlyList<RecentAppEntry> apps = tracker.ListRecentApps();

        Assert.Equal(["Third.exe", "Second.exe"], apps.Select(app => app.ProcessName));
    }

    [Fact]
    public void ListRecentApps_ReturnsNewestFirst_WhenRecordedRapidly()
    {
        RecentAppTracker tracker = new();

        tracker.Record("First");
        tracker.Record("Second");
        tracker.Record("Third");

        IReadOnlyList<RecentAppEntry> apps = tracker.ListRecentApps();

        Assert.Equal(["Third.exe", "Second.exe", "First.exe"], apps.Select(app => app.ProcessName));
        Assert.True(apps[0].LastSeenUtc > apps[1].LastSeenUtc);
        Assert.True(apps[1].LastSeenUtc > apps[2].LastSeenUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_IgnoresBlankProcessNames(string? processName)
    {
        RecentAppTracker tracker = new();

        tracker.Record(processName);

        Assert.Empty(tracker.ListRecentApps());
    }
}
