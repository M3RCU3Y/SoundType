using SoundType.Core.Models;

namespace SoundType.App.ViewModels;

internal sealed class SampleVariationModeListItem(SampleVariationMode mode, string displayName)
{
    public SampleVariationMode Mode { get; } = mode;

    public override string ToString() => displayName;
}
