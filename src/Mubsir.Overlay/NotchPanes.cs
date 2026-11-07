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

    public Action? Tap { get; set; }

    public Action? Cancel { get; set; }

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
        _wave = new VoiceWave(P) { Width = 150, Height = 30 };
        _heard = Ui.Text(string.Empty, 15, P.CreamBrush);
        Set(Column(6, Ends(Row(10, _mark.Element, Ui.Label("Listening", P.AmberBrush)), _wave), _heard));
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

/// <summary>Clean and minimal: one breathing light and the word.</summary>
internal sealed class ThinkingPane : Pane
{
    private readonly ScaleTransform _scale = new(1, 1);

    public ThinkingPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        var fill = new RadialGradientBrush { Center = new Point(0.35, 0.35), GradientOrigin = new Point(0.35, 0.35), RadiusX = 0.75, RadiusY = 0.75 };
        fill.GradientStops.Add(new GradientStop(P.Cream, 0));
        fill.GradientStops.Add(new GradientStop(P.Amber, 0.45));
        fill.GradientStops.Add(new GradientStop(P.Crest, 1));
        fill.Freeze();
        var orb = new WpfEllipse
        {
            Width = 12,
            Height = 12,
            Fill = fill,
            RenderTransform = _scale,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Effect = new DropShadowEffect { Color = P.Amber, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.7 },
        };
        StackPanel row = Row(10, orb, Ui.Text("Thinking", 13.5, P.CreamDim));
        row.HorizontalAlignment = HorizontalAlignment.Center;
        Set(row, new Thickness(0));
    }

    public override FrameNeed Animate(double t, double dt, double voice)
    {
        if (Context.ReduceMotion)
        {
            return FrameNeed.None;
        }

        double dip = 0.5 - (0.5 * Math.Cos(t * Math.PI * 2 / 1.5));
        _scale.ScaleX = _scale.ScaleY = 1 - (0.32 * dip);
        return FrameNeed.Fast;
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
        _label = Ui.Label("Working on it", P.AmberBrush);
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
            _ => ("Working on it", P.AmberBrush),
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

internal sealed class PointingPane : Pane
{
    private readonly TextBlock _text;

    public PointingPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        var box = new Canvas { Width = 26, Height = 26 };
        box.Children.Add(Shape("M4 2 L22 12 L13.5 14 L9.5 22 Z", P.CrestBrush, P.CreamBrush, 1.6));
        var pointer = new Viewbox { Width = 18, Height = 18, Child = box };
        _text = Ui.Text(string.Empty, 14, P.CreamBrush);
        Set(Ends(Row(10, pointer, _text), Ui.Text("Ctrl to stop", 12.5, P.CreamDim)));
    }

    public override void Apply(CompanionContent content)
    {
        _text.Text = content.PointText ?? string.Empty;
        _text.FlowDirection = FlowOf(content.PointText);
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
        Set(Column(6, Ends(Row(10, _mark.Element, Ui.Label("Mubsir", P.AmberBrush)), _wave), _caption, _arabic));
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

internal sealed class ReadingPane : Pane
{
    // The ring's circumference in stroke widths: 2π · 18 / 4.
    private const double RingLength = 28.27;
    private readonly WpfEllipse _ring;
    private readonly Spring _progress;
    private readonly TextBlock _title;
    private readonly TextBlock _place;
    private readonly TextBlock _hint;
    private double _drawn;

    public ReadingPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        var icon = new Grid { Width = 46, Height = 46 };
        icon.Children.Add(new WpfEllipse { Width = 40, Height = 40, StrokeThickness = 4, Stroke = Palette.Solid(P.Amber, 0.2) });
        _ring = new WpfEllipse
        {
            Width = 40,
            Height = 40,
            StrokeThickness = 4,
            Stroke = P.AmberBrush,
            StrokeDashCap = PenLineCap.Round,
            StrokeDashArray = [0, 100],
            RenderTransform = new RotateTransform(-90),
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        icon.Children.Add(_ring);
        _progress = context.NewSpring(0, new SpringToken(0.9, 1));
        _title = Ui.Text(string.Empty, 14.5, P.CreamBrush, FontWeights.SemiBold);
        _place = Ui.Text(string.Empty, 12.5, P.CreamDim);
        _hint = Ui.Text(string.Empty, 12.5, P.AmberBrush);
        Set(Row(10, icon, Column(0, _title, _place, _hint)), new Thickness(22, 8, 22, 8));
    }

    public override void Apply(CompanionContent content)
    {
        _title.Text = content.BookTitle ?? string.Empty;
        _title.FlowDirection = FlowOf(content.BookTitle);
        _place.Text = content.BookPlace ?? string.Empty;
        _place.FlowDirection = FlowOf(content.BookPlace);
        _hint.Text = content.BookHint ?? string.Empty;
        _hint.FlowDirection = FlowOf(content.BookHint);
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

/// <summary>Payment: the real amount and site, and the four Ctrl presses that alone can approve it.</summary>
internal sealed class ConfirmPane : Pane
{
    private readonly TextBlock _site;
    private readonly TextBlock _amount;
    private readonly TextBlock _facts;
    private readonly WpfEllipse[] _taps = new WpfEllipse[4];
    private readonly ScaleTransform[] _tapScale = new ScaleTransform[4];
    private readonly Spring[] _tapPop = new Spring[4];
    private readonly Border _key;
    private readonly Spring _keyHit;
    private readonly Brush _keyRest;
    private int _shown;

    public ConfirmPane(PaneContext context, Size2 size)
        : base(context, size)
    {
        var lockBox = new Canvas { Width = 11, Height = 12 };
        lockBox.Children.Add(Shape("M1 6.5 A1.5 1.5 0 0 1 2.5 5 H8.5 A1.5 1.5 0 0 1 10 6.5 V10 A1.5 1.5 0 0 1 8.5 11.5 H2.5 A1.5 1.5 0 0 1 1 10 Z", P.OkBrush, null));
        lockBox.Children.Add(Shape("M3 5 V3.6 A2.5 2.5 0 0 1 8 3.6 V5", null, P.OkBrush, 1.4));
        _site = Ui.Text(string.Empty, 12.5, P.OkBrush);

        _amount = Ui.Text(string.Empty, 34, P.CreamBrush, FontWeights.SemiBold);
        _facts = Ui.Text(string.Empty, 13.5, P.CreamDim);

        var tapRow = new StackPanel { Orientation = Orientation.Horizontal };
        var popToken = new SpringToken(0.34, 0.5);
        for (int i = 0; i < 4; i++)
        {
            _tapScale[i] = new ScaleTransform(1, 1);
            _tapPop[i] = context.NewSpring(1, popToken);
            _taps[i] = new WpfEllipse
            {
                Width = 14,
                Height = 14,
                StrokeThickness = 2,
                Stroke = P.AmberBrush,
                Margin = new Thickness(i == 0 ? 0 : 9, 0, 0, 0),
                RenderTransform = _tapScale[i],
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            tapRow.Children.Add(_taps[i]);
        }

        _keyRest = Palette.Solid(Colors.White, 0.08);
        _key = new Border
        {
            CornerRadius = new CornerRadius(5),
            BorderBrush = Palette.Solid(Colors.White, 0.18),
            BorderThickness = new Thickness(1, 1, 1, 2),
            Padding = new Thickness(5, 0, 5, 0),
            Margin = new Thickness(4, 0, 4, 0),
            Background = _keyRest,
            Child = Ui.Text("Ctrl", 12, P.CreamBrush),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _keyHit = context.NewSpring(0, new SpringToken(0.25, 1));
        var hint = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 0, 0) };
        hint.Children.Add(Ui.Text("Press", 12.5, P.CreamDim));
        hint.Children.Add(_key);
        hint.Children.Add(Ui.Text("4 times · Esc cancels", 12.5, P.CreamDim));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        Border press = Pill("Press Ctrl", P.AmberBrush, P.AmberBrush, Palette.Solid(Color.FromRgb(0x14, 0x16, 0x2B)), 13, () => Context.Tap?.Invoke(), FontWeights.Medium);
        Border cancel = Pill("Cancel", Brushes.Transparent, Palette.Solid(Colors.White, 0.2), P.CreamBrush, 13, () => Context.Cancel?.Invoke());
        cancel.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(press);
        buttons.Children.Add(cancel);

        var taps = new StackPanel { Orientation = Orientation.Horizontal };
        taps.Children.Add(tapRow);
        taps.Children.Add(hint);

        Set(Column(6, Ends(Ui.Label("Please confirm", P.CrestBrush), Row(5, lockBox, _site)), _amount, _facts, taps, buttons));
    }

    public override void Apply(CompanionContent content)
    {
        _site.Text = content.Site ?? string.Empty;
        _amount.Text = content.Amount ?? string.Empty;
        _facts.Text = content.Facts ?? string.Empty;
        _facts.FlowDirection = FlowOf(content.Facts);
        int taps = Math.Clamp(content.Taps, 0, 4);
        for (int i = 0; i < 4; i++)
        {
            bool on = i < taps;
            _taps[i].Fill = on ? P.CrestBrush : null;
            _taps[i].Stroke = on ? P.CrestBrush : P.AmberBrush;
            if (on && i >= _shown)
            {
                // The new dot pops in, and the key on screen flashes as if pressed.
                _tapPop[i].Jump(0.45);
                _tapPop[i].SetTarget(1);
                _keyHit.Jump(1);
                _keyHit.SetTarget(0);
            }
        }

        _shown = taps;
    }

    public override FrameNeed Animate(double t, double dt, double voice)
    {
        bool moving = false;
        for (int i = 0; i < 4; i++)
        {
            moving |= _tapPop[i].Step(dt);
            _tapScale[i].ScaleX = _tapScale[i].ScaleY = _tapPop[i].Value;
        }

        moving |= _keyHit.Step(dt);
        _key.Background = _keyHit.Value > 0.02 ? Palette.Solid(P.Crest, Math.Clamp(_keyHit.Value, 0, 1)) : _keyRest;
        return moving ? FrameNeed.Fast : FrameNeed.None;
    }
}

/// <summary>A short result line: paid, or stopped and undone.</summary>
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

    public override void Apply(CompanionContent content)
    {
        if (!string.IsNullOrEmpty(content.PaidText))
        {
            _text.Text = content.PaidText;
            _text.FlowDirection = FlowOf(content.PaidText);
        }
    }
}
