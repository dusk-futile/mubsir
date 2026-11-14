using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Mubsir.Core.Motion;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfPath = System.Windows.Shapes.Path;

namespace Mubsir.Overlay;

/// <summary>What panes share: the assets, whether motion is off, and where button presses go.</summary>
internal sealed class PaneContext(OverlayAssets assets, bool reduceMotion)
{
    public OverlayAssets Assets { get; } = assets;

    public Palette P { get; } = assets.Palette;

    public bool ReduceMotion { get; } = reduceMotion;

    public SpringToken Content { get; } = assets.Tokens.Spring("content");

    public Action? Stop { get; set; }

    public Spring NewSpring(double value, SpringToken token, double precision = 0.001) =>
        new(value, token, precision) { Instant = ReduceMotion };
}

/// <summary>
/// One state's content inside the notch. Panes cross-fade: the new one comes in with a little blur and
/// scale, and the old one leaves the same way.
/// </summary>
internal abstract class Pane
{
    private readonly ScaleTransform _scale = new(1.05, 1.05);
    private readonly BlurEffect _blur = new() { Radius = 0, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };

    protected Pane(PaneContext context, Size2 size)
    {
        Context = context;
        Size = size;
        Root = new Grid
        {
            Width = size.Width,
            Height = size.Height,
            RenderTransform = _scale,
            RenderTransformOrigin = new Point(0.5, 0),
            Visibility = Visibility.Collapsed,
            Opacity = 0,
        };
        Fade = context.NewSpring(0, context.Content);
        Zoom = context.NewSpring(1.05, context.Content, 0.0005);
        Blur = context.NewSpring(context.Assets.Tokens.ContentBlur, context.Content, 0.02);
    }

    public Grid Root { get; }

    public Size2 Size { get; }

    public Spring Fade { get; }

    public Spring Zoom { get; }

    public Spring Blur { get; }

    /// <summary>True while this is the pane being shown (it may still be fading in).</summary>
    public bool On { get; set; }

    /// <summary>When a pane that was just asked for should start to appear (the notch moves first).</summary>
    public double AppearAt { get; set; } = double.PositiveInfinity;

    protected PaneContext Context { get; }

    protected Palette P => Context.P;

    /// <summary>Takes the app's words for this state.</summary>
    public virtual void Apply(CompanionContent content)
    {
    }

    /// <summary>Per-frame motion of the pane's own content while it is visible.</summary>
    public virtual FrameNeed Animate(double t, double dt, double voice) => FrameNeed.None;

    public void DrawTransition()
    {
        Root.Opacity = Math.Clamp(Fade.Value, 0, 1);
        _scale.ScaleX = _scale.ScaleY = Zoom.Value;
        if (Blur.Value > 0.05)
        {
            _blur.Radius = Blur.Value;
            Root.Effect = _blur;
        }
        else
        {
            Root.Effect = null;
        }
    }

    protected void Set(UIElement content, Thickness? padding = null)
    {
        if (content is FrameworkElement element)
        {
            element.Margin = padding ?? new Thickness(22, 12, 22, 12);
            element.VerticalAlignment = VerticalAlignment.Center;
        }

        Root.Children.Add(content);
    }

    protected static StackPanel Column(double gap, params FrameworkElement[] items)
    {
        var column = new StackPanel { Orientation = Orientation.Vertical };
        for (int i = 0; i < items.Length; i++)
        {
            if (i > 0)
            {
                items[i].Margin = new Thickness(0, gap, 0, 0);
            }

            column.Children.Add(items[i]);
        }

        return column;
    }

    protected static StackPanel Row(double gap, params FrameworkElement[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < items.Length; i++)
        {
            if (i > 0)
            {
                items[i].Margin = new Thickness(gap, 0, 0, 0);
            }

            items[i].VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(items[i]);
        }

        return row;
    }

    /// <summary>A row with one thing on the left and one pushed to the right edge.</summary>
    protected static DockPanel Ends(FrameworkElement left, FrameworkElement right)
    {
        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(right, Dock.Right);
        right.VerticalAlignment = VerticalAlignment.Center;
        left.VerticalAlignment = VerticalAlignment.Center;
        dock.Children.Add(right);
        dock.Children.Add(left);
        return dock;
    }

