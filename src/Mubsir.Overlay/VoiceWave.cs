using System.Windows;
using System.Windows.Media;

namespace Mubsir.Overlay;

/// <summary>
/// The voice wave: three soft ribbons that swell with the voice and taper to nothing at both ends,
/// around one thin bright line. Drawn as vectors, so it is sharp at any display scale.
/// </summary>
internal sealed class VoiceWave : FrameworkElement
{
    private const int Points = 64;
    private const double Tau = Math.PI * 2;
    private readonly Layer[] _layers;
    private readonly Brush _line;
    private double _level = 0.08;
    private double _time;

    public VoiceWave(Palette palette)
    {
        _layers =
        [
            new Layer(1.5, 2.4, 1, Ribbon(palette.Amber, palette.Crest, 0.72)),
            new Layer(2.2, -3.1, 0.72, Ribbon(palette.Crest, palette.Lilac, 0.58)),
            new Layer(3.1, 4.2, 0.46, Ribbon(palette.Cream, palette.Amber, 0.5)),
        ];
        _line = Ribbon(palette.Cream, palette.Cream, 0.85);
        IsHitTestVisible = false;
    }

    public void Update(double time, double level)
    {
        _time = time;
        _level = level;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        double mid = h / 2;
        foreach (Layer layer in _layers)
        {
            dc.DrawGeometry(layer.Fill, null, Build(layer, w, h, mid));
        }

        dc.DrawRectangle(_line, null, new Rect(0, mid - (h * 0.012), w, h * 0.024));
    }

    private StreamGeometry Build(Layer layer, double w, double h, double mid)
    {
        var heights = new double[Points + 1];
        for (int i = 0; i <= Points; i++)
        {
            double x = (double)i / Points;
            double envelope = Math.Pow(Math.Sin(Math.PI * x), 2.2);
            heights[i] = Math.Abs(Math.Sin((Tau * layer.Frequency * x) + (_time * layer.Speed)) * envelope * _level * layer.Amplitude * h * 0.46) + (h * 0.02);
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext c = geometry.Open())
        {
            c.BeginFigure(new Point(0, mid), isFilled: true, isClosed: true);
            for (int i = 0; i <= Points; i++)
            {
                c.LineTo(new Point(w * i / Points, mid - heights[i]), isStroked: false, isSmoothJoin: true);
            }

            for (int i = Points; i >= 0; i--)
            {
                c.LineTo(new Point(w * i / Points, mid + heights[i]), isStroked: false, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static LinearGradientBrush Ribbon(Color from, Color to, double alpha)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        brush.GradientStops.Add(new GradientStop(Palette.WithAlpha(from, 0), 0));
        brush.GradientStops.Add(new GradientStop(Palette.WithAlpha(from, alpha), 0.5));
        brush.GradientStops.Add(new GradientStop(Palette.WithAlpha(to, 0), 1));
        brush.Freeze();
        return brush;
    }

    private sealed record Layer(double Frequency, double Speed, double Amplitude, Brush Fill);
}
