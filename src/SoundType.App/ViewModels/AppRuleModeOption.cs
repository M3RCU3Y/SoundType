using SoundType.Core.Models;

namespace SoundType.App.ViewModels;

internal sealed class AppRuleModeOption(AppRuleMode mode, string label)
{
    public AppRuleMode Mode { get; } = mode;
    public string Label { get; } = label;

    public override string ToString() => Label;
}