    protected static FlowDirection FlowOf(string? text)
    {
        foreach (char c in text ?? string.Empty)
        {
            if (char.IsLetter(c))
            {
                return c is >= '֐' and <= 'ࣿ' ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            }
        }

        return FlowDirection.LeftToRight;
    }

    /// <summary>A pill the user can press. The window never takes focus, so it is a plain clickable shape.</summary>
    protected Border Pill(string text, Brush background, Brush border, Brush foreground, double size, Action? action, FontWeight? weight = null)
    {
        TextBlock label = Ui.Text(text, size, foreground, weight);
        var pill = new Border
        {
            CornerRadius = new CornerRadius(999),
            Background = background,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 4, 12, 5),
            Child = label,
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        pill.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            action?.Invoke();
        };
        return pill;
    }

    protected static WpfPath Shape(string data, Brush? fill, Brush? stroke, double thickness = 0)
    {
        Geometry geometry = Geometry.Parse(data);
        geometry.Freeze();
        return new WpfPath
        {
            Data = geometry,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeDashCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false,
        };
    }

    protected Grid ResultIcon(Color color, string data, double thickness)
    {
        var icon = new Grid { Width = 22, Height = 22 };
        icon.Children.Add(new WpfEllipse { Fill = Palette.Solid(color, 0.16), Stroke = Palette.Solid(color), StrokeThickness = 1.6 });
        var canvas = new Viewbox { Stretch = Stretch.Uniform };
        var box = new Canvas { Width = 24, Height = 24 };
        box.Children.Add(Shape(data, null, Palette.Solid(color), thickness));
        canvas.Child = box;
        icon.Children.Add(canvas);
        return icon;
    }
}

/// <summary>The notch at rest: one small light on the right.</summary>
internal sealed class IdlePane : Pane
{
    private readonly WpfEllipse _light;
    private readonly ScaleTransform _scale = new(1, 1);
    private double _drawnAt;

    public IdlePane(PaneContext context, Size2 size)
        : base(context, size)
    {
        _light = new WpfEllipse
        {
            Width = 6,
            Height = 6,
            Fill = P.AmberBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0),
            RenderTransform = _scale,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Effect = new DropShadowEffect { Color = P.Amber, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.85 },
        };
        Root.Children.Add(_light);
    }

    public bool Asleep { get; set; }

    public override FrameNeed Animate(double t, double dt, double voice)
    {
        if (Context.ReduceMotion)
        {
            _light.Opacity = Asleep ? 0.45 : 1;
            return FrameNeed.None;
        }

        // A slow pulse: six updates a second are enough, however fast the clock runs.
        if (t - _drawnAt < 0.16)
        {
            return FrameNeed.Slow;
        }

        _drawnAt = t;
        double period = Asleep ? 6 : 3.6;
        double dip = 0.5 - (0.5 * Math.Cos(t * Math.PI * 2 / period));
        _light.Opacity = (1 - (0.6 * dip)) * (Asleep ? 0.45 : 1);
        _scale.ScaleX = _scale.ScaleY = 1 - (0.22 * dip);
        return FrameNeed.Slow;
    }
}

internal sealed class PeekPane : Pane
{
    public PeekPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        StackPanel row = Row(10, Ui.Mark(P.Amber, 20, 16), Ui.Text("Hold Right Ctrl to talk", 12.5, P.CreamBrush));
        row.HorizontalAlignment = HorizontalAlignment.Center;
        Set(row, new Thickness(0));
    }
}

/// <summary>The app icon's eye with a ring that ripples out of the iris with the voice.</summary>
internal sealed class RippleMark
{
    // The iris in the 30 x 24 mark.
    private const double CenterX = 16.67;
    private const double CenterY = 9.1;
    private const double UnitsToPixels = 30.0 / 72;
    private readonly WpfEllipse _ring;

