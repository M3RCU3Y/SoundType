namespace SoundType.Core.Models;

public sealed class AppRule
{
    public string ProcessName { get; set; } = "";
    public AppRuleMode Mode { get; set; } = AppRuleMode.Disabled;
    public string? SoundPackId { get; set; }
    public double? VolumeOverride { get; set; }
}
