using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;
using MediaImageSource = System.Windows.Media.ImageSource;

namespace SoundType.App.ViewModels;

internal sealed class RecentAppActivityItem(string processName, string lastSeenText, MediaColor dotColor)
{
    private readonly AppVisual _visual = AppVisual.ForProcess(processName);

    public string ProcessName { get; } = processName;
    public string LastSeenText { get; } = lastSeenText;
    public string IconText => _visual.IconText;
    public MediaFontFamily IconFont => _visual.IconFontFamily;
    public MediaBrush IconForeground => _visual.IconForeground;
    public MediaImageSource? IconSource => _visual.IconSource;
    public MediaBrush DotBrush { get; } = new SolidColorBrush(dotColor);
}