    public RippleMark(Palette p)
    {
        _ring = new WpfEllipse { Stroke = p.AmberBrush, StrokeThickness = 0.7, Opacity = 0, IsHitTestVisible = false };
        var layer = new Canvas { IsHitTestVisible = false };
        layer.Children.Add(_ring);
        Element = new Grid { Width = 30, Height = 24 };
        Element.Children.Add(Ui.Mark(p.Amber, 30, 24));
        Element.Children.Add(layer);
    }

    public Grid Element { get; }

    public void Animate(double t, double voice, bool reduce)
    {
        if (reduce)
        {
            _ring.Opacity = 0;
            return;
        }

        double phase = (t * 1.15) % 1;
        double r = (10 + (phase * 10)) * UnitsToPixels;
        _ring.Width = _ring.Height = r * 2;
        Canvas.SetLeft(_ring, CenterX - r);
        Canvas.SetTop(_ring, CenterY - r);
        _ring.Opacity = (1 - phase) * 0.55 * Math.Min(1, voice * 1.6);
    }
}

internal sealed class ListeningPane : Pane
{
    private const int TailLength = 54;
    private readonly RippleMark _mark;
    private readonly VoiceWave _wave;
    private readonly TextBlock _heard;

    public ListeningPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        _mark = new RippleMark(P);
        _wave = new VoiceWave(P, listening: true) { Width = 150, Height = 30 };
        _heard = Ui.Text(string.Empty, 15, P.CreamBrush);
        _mark.Element.HorizontalAlignment = HorizontalAlignment.Left;
        Set(Column(6, Ends(_mark.Element, _wave), _heard));
    }

    public override void Apply(CompanionContent content)
    {
        // The newest words matter most, so a long sentence keeps its end on screen.
        string text = content.Transcript ?? string.Empty;
        _heard.FlowDirection = FlowOf(text);
        _heard.Text = text.Length <= TailLength ? text : "…" + text[^TailLength..];
    }

    public override FrameNeed Animate(double t, double dt, double voice)
    {
        bool reduce = Context.ReduceMotion;
        _wave.Update(reduce ? 0 : t, reduce ? 0.4 : voice);
        _mark.Animate(t, voice, reduce);
        return reduce ? FrameNeed.None : FrameNeed.Fast;
    }
}

/// <summary>
/// Thinking: Mubsir's signature. A stroke shaped like the letter م writes itself right to left, amber into
/// coral, with a cream pen tip, then lifts away from where it began, like a pen moving on. The sharp ink has a
/// warm light under it, warm sparks lift off the tip and fade, and the faint outline of the letter breathes.
/// The colours never leave Mubsir's amber, coral and cream. A light passes over the word beside it.
/// </summary>
internal sealed class ThinkingPane : Pane
{
    private const double Draw = 1.4;
    private const double Hold = 0.4;
    private const double Lift = 0.8;
    private const double Cycle = Draw + Hold + Lift;
    private const double SparkLife = 0.9;
    private const int Sparks = 12;

    // The letter's path in a 44 x 22 box, as chained cubic curves: the joining stroke, the round head, the tail.
    private static readonly Point[][] Curves =
    [
        [new(40, 8), new(34, 8), new(29, 9), new(26, 9)],
        [new(26, 9), new(24, 2), new(17, 2), new(18, 8)],
        [new(18, 8), new(19, 12), new(25, 12), new(26, 9)],
        [new(26, 9), new(20, 18), new(7, 20), new(2, 13)],
    ];

    private static readonly Point[] Points = Sample();
    private readonly WpfPath _stroke;
    private readonly WpfPath _glow;
    private readonly WpfPath _ghost;
    private readonly Grid _tip;
    private readonly TranslateTransform _tipAt = new();
    private readonly TranslateTransform _float = new();
    private readonly TranslateTransform _sweep = new(-1, 0);
    private readonly (WpfPath Shape, TranslateTransform At, ScaleTransform Size)[] _sparks = new (WpfPath, TranslateTransform, ScaleTransform)[Sparks];
    private readonly (double Born, double X, double Y, double Vx, double Vy)[] _sparkState = new (double, double, double, double, double)[Sparks];
    private readonly Random _random = new(7);
    private (int From, int To) _drawn = (-1, -1);
    private double _nextSpark;
    private int _spark;

