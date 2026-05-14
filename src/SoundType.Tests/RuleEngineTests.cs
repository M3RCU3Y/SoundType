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
    public void Decide_PlaysDefault_WhenNoAppRuleMatches()
    {
        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), "Code.exe", new AppSettings(), CreatePack());

        Assert.True(decision.ShouldPlay);
        Assert.Equal("normal", decision.SoundGroup);
        Assert.Null(decision.SoundPackId);
        Assert.Equal(1.0, decision.VolumeMultiplier);
    }

    [Fact]
    public void Decide_DefaultRule_PlaysWithoutSoundPackOverride()
    {
        AppSettings settings = new();
        settings.AppRules.Add(new AppRule
        {
            ProcessName = "Code.exe",
            Mode = AppRuleMode.Default,
            SoundPackId = "ignored-pack",
            VolumeOverride = 0.5
        });

        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), "Code.exe", settings, CreatePack());

        Assert.True(decision.ShouldPlay);
        Assert.Equal("normal", decision.SoundGroup);
        Assert.Null(decision.SoundPackId);
        Assert.Equal(0.5, decision.VolumeMultiplier);
    }

    [Fact]
    public void Decide_UseSpecificPack_ReturnsNonEmptySoundPackId()
    {
        AppSettings settings = new();
        settings.AppRules.Add(new AppRule
        {
            ProcessName = "Code.exe",
            Mode = AppRuleMode.UseSpecificPack,
            SoundPackId = "mechanical"
        });

        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), "Code.exe", settings, CreatePack());

        Assert.True(decision.ShouldPlay);
        Assert.Equal("mechanical", decision.SoundPackId);
    }

    [Fact]
    public void Decide_UseSpecificPack_IgnoresBlankSoundPackId()
    {
        AppSettings settings = new();
        settings.AppRules.Add(new AppRule
        {
            ProcessName = "Code.exe",
            Mode = AppRuleMode.UseSpecificPack,
            SoundPackId = " "
        });

        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), "Code.exe", settings, CreatePack());

        Assert.True(decision.ShouldPlay);
        Assert.Null(decision.SoundPackId);
    }

    [Theory]
    [InlineData(-0.2, 0.0)]
    [InlineData(0.8, 0.8)]
    [InlineData(2.0, 1.5)]
    public void Decide_ClampsVolumeOverride_WhenUsed(double overrideVolume, double expectedVolume)
    {
        AppSettings settings = new();
        settings.AppRules.Add(new AppRule
        {
            ProcessName = "Code.exe",
            Mode = AppRuleMode.Default,
            VolumeOverride = overrideVolume
        });

        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), "Code.exe", settings, CreatePack());

        Assert.True(decision.ShouldPlay);
        Assert.Equal(expectedVolume, decision.VolumeMultiplier);
    }

    [Fact]
    public void Decide_EnabledOnlyRule_PlaysForMatchingApp()
    {
        AppSettings settings = new();
        settings.AppRules.Add(new AppRule { ProcessName = "Code.exe", Mode = AppRuleMode.EnabledOnly });

        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), "Code.exe", settings, CreatePack());

        Assert.True(decision.ShouldPlay);
        Assert.Equal("normal", decision.SoundGroup);
    }

    [Fact]
    public void Decide_EnabledOnlyRules_SkipAppsWithoutEnabledOnlyRule()
    {
        AppSettings settings = new();
        settings.AppRules.Add(new AppRule { ProcessName = "Code.exe", Mode = AppRuleMode.EnabledOnly });

        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), "Notepad.exe", settings, CreatePack());

        Assert.False(decision.ShouldPlay);
        Assert.Contains("enabled-only", decision.Reason);
    }

    [Fact]
    public void Decide_NoEnabledOnlyRules_PreservesDefaultGlobalPlayback()
    {
        AppSettings settings = new();
        settings.AppRules.Add(new AppRule { ProcessName = "Code.exe", Mode = AppRuleMode.Default });

        PlaybackDecision decision = _engine.Decide(new("A", "A", KeyCategory.Character), "Notepad.exe", settings, CreatePack());

        Assert.True(decision.ShouldPlay);
        Assert.Equal("normal", decision.SoundGroup);
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
