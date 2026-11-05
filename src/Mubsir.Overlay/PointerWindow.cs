using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Mubsir.Core.Motion;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfPath = System.Windows.Shapes.Path;

namespace Mubsir.Overlay;

/// <summary>
/// The pointer that flies out of the notch to the exact spot on screen, lands with two pulses and says
/// what it is showing. Always click-through: it points at things but never gets in the way of clicking them.
/// </summary>
internal sealed class PointerWindow : Window, IAnimated
{
    // Where the pointer's tip sits inside this small window; the rest is room for the pulse and the label.
    private const double Tip = 30;
    private const double PulseSeconds = 1.1;
    private const int Pulses = 2;
    private readonly FrameLoop _loop;
    private readonly bool _reduce;
    private readonly Spring _x;
    private readonly Spring _y;
    private readonly Spring _fade;
    private readonly Spring _tagFade;
    private readonly RotateTransform _lean = new();
    private readonly WpfEllipse _pulse;
    private readonly ScaleTransform _pulseScale = new(0.6, 0.6);
    private readonly Border _tag;
    private readonly TextBlock _tagText;
    private readonly Canvas _root = new();
    private readonly double _startY;
    private bool _flying;
    private bool _active;
    private double _landedAt = double.PositiveInfinity;
    private double _hideAt = double.PositiveInfinity;

    public PointerWindow(OverlayAssets assets, FrameLoop loop, bool reduceMotion)
    {
        _loop = loop;
        _reduce = reduceMotion;
        Win32.Configure(this);
        Title = "Mubsir pointer";
        Width = 300;
        Height = 110;
        IsHitTestVisible = false;
        SourceInitialized += (_, _) => Win32.SetClickThrough(this, true);

        MotionTokens tokens = assets.Tokens;
        _startY = tokens.NotchSizes[CompanionState.Pointing].Height - 6;
        _x = new Spring(0, tokens.Spring("pointerX"), 0.05) { Instant = reduceMotion };
        _y = new Spring(0, tokens.Spring("pointerY"), 0.05) { Instant = reduceMotion };
        _fade = new Spring(0, tokens.Spring("content")) { Instant = reduceMotion };
        _tagFade = new Spring(0, tokens.Spring("content")) { Instant = reduceMotion };

        Palette p = assets.Palette;
        _pulse = new WpfEllipse
        {
            Width = 44,
            Height = 44,
            Stroke = p.CrestBrush,
            StrokeThickness = 2,
            Opacity = 0,
            RenderTransform = _pulseScale,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        Canvas.SetLeft(_pulse, Tip - 22);
        Canvas.SetTop(_pulse, Tip - 22);
        _root.Children.Add(_pulse);

        Geometry shape = Geometry.Parse("M4 2 L22 12 L13.5 14 L9.5 22 Z");
        shape.Freeze();
        var arrow = new WpfPath
        {
            Data = shape,
            Fill = p.CrestBrush,
            Stroke = p.CreamBrush,
            StrokeThickness = 1.6,
            StrokeLineJoin = PenLineJoin.Round,
            RenderTransform = _lean,
            RenderTransformOrigin = new Point(0, 0),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0, 0, 20), BlurRadius = 8, ShadowDepth = 4, Direction = 270, Opacity = 0.5, RenderingBias = RenderingBias.Performance },
        };

        // The glyph's tip is at (4, 2), so shift it to land exactly on the target.
        Canvas.SetLeft(arrow, Tip - 4);
        Canvas.SetTop(arrow, Tip - 2);
        _root.Children.Add(arrow);

        _tagText = Ui.Text(string.Empty, 12, Brushes.White);
        _tag = new Border
        {
            Background = p.CrestBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(9, 3, 9, 3),
            Child = _tagText,
            Opacity = 0,
        };
        Canvas.SetLeft(_tag, Tip + 22);
        Canvas.SetTop(_tag, Tip + 20);
        _root.Children.Add(_tag);
        _root.Opacity = 0;
        Content = _root;

        new WindowInteropHelper(this).EnsureHandle();
    }

    /// <summary>Flies from the notch to a point on the screen, in physical pixels.</summary>
    public void Fly(double screenX, double screenY, string label)
    {
        Point target = ToDips(screenX, screenY);
        Show();
        _active = true;
        _hideAt = double.PositiveInfinity;
        _landedAt = double.PositiveInfinity;
        _tagText.Text = label;
        _tagFade.Jump(0);
        _x.Jump(SystemParameters.PrimaryScreenWidth / 2);
        _y.Jump(_startY);
        _fade.Jump(0);
        _fade.SetTarget(1);
        _x.SetTarget(target.X);
        _y.SetTarget(target.Y);
        _flying = true;
        _loop.Wake();
    }

    /// <summary>Calls the pointer back to the notch, where it fades away.</summary>
    public void Recall()
    {
        if (!_active || _hideAt < double.PositiveInfinity)
        {
            return;
        }

        _flying = false;
        _landedAt = double.PositiveInfinity;
        _tagFade.SetTarget(0);
        _pulse.Opacity = 0;
        _x.SetTarget(SystemParameters.PrimaryScreenWidth / 2);
        _y.SetTarget(_startY);
        _hideAt = _loop.Now + (_reduce ? 0 : 0.26);
        _loop.Wake();
    }

    public FrameNeed Tick(double now, double dt)
    {
        if (!_active)
        {
            return FrameNeed.None;
        }

        bool moving = _x.Step(dt) | _y.Step(dt) | _fade.Step(dt) | _tagFade.Step(dt);
        if (now >= _hideAt)
        {
            _hideAt = double.PositiveInfinity;
            _fade.SetTarget(0);
            moving = true;
        }

        Left = _x.Value - Tip;
        Top = _y.Value - Tip;
        _lean.Angle = Math.Clamp(_x.Velocity * 0.012, -16, 16);
        _root.Opacity = Math.Clamp(_fade.Value, 0, 1);
        _tag.Opacity = Math.Clamp(_tagFade.Value, 0, 1);

        if (_flying && Math.Abs(_x.Value - _x.Target) < 1.5 && Math.Abs(_y.Value - _y.Target) < 1.5 && Math.Abs(_x.Velocity) < 40)
        {
            _flying = false;
            _landedAt = now;
            _tagFade.SetTarget(1);
            moving = true;
        }

        bool pulsing = false;
        if (now >= _landedAt && !_reduce)
        {
            double t = (now - _landedAt) / PulseSeconds;
            if (t < Pulses)
            {
                double phase = t % 1;
                _pulse.Opacity = 0.8 * (1 - phase);
                _pulseScale.ScaleX = _pulseScale.ScaleY = 0.6 + (0.9 * phase);
                pulsing = true;
            }
            else
            {
                _pulse.Opacity = 0;
                _landedAt = double.PositiveInfinity;
            }
        }

        if (_fade.Target == 0 && _fade.Value < 0.01 && _fade.Velocity == 0)
        {
            _active = false;
            Hide();
            return FrameNeed.None;
        }

        return moving || pulsing || _hideAt < double.PositiveInfinity ? FrameNeed.Fast : FrameNeed.None;
    }

    private Point ToDips(double x, double y)
    {
        Matrix fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return fromDevice.Transform(new Point(x, y));
    }
}