    public ThinkingPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        var mark = new Canvas { Width = 44, Height = 22, RenderTransform = _float };
        _ghost = new WpfPath { Data = Line(0, Points.Length - 1), Stroke = Palette.Solid(P.Cream, 1), StrokeThickness = 3, Opacity = 0.14 };
        Round(_ghost);
        mark.Children.Add(_ghost);

        var ink = new LinearGradientBrush { StartPoint = new Point(1, 0), EndPoint = new Point(0, 1) };
        ink.GradientStops.Add(new GradientStop(P.Amber, 0.15));
        ink.GradientStops.Add(new GradientStop(P.Crest, 0.95));
        ink.Freeze();

        // The light under the ink: the same line, wider and soft. Only this small shape is blurred.
        _glow = new WpfPath { Stroke = ink, StrokeThickness = 6, Opacity = 0.35, Effect = new BlurEffect { Radius = 5, RenderingBias = RenderingBias.Performance } };
        Round(_glow);
        mark.Children.Add(_glow);
        _stroke = new WpfPath { Stroke = ink, StrokeThickness = 3 };
        Round(_stroke);
        mark.Children.Add(_stroke);

        for (int i = 0; i < Sparks; i++)
        {
            var at = new TranslateTransform();
            var scale = new ScaleTransform(1, 1);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(at);
            var spark = Shape("M2 0 C2.15 1.4 2.6 1.85 4 2 C2.6 2.15 2.15 2.6 2 4 C1.85 2.6 1.4 2.15 0 2 C1.4 1.85 1.85 1.4 2 0 Z", Palette.Solid(i % 3 == 0 ? P.Amber : i % 3 == 1 ? P.Cream : P.Crest), null);
            spark.RenderTransform = group;
            spark.Opacity = 0;
            mark.Children.Add(spark);
            _sparks[i] = (spark, at, scale);
            _sparkState[i] = (double.NegativeInfinity, 0, 0, 0, 0);
        }

        // The pen tip: a cream point inside a soft halo (a radial fade, so no blur is needed).
        var halo = new RadialGradientBrush();
        halo.GradientStops.Add(new GradientStop(Palette.WithAlpha(P.Amber, 0.5), 0));
        halo.GradientStops.Add(new GradientStop(Palette.WithAlpha(P.Amber, 0), 1));
        halo.Freeze();
        _tip = new Grid { Width = 12, Height = 12, RenderTransform = _tipAt, IsHitTestVisible = false };
        _tip.Children.Add(new WpfEllipse { Fill = halo });
        _tip.Children.Add(new WpfEllipse { Width = 5, Height = 5, Fill = P.CreamBrush });
        mark.Children.Add(_tip);

