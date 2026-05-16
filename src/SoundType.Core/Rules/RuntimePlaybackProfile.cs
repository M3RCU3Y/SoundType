using SoundType.Core.Models;

namespace SoundType.Core.Rules;

public sealed class RuntimePlaybackProfile
{
    private readonly HashSet<string> _excludedKeys;
    private readonly Dictionary<string, RuntimeAppRule> _rulesByProcess;
    private readonly SoundGroupVolumeSettings _groupVolumes;

    private RuntimePlaybackProfile(
        bool enabled,
        bool ignoreKeyRepeats,
        HashSet<string> excludedKeys,
        Dictionary<string, RuntimeAppRule> rulesByProcess,
        bool enabledOnlyModeActive,
        SoundGroupVolumeSettings groupVolumes)
    {
        Enabled = enabled;
        IgnoreKeyRepeats = ignoreKeyRepeats;
        _excludedKeys = excludedKeys;
        _rulesByProcess = rulesByProcess;
        EnabledOnlyModeActive = enabledOnlyModeActive;
        _groupVolumes = groupVolumes;
    }

    public bool Enabled { get; }
    public bool IgnoreKeyRepeats { get; }
    public bool EnabledOnlyModeActive { get; }

    public static RuntimePlaybackProfile FromSettings(AppSettings settings)
    {
        HashSet<string> excludedKeys = new(settings.ExcludedKeys, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, RuntimeAppRule> rulesByProcess = new(StringComparer.OrdinalIgnoreCase);
        bool enabledOnlyModeActive = false;
        foreach (AppRule rule in settings.AppRules)
        {
            enabledOnlyModeActive |= rule.Mode == AppRuleMode.EnabledOnly;
            if (string.IsNullOrWhiteSpace(rule.ProcessName))
            {
                continue;
            }

            rulesByProcess[rule.ProcessName] = new RuntimeAppRule(
                rule.Mode,
                string.IsNullOrWhiteSpace(rule.SoundPackId) ? null : rule.SoundPackId,
                rule.VolumeOverride);
        }

        SoundGroupVolumeSettings groupVolumes = new()
        {
            Normal = settings.GroupVolumes.Normal,
            Enter = settings.GroupVolumes.Enter,
            Space = settings.GroupVolumes.Space,
            Backspace = settings.GroupVolumes.Backspace,
            Tab = settings.GroupVolumes.Tab
        };
        groupVolumes.Clamp();

        return new RuntimePlaybackProfile(
            settings.Enabled,
            settings.IgnoreKeyRepeats,
            excludedKeys,
            rulesByProcess,
            enabledOnlyModeActive,
            groupVolumes);
    }

    public bool IsKeyExcluded(string code) => _excludedKeys.Contains(code);

    public bool TryGetRule(string? processName, out RuntimeAppRule? rule)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            rule = null;
            return false;
        }

        if (_rulesByProcess.TryGetValue(processName, out RuntimeAppRule? found) && found is not null)
        {
            rule = found;
            return true;
        }

        rule = null;
        return false;
    }

    public double GetVolumeForGroup(string? group) => _groupVolumes.GetVolumeForGroup(group);
}

public sealed record RuntimeAppRule(AppRuleMode Mode, string? SoundPackId, double? VolumeOverride);
