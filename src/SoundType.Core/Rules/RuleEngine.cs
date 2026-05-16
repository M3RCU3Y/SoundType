using SoundType.Core.Models;

namespace SoundType.Core.Rules;

public sealed class RuleEngine
{
    public PlaybackDecision Decide(
        KeyIdentity key,
        string? currentProcessName,
        AppSettings settings,
        SoundPackMetadata? activePack) =>
        Decide(key, currentProcessName, RuntimePlaybackProfile.FromSettings(settings), activePack);

    public PlaybackDecision Decide(
        KeyIdentity key,
        string? currentProcessName,
        RuntimePlaybackProfile profile,
        SoundPackMetadata? activePack)
    {
        if (!profile.Enabled)
        {
            return PlaybackDecision.Skip("SoundType is muted.");
        }

        if (profile.IsKeyExcluded(key.Code))
        {
            return PlaybackDecision.Skip($"{key.DisplayName} is excluded.");
        }

        _ = profile.TryGetRule(currentProcessName, out RuntimeAppRule? appRule);

        if (appRule?.Mode == AppRuleMode.Disabled)
        {
            return PlaybackDecision.Skip($"{currentProcessName} is disabled.");
        }

        if (profile.EnabledOnlyModeActive && appRule?.Mode != AppRuleMode.EnabledOnly)
        {
            return PlaybackDecision.Skip($"{currentProcessName ?? "Current app"} is not enabled-only.");
        }

        string group = ResolveGroup(key, activePack);
        double volumeMultiplier = ResolveVolumeMultiplier(appRule);
        string? soundPackId = ResolveSoundPackId(appRule);
        return PlaybackDecision.Play(group, volumeMultiplier, soundPackId);
    }

    private static double ResolveVolumeMultiplier(RuntimeAppRule? appRule) =>
        appRule?.VolumeOverride is double volumeOverride
            ? Math.Clamp(volumeOverride, 0.0, 1.5)
            : 1.0;

    private static string? ResolveSoundPackId(RuntimeAppRule? appRule)
    {
        if (appRule?.Mode == AppRuleMode.UseSpecificPack &&
            !string.IsNullOrWhiteSpace(appRule.SoundPackId))
        {
            return appRule.SoundPackId;
        }

        return null;
    }

    private static string ResolveGroup(KeyIdentity key, SoundPackMetadata? pack)
    {
        if (pack?.KeyOverrides.TryGetValue(key.Code, out string? overrideGroup) == true &&
            HasGroup(pack, overrideGroup))
        {
            return overrideGroup;
        }

        string preferred = key.Code switch
        {
            "Enter" => "enter",
            "Space" => "space",
            "Backspace" => "backspace",
            "Tab" => "tab",
            _ => "normal"
        };

        return HasGroup(pack, preferred) ? preferred : "normal";
    }

    private static bool HasGroup(SoundPackMetadata? pack, string group) =>
        pack is not null &&
        pack.Groups.TryGetValue(group, out List<string>? files) &&
        files.Count > 0;
}
