using MediaBrush = System.Windows.Media.Brush;
using MediaFontFamily = System.Windows.Media.FontFamily;

namespace SoundType.App.ViewModels;

internal sealed class RecentAppChipItem(string processName)
{
    private readonly AppVisual _visual = AppVisual.ForProcess(processName);

    public string ProcessName { get; } = processName;
    public string IconText => _visual.IconText;
    public MediaFontFamily IconFont => _visual.IconFontFamily;
    public MediaBrush IconForeground => _visual.IconForeground;

    public override string ToString() => ProcessName;
}
