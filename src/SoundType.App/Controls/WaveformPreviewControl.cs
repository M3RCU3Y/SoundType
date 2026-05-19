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
        SolidColorBrush panelBrush = new(MediaColor.FromArgb(54, 12, 19, 25));
        MediaPen gridPen = new(new SolidColorBrush(MediaColor.FromArgb(92, 44, 53, 62)), 1);
        drawingContext.DrawRectangle(panelBrush, null, bounds);

        if (ActualWidth > 0 && ActualHeight > 0)
        {
            for (int i = 1; i < 6; i++)
            {
                double y = ActualHeight * i / 6.0;
                drawingContext.DrawLine(gridPen, new WindowsPoint(0, y), new WindowsPoint(ActualWidth, y));
            }

            for (int i = 1; i < 6; i++)
            {
                double x = ActualWidth * i / 7.0;
                drawingContext.DrawLine(gridPen, new WindowsPoint(x, 0), new WindowsPoint(x, ActualHeight));
            }
        }

        if (Peaks.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        MediaPen railPen = new(new SolidColorBrush(MediaColor.FromRgb(35, 46, 55)), 1);
        double center = ActualHeight / 2.0;
        drawingContext.DrawLine(railPen, new WindowsPoint(12, center), new WindowsPoint(Math.Max(12, ActualWidth - 12), center));

        double maxPeak = Peaks.Count == 0 ? 0 : Peaks.Max();
        double trimThreshold = Math.Max(0.08, maxPeak * 0.28);
        int firstAudiblePeak = 0;
        for (int i = 0; i < Peaks.Count; i++)
        {
            if (Peaks[i] >= trimThreshold)
            {
                firstAudiblePeak = Math.Max(0, i - 1);
                break;
            }
        }

        int displayCount = Math.Max(1, Peaks.Count - firstAudiblePeak);
        double step = Math.Max(1.0, (ActualWidth - 24) / displayCount);
        double barWidth = Math.Max(1.0, Math.Min(3.0, step * 0.46));
        MediaPen peakPen = new(new SolidColorBrush(MediaColor.FromRgb(63, 215, 150)), barWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        MediaPen ghostPen = new(new SolidColorBrush(MediaColor.FromArgb(62, 94, 234, 212)), Math.Max(1.0, barWidth * 0.55))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        for (int i = firstAudiblePeak; i < Peaks.Count; i++)
        {
            double peak = Math.Clamp(Peaks[i], 0.02, 1.0);
            double x = 12 + (i - firstAudiblePeak) * step + step / 2.0;
            double halfHeight = peak * (ActualHeight - 24) / 2.0;
            drawingContext.DrawLine(ghostPen, new WindowsPoint(x, center - halfHeight * 0.72), new WindowsPoint(x, center + halfHeight * 0.72));
            drawingContext.DrawLine(peakPen, new WindowsPoint(x, center - halfHeight), new WindowsPoint(x, center + halfHeight));
        }
    }
}