        var shimmer = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5), RelativeTransform = _sweep };
        Color dim = Palette.WithAlpha(P.Cream, 0.5);
        shimmer.GradientStops.Add(new GradientStop(dim, 0));
        shimmer.GradientStops.Add(new GradientStop(dim, 0.35));
        shimmer.GradientStops.Add(new GradientStop(P.Cream, 0.5));
        shimmer.GradientStops.Add(new GradientStop(dim, 0.65));
        shimmer.GradientStops.Add(new GradientStop(dim, 1));
        TextBlock word = Ui.Text("Thinking", 13.5, shimmer, FontWeights.Medium);

        // Drawn in a 44 x 22 box, shown a quarter larger so the letter reads at a glance.
        var scaled = new Viewbox { Width = 55, Height = 27.5, Child = mark };
        StackPanel row = Row(10, scaled, word);
        row.HorizontalAlignment = HorizontalAlignment.Center;
        Set(row, new Thickness(0));
        Show(0, Points.Length - 1, 1);
    }

    public override FrameNeed Animate(double t, double dt, double voice)
    {
        if (Context.ReduceMotion)
        {
            return FrameNeed.None;
        }

        // Write, hold, then lift away from the start so the tip carries on to the end of the letter.
        double x = t % Cycle;
        double from = 0, to = 1, tip = 1;
        bool writing = x < Draw;
        if (writing)
        {
            to = Ease(x / Draw);
        }
        else if (x >= Draw + Hold)
        {
            from = Ease((x - Draw - Hold) / Lift);
            tip = 1 - (from * from);
        }

        int last = Points.Length - 1;
        Show((int)Math.Round(from * last), (int)Math.Round(to * last), tip);

        // Breath and float.
        _ghost.Opacity = 0.1 + (0.08 * (0.5 + (0.5 * Math.Sin(t * 2.2))));
        _glow.Opacity = 0.28 + (0.17 * (0.5 + (0.5 * Math.Sin(t * 3.1))));
        _float.Y = 0.7 * Math.Sin(t * 1.4);

        // Stardust lifts off the pen tip while it writes, drifts up and fades.
        Point pen = Points[(int)Math.Round(to * last)];
        if (writing && t >= _nextSpark)
        {
            _nextSpark = t + 0.07;
            int i = _spark++ % Sparks;
            double angle = (-Math.PI / 2) + ((_random.NextDouble() - 0.5) * 1.6);
            double speed = 6 + (_random.NextDouble() * 8);
            _sparkState[i] = (t, pen.X, pen.Y, speed * Math.Cos(angle), speed * Math.Sin(angle));
        }

        for (int i = 0; i < Sparks; i++)
        {
            var (born, sx, sy, vx, vy) = _sparkState[i];
            double age = t - born;
            var (shape, at, size) = _sparks[i];
            if (age < 0 || age > SparkLife)
            {
                shape.Opacity = 0;
                continue;
            }

            double life = age / SparkLife;
            at.X = sx - 2 + (vx * age);
            at.Y = sy - 2 + (vy * age) - (4 * age * age);
            size.ScaleX = size.ScaleY = 0.5 + (0.6 * Math.Sin(Math.PI * life));
            shape.Opacity = (1 - life) * 0.9;
        }

        // The light crosses the word in a second, then rests for half a second.
        double phase = t % 1.5;
        _sweep.X = phase <= 1 ? -1 + (2 * phase) : 1;
        return FrameNeed.Fast;
    }

    private void Show(int from, int to, double tipOpacity)
    {
        if (to - from < 2)
        {
            _stroke.Data = null;
            _glow.Data = null;
            _tip.Opacity = 0;
            return;
        }

        if ((from, to) != _drawn)
        {
            _drawn = (from, to);
            StreamGeometry line = Line(from, to);
            _stroke.Data = line;
            _glow.Data = line;
        }

        _tipAt.X = Points[to].X - 6;
        _tipAt.Y = Points[to].Y - 6;
        _tip.Opacity = tipOpacity;
    }

    private static double Ease(double x) => x < 0.5 ? 4 * x * x * x : 1 - (Math.Pow((-2 * x) + 2, 3) / 2);

    private static void Round(WpfPath path)
    {
        path.StrokeStartLineCap = PenLineCap.Round;
        path.StrokeEndLineCap = PenLineCap.Round;
        path.StrokeLineJoin = PenLineJoin.Round;
        path.IsHitTestVisible = false;
    }

    private static StreamGeometry Line(int from, int to)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext c = geometry.Open())
        {
            c.BeginFigure(Points[from], isFilled: false, isClosed: false);
            for (int i = from + 1; i <= to; i++)
            {
                c.LineTo(Points[i], isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>Evenly spaced points along the letter, so the pen moves at a steady speed.</summary>
    private static Point[] Sample()
    {
        var dense = new List<Point>();
        foreach (Point[] c in Curves)
        {
            for (int i = 0; i <= 60; i++)
            {
                double t = i / 60.0, u = 1 - t;
                dense.Add(new Point(
                    (u * u * u * c[0].X) + (3 * u * u * t * c[1].X) + (3 * u * t * t * c[2].X) + (t * t * t * c[3].X),
                    (u * u * u * c[0].Y) + (3 * u * u * t * c[1].Y) + (3 * u * t * t * c[2].Y) + (t * t * t * c[3].Y)));
            }
        }

        var even = new List<Point> { dense[0] };
        double carried = 0;
        for (int i = 1; i < dense.Count; i++)
        {
            carried += (dense[i] - dense[i - 1]).Length;
            if (carried >= 0.5)
            {
                even.Add(dense[i]);
                carried = 0;
            }
        }

        return even.ToArray();
    }
}

/// <summary>A step's icon: a ring, a spinner while it runs, a tick that draws itself when it is finished,
/// and a back arrow when it has been undone.</summary>
internal sealed class StepIcon
{
    private const double TickLength = 6;
    private readonly WpfEllipse _ring;
    private readonly WpfEllipse _arc;
    private readonly RotateTransform _spin = new(0);
    private readonly WpfPath _tick;
    private readonly WpfPath _back;
    private readonly Palette _p;
    private readonly Spring _draw;
    private StepState _state = StepState.Waiting;

    public StepIcon(PaneContext context)
    {
        _p = context.P;
        _draw = context.NewSpring(TickLength, context.Content, 0.01);
        Element = new Canvas { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        _ring = new WpfEllipse { Width = 14, Height = 14, StrokeThickness = 1.5, Stroke = Palette.Solid(_p.Cream, 0.28) };
        _arc = new WpfEllipse
        {
            Width = 14,
            Height = 14,
            StrokeThickness = 2,
            Stroke = _p.AmberBrush,
            StrokeDashArray = [6, 20],
            StrokeDashCap = PenLineCap.Round,
            RenderTransform = _spin,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Opacity = 0,
        };
        _tick = Line("M4.6 8.3 L7 10.6 L11.5 5.7", _p.OkBrush, 2);
        _tick.StrokeDashArray = [TickLength, TickLength];
        _tick.StrokeDashOffset = TickLength;
        _tick.Opacity = 0;
        _back = Line("M5.2 6.6 H9.6 A2.4 2.4 0 0 1 9.6 11.4 H6.6 M6.8 4.8 L5.2 6.6 L6.8 8.4", _p.CrestBrush, 1.8);
        _back.Opacity = 0;
        foreach (UIElement e in new UIElement[] { _ring, _arc })
        {
            Canvas.SetLeft(e, 1);
            Canvas.SetTop(e, 1);
            Element.Children.Add(e);
        }

        Element.Children.Add(_tick);
        Element.Children.Add(_back);
    }

    public Canvas Element { get; }

    public void Set(StepState state)
    {
        if (state == _state)
        {
            return;
        }

        _state = state;
        bool spinning = state is StepState.Running or StepState.Undoing;
        _arc.Opacity = spinning ? 1 : 0;
        _arc.Stroke = state == StepState.Undoing ? _p.CrestBrush : _p.AmberBrush;
        _back.Opacity = state == StepState.Undone ? 1 : 0;
        _ring.Stroke = state switch
        {
            StepState.Done => Palette.Solid(_p.Ok, 0.55),
            StepState.Undone => Palette.Solid(_p.Crest, 0.45),
            _ => Palette.Solid(_p.Cream, 0.28),
        };
        if (state == StepState.Done)
        {
            _tick.Opacity = 1;
            _draw.SetTarget(0);
        }
        else
        {
            _tick.Opacity = 0;
            _draw.Jump(TickLength);
        }

        _tick.StrokeDashOffset = _draw.Value;
    }

    /// <summary>Returns true while the spinner or the tick is moving.</summary>
    public bool Animate(double dt, bool reduce)
    {
        bool moving = false;
        if (_state is StepState.Running or StepState.Undoing && !reduce)
        {
            _spin.Angle = (_spin.Angle + ((_state == StepState.Running ? 450 : -450) * dt)) % 360;
            moving = true;
        }

        if (_draw.Step(dt))
        {
            moving = true;
        }

        _tick.StrokeDashOffset = _draw.Value;
        return moving;
    }

    private static WpfPath Line(string data, Brush stroke, double thickness)
    {
        Geometry geometry = Geometry.Parse(data);
        geometry.Freeze();
        return new WpfPath
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeDashCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
    }
}

/// <summary>The Doing checklist: each step is ticked only after it has really finished.</summary>
internal sealed class DoingPane : Pane
{
    private readonly TextBlock _label;
    private readonly Border _stop;
    private readonly StackPanel _list = new() { Orientation = Orientation.Vertical };
    private readonly List<(StepIcon Icon, TextBlock Text)> _rows = [];

    public DoingPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        _label = Ui.Label("Step 1", P.AmberBrush);
        _stop = Pill("Stop · Ctrl", Palette.Solid(Colors.White, 0.08), Palette.Solid(Colors.White, 0.16), P.CreamBrush, 12, () => Context.Stop?.Invoke());
        _list.Margin = new Thickness(0, 4, 0, 0);
        Set(Column(6, Ends(Row(10, Ui.Mark(P.Amber, 20, 16), _label), _stop), _list));
    }

    public override void Apply(CompanionContent content)
    {
        (string label, Brush brush) = content.Mode switch
        {
            DoingMode.Done => ("Done", P.OkBrush),
            DoingMode.Stopped => ("Stopped", P.OkBrush),
            DoingMode.Undoing => ("Stopping and undoing", P.CrestBrush),
            _ => (StepCount(content.Steps), P.AmberBrush),
        };
        _label.Text = Ui.Tracked(label);
        _label.Foreground = brush;
        _stop.Visibility = content.Mode == DoingMode.Running ? Visibility.Visible : Visibility.Hidden;

        IReadOnlyList<CompanionStep> steps = content.Steps ?? [];
        while (_rows.Count < steps.Count)
        {
            var icon = new StepIcon(Context);
            TextBlock text = Ui.Text(string.Empty, 13.5, P.CreamFaint);
            StackPanel row = Row(10, icon.Element, text);
            row.Margin = new Thickness(0, _rows.Count == 0 ? 0 : 7, 0, 0);
            _list.Children.Add(row);
            _rows.Add((icon, text));
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            (StepIcon icon, TextBlock text) = _rows[i];
            bool used = i < steps.Count;
            ((FrameworkElement)_list.Children[i]).Visibility = used ? Visibility.Visible : Visibility.Collapsed;
            if (!used)
            {
                continue;
            }

            CompanionStep step = steps[i];
            text.Text = step.Text;
            text.FlowDirection = FlowOf(step.Text);
            text.Foreground = step.State switch
            {
                StepState.Running => P.CreamBrush,
                StepState.Done => P.CreamDim,
                StepState.Undoing => P.CrestBrush,
                _ => P.CreamFaint,
            };
            text.TextDecorations = step.State == StepState.Undone ? TextDecorations.Strikethrough : null;
            icon.Set(step.State);
        }
    }

    /// <summary>"Step 3": the step running now, counted from the start of the task.</summary>
    private static string StepCount(IReadOnlyList<CompanionStep>? steps)
    {
        IReadOnlyList<CompanionStep> all = steps ?? [];
        int running = all.ToList().FindIndex(s => s.State == StepState.Running);
        int done = all.Count(s => s.State == StepState.Done);
        return "Step " + (running >= 0 ? running + 1 : Math.Max(1, done));
    }

    public override FrameNeed Animate(double t, double dt, double voice)
    {
        bool moving = false;
        foreach ((StepIcon icon, _) in _rows)
        {
            moving |= icon.Animate(dt, Context.ReduceMotion);
        }

        return moving ? FrameNeed.Fast : FrameNeed.None;
    }
}

internal sealed class SpeakingPane : Pane
{
    private readonly RippleMark _mark;
    private readonly VoiceWave _wave;
    private readonly TextBlock _caption;
    private readonly TextBlock _arabic;
    private readonly Spring _arabicFade;
    private readonly Brush _unspoken;

    public SpeakingPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        _mark = new RippleMark(P);
        _wave = new VoiceWave(P) { Width = 150, Height = 30 };
        _unspoken = Palette.Solid(P.Cream, 0.26);
        _caption = Ui.Text(string.Empty, 15, P.CreamBrush);
        _caption.TextWrapping = TextWrapping.Wrap;
        _arabic = Ui.Text(string.Empty, 15, Palette.Solid(Color.FromRgb(0xF7, 0xDD, 0xB3)));
        _arabic.FlowDirection = FlowDirection.RightToLeft;
        _arabic.TextWrapping = TextWrapping.Wrap;
        _arabic.Opacity = 0;
        _arabicFade = context.NewSpring(0, context.Content);
        _mark.Element.HorizontalAlignment = HorizontalAlignment.Left;
        Set(Column(6, Ends(_mark.Element, _wave), _caption, _arabic));
    }

    public override void Apply(CompanionContent content)
    {
        string text = content.Caption ?? string.Empty;
        _caption.FlowDirection = FlowOf(text);
        _caption.Inlines.Clear();
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int spoken = Math.Clamp(content.CaptionWordsSpoken ?? words.Length, 0, words.Length);
        if (spoken > 0)
        {
            _caption.Inlines.Add(new Run(string.Join(' ', words[..spoken]) + (spoken < words.Length ? " " : string.Empty)));
        }

        if (spoken < words.Length)
        {
            _caption.Inlines.Add(new Run(string.Join(' ', words[spoken..])) { Foreground = _unspoken });
        }

        _arabic.Text = content.CaptionArabic ?? string.Empty;
        _arabicFade.SetTarget(string.IsNullOrEmpty(content.CaptionArabic) ? 0 : 1);
    }

    public override FrameNeed Animate(double t, double dt, double voice)
    {
        bool reduce = Context.ReduceMotion;
        _wave.Update(reduce ? 0 : t, reduce ? 0.4 : voice);
        _mark.Animate(t, voice, reduce);
        bool moving = _arabicFade.Step(dt);
        _arabic.Opacity = Math.Clamp(_arabicFade.Value, 0, 1);
        return reduce && !moving ? FrameNeed.None : FrameNeed.Fast;
    }
}

/// <summary>Reading a book: "Book open" and the page, with a ring that fills as the book goes on.</summary>
internal sealed class ReadingPane : Pane
{
    // The ring's circumference in stroke widths: 2π · 13 / 3.
    private const double RingLength = 27.2;
    private readonly WpfEllipse _ring;
    private readonly Spring _progress;
    private readonly TextBlock _place;
    private double _drawn;

    public ReadingPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        var icon = new Grid { Width = 30, Height = 30 };
        icon.Children.Add(new WpfEllipse { Width = 26, Height = 26, StrokeThickness = 3, Stroke = Palette.Solid(P.Amber, 0.2) });
        _ring = new WpfEllipse
        {
            Width = 26,
            Height = 26,
            StrokeThickness = 3,
            Stroke = P.AmberBrush,
            StrokeDashCap = PenLineCap.Round,
            StrokeDashArray = [0, 100],
            RenderTransform = new RotateTransform(-90),
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        icon.Children.Add(_ring);
        _progress = context.NewSpring(0, new SpringToken(0.9, 1));
        _place = Ui.Text(string.Empty, 13, P.CreamDim);
        Set(Row(12, icon, Column(0, Ui.Text("Book open", 14.5, P.CreamBrush, FontWeights.SemiBold), _place)), new Thickness(20, 6, 20, 6));
    }

    public override void Apply(CompanionContent content)
    {
        _place.Text = content.BookPlace ?? string.Empty;
        _progress.SetTarget(Math.Clamp(content.BookProgress, 0, 1));
    }

    public override FrameNeed Animate(double t, double dt, double voice)
    {
        bool moving = _progress.Step(dt);
        if (Math.Abs(_progress.Value - _drawn) > 0.0005)
        {
            _drawn = _progress.Value;
            _ring.StrokeDashArray = [Math.Max(0, _drawn) * RingLength, 100];
        }

        return moving ? FrameNeed.Fast : FrameNeed.None;
    }
}

/// <summary>A short result line: stopped and undone.</summary>
internal sealed class ResultPane : Pane
{
    private readonly TextBlock _text;

    public ResultPane(PaneContext context, Size2 size, Color color, string icon, double iconThickness, string text)
        : base(context, size)
    {
        _text = Ui.Text(text, 14, P.CreamBrush);
        StackPanel row = Row(10, ResultIcon(color, icon, iconThickness), _text);
        row.HorizontalAlignment = HorizontalAlignment.Center;
        Set(row, new Thickness(0));
    }
}
