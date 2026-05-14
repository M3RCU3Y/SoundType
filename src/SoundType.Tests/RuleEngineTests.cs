using SoundType.Core.Models;
using SoundType.Core.Rules;

namespace SoundType.Tests;

public sealed class RuleEngineTests
{
    private readonly RuleEngine _engine = new();

    [Fact]
    public void Decide_Skips_WhenAppIsMuted()
    {
        AppSettings settings = new() { Enabled = false };
        KeyIdentity key = new("A", "A", KeyCategory.Character);

        PlaybackDecision decision = _engine.Decide(key, "Code.exe", settings, CreatePack());

        Assert.False(decision.ShouldPlay);
        Assert.Equal("SoundType is muted.", decision.Reason);
    }

    [Fact]
    public void Decide_Skips_WhenKeyIsExcluded()
    {
        AppSettings settings = new();
        settings.ExcludedKeys.Add("A");

        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), null, settings, CreatePack());

        Assert.False(decision.ShouldPlay);
        Assert.Contains("excluded", decision.Reason);
    }

    [Fact]
    public void Decide_Skips_WhenCurrentAppIsDisabled()
    {
        AppSettings settings = new();
        settings.AppRules.Add(new AppRule { ProcessName = "Code.exe", Mode = AppRuleMode.Disabled });

        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), "Code.exe", settings, CreatePack());

        Assert.False(decision.ShouldPlay);
        Assert.Contains("Code.exe", decision.Reason);
    }

    [Fact]
    public void Decide_UsesEnterGroup_WhenAvailable()
    {
        PlaybackDecision decision = _engine.Decide(new("Enter", "Enter", KeyCategory.Special), null, new AppSettings(), CreatePack());

        Assert.True(decision.ShouldPlay);
        Assert.Equal("enter", decision.SoundGroup);
    }

    [Fact]
    public void Decide_FallsBackToNormal_WhenSpecialGroupMissing()
    {
        SoundPackMetadata pack = CreatePack();
        pack.Groups.Remove("space");

        PlaybackDecision decision = _engine.Decide(new("Space", "Space", KeyCategory.Character), null, new AppSettings(), pack);

        Assert.True(decision.ShouldPlay);
        Assert.Equal("normal", decision.SoundGroup);
    }

    private static SoundPackMetadata CreatePack() =>
        new()
        {
            Id = "test",
            Name = "Test",
            Groups =
            {
                ["normal"] = ["normal/key.wav"],
                ["enter"] = ["enter/ding.wav"],
                ["space"] = ["space/space.wav"]
            },
            KeyOverrides =
            {
                ["Enter"] = "enter",
                ["Space"] = "space"
            }
        };
}
