using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaPen = System.Windows.Media.Pen;
using WindowsPoint = System.Windows.Point;

namespace SoundType.App.Controls;

public sealed class WaveformPreviewControl : FrameworkElement
{
    public static readonly DependencyProperty PeaksProperty = DependencyProperty.Register(
        nameof(Peaks),
        typeof(IReadOnlyList<double>),
        typeof(WaveformPreviewControl),
        new FrameworkPropertyMetadata(Array.Empty<double>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double> Peaks
    {
        get => (IReadOnlyList<double>)GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        Rect bounds = new(0, 0, ActualWidth, ActualHeight);
        drawingContext.DrawRoundedRectangle(new SolidColorBrush(MediaColor.FromRgb(16, 24, 39)), null, bounds, 14, 14);

        if (Peaks.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        MediaPen railPen = new(new SolidColorBrush(MediaColor.FromRgb(44, 54, 80)), 1);
        double center = ActualHeight / 2.0;
        drawingContext.DrawLine(railPen, new WindowsPoint(12, center), new WindowsPoint(Math.Max(12, ActualWidth - 12), center));

        double step = Math.Max(2.0, (ActualWidth - 24) / Peaks.Count);
        double barWidth = Math.Max(1.5, Math.Min(5.0, step * 0.54));
        MediaPen peakPen = new(new SolidColorBrush(MediaColor.FromRgb(16, 185, 129)), barWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        MediaPen ghostPen = new(new SolidColorBrush(MediaColor.FromArgb(70, 94, 234, 212)), Math.Max(1.0, barWidth * 0.55))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        for (int i = 0; i < Peaks.Count; i++)
        {
            double peak = Math.Clamp(Peaks[i], 0.02, 1.0);
            double x = 12 + i * step + step / 2.0;
            double halfHeight = peak * (ActualHeight - 24) / 2.0;
            drawingContext.DrawLine(ghostPen, new WindowsPoint(x, center - halfHeight * 0.72), new WindowsPoint(x, center + halfHeight * 0.72));
            drawingContext.DrawLine(peakPen, new WindowsPoint(x, center - halfHeight), new WindowsPoint(x, center + halfHeight));
        }
    }
}
