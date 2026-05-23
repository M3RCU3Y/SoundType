using System.Windows.Media;
using SoundType.Core.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace SoundType.App.ViewModels;

internal sealed class AppRuleListItem(AppRule rule, IReadOnlyDictionary<string, SoundPackMetadata> packsById)
{
    private readonly AppVisual _visual = AppVisual.ForProcess(rule.ProcessName);

    public AppRule Rule { get; } = rule;
    public string ProcessName => Rule.ProcessName;
    public string ProcessInitial => string.IsNullOrWhiteSpace(Rule.ProcessName)
        ? "?"
        : Rule.ProcessName.Trim()[0].ToString().ToUpperInvariant();
    public string ProcessIconText => _visual.IconText;
    public MediaFontFamily ProcessIconFont => _visual.IconFontFamily;
    public MediaBrush ProcessIconForeground => _visual.IconForeground;
    public MediaBrush ProcessIconBackground => _visual.IconBackground;
    public double ProcessIconFontSize => _visual.IconFontSize;
    public string ModeLabel => Rule.Mode switch
    {
        AppRuleMode.Default => "Default",
        AppRuleMode.Disabled => "Disabled",
        AppRuleMode.EnabledOnly => "Enabled Only",
        AppRuleMode.UseSpecificPack => "Use Specific Pack",
        _ => Rule.Mode.ToString()
    };
    public MediaBrush ModeBrush => new SolidColorBrush(Rule.Mode == AppRuleMode.Disabled
        ? MediaColor.FromRgb(240, 109, 119)
        : MediaColor.FromRgb(124, 240, 187));

    public string PackDisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Rule.SoundPackId))
            {
                return "(Default)";
            }

            return packsById.TryGetValue(Rule.SoundPackId, out SoundPackMetadata? pack)
                ? pack.Name
                : Rule.SoundPackId;
        }
    }

    public int VolumePercent => (int)Math.Round(Math.Clamp(Rule.VolumeOverride ?? 1.0, 0.0, 1.5) * 100);
    public string VolumeText => $"{VolumePercent}%";
    public string LastSeenText => ProcessName.ToLowerInvariant() switch
    {
        "discord.exe" => "2m ago",
        "chrome.exe" => "5m ago",
        "obs64.exe" => "12m ago",
        "spotify.exe" => "18m ago",
        "explorer.exe" => "1h ago",
        _ => "Now"
    };
    public bool IsEnabled => Rule.Mode != AppRuleMode.Disabled;

    public override string ToString()
    {
        string pack = string.IsNullOrWhiteSpace(Rule.SoundPackId) ? "" : $" | Pack: {PackDisplayName}";
        string volume = Rule.VolumeOverride is double value ? $" | Volume: {Math.Round(value * 100)}%" : "";
        return $"{Rule.ProcessName} | {ModeLabel}{pack}{volume}";
    }
}
