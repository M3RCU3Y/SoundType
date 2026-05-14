using SoundType.Core.Models;

namespace SoundType.Core.Rules;

public sealed class RuleEngine
{
    public PlaybackDecision Decide(
        KeyIdentity key,
        string? currentProcessName,
        AppSettings settings,
        SoundPackMetadata? activePack)
    {
        if (!settings.Enabled)
        {
            return PlaybackDecision.Skip("SoundType is muted.");
        }

        if (settings.ExcludedKeys.Contains(key.Code))
        {
            return PlaybackDecision.Skip($"{key.DisplayName} is excluded.");
        }

        AppRule? appRule = settings.AppRules.FirstOrDefault(rule =>
            !string.IsNullOrWhiteSpace(rule.ProcessName) &&
            string.Equals(rule.ProcessName, currentProcessName, StringComparison.OrdinalIgnoreCase));

        if (appRule?.Mode == AppRuleMode.Disabled)
        {
            return PlaybackDecision.Skip($"{currentProcessName} is disabled.");
        }

        string group = ResolveGroup(key, activePack);
        double volumeMultiplier = appRule?.VolumeOverride ?? 1.0;
        return PlaybackDecision.Play(group, volumeMultiplier);
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
