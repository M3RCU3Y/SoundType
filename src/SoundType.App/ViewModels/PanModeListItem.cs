using SoundType.Core.Models;

namespace SoundType.App.ViewModels;

internal sealed class PanModeListItem(PanMode mode, string displayName)
{
    public PanMode Mode { get; } = mode;

    public override string ToString() => displayName;
}
