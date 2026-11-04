using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Mubsir.Core.Motion;
using WpfPath = System.Windows.Shapes.Path;

namespace Mubsir.Overlay;

/// <summary>
/// The notch: hangs from the top edge of the screen with concave fillets, and pours out into each
/// state on springs. It is drawn by Mubsir itself, so it looks the same on any PC.
/// </summary>
internal sealed class NotchWindow : Window, IAnimated
{
    private const double SidePad = 50;
    private const double BottomPad = 70;
    private readonly OverlayAssets _assets;
    private readonly FrameLoop _loop;
    private readonly bool _reduce;
    private readonly List<Spring> _springs = [];
    private readonly Spring _width;
    private readonly Spring _height;
    private readonly Spring _radius;
    private readonly Spring _rimLevel;
    private readonly Spring _voice;
    private readonly WpfPath _shape;
    private readonly WpfPath _rim;
    private readonly RotateTransform _rimTurn = new(0, 0.5, 0.5);
    private readonly Canvas _panesLayer = new();
    private readonly Dictionary<CompanionState, Pane> _panes = [];
    private readonly IdlePane _idle;
    private readonly double _center;
    private readonly Random _random = new();
    private readonly ClickThrough _clickThrough;
    private CompanionState _state = CompanionState.Idle;
    private Pane? _shown;
    private double _voiceTarget;
    private double _nextSyllable;
    private bool _hovering;
    private double _drawnWidth = -1;
    private double _drawnHeight = -1;
    private double _drawnRadius = -1;

    public NotchWindow(OverlayAssets assets, FrameLoop loop, bool reduceMotion)
    {
        _assets = assets;
        _loop = loop;
        _reduce = reduceMotion;
        Win32.Configure(this);
        Title = "Mubsir notch";

        MotionTokens tokens = assets.Tokens;
        Width = tokens.NotchSizes.Values.Max(s => s.Width) + (SidePad * 2);
        Height = tokens.NotchSizes.Values.Max(s => s.Height) + BottomPad;
        _center = Width / 2;
        Place();
        SystemParameters.StaticPropertyChanged += OnSystemChanged;

        // The notch starts as a sliver and pours out of the top edge when it first appears.
        _width = NewSpring(64, tokens.Spring("notchWidth"), 0.05);
        _height = NewSpring(0, tokens.Spring("notchHeight"), 0.05);
        _radius = NewSpring(0, tokens.Spring("notchHeight"), 0.05);
        _rimLevel = NewSpring(0, tokens.Spring("glow"));
        _voice = NewSpring(0.08, tokens.Spring("voice"));

        Palette p = assets.Palette;
        var rimFill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1), RelativeTransform = _rimTurn };
        rimFill.GradientStops.Add(new GradientStop(p.Amber, 0));
        rimFill.GradientStops.Add(new GradientStop(p.Crest, 0.33));
        rimFill.GradientStops.Add(new GradientStop(p.Lilac, 0.66));
        rimFill.GradientStops.Add(new GradientStop(p.Teal, 1));
        _rim = new WpfPath
        {
            Fill = rimFill,
            Opacity = 0,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Effect = new BlurEffect { Radius = 10, RenderingBias = RenderingBias.Performance },
        };
        _shape = new WpfPath
        {
            Fill = p.IslandBrush,
            Effect = new DropShadowEffect { Color = Color.FromRgb(0, 0, 12), BlurRadius = 36, ShadowDepth = 16, Direction = 270, Opacity = 0.5, RenderingBias = RenderingBias.Performance },
        };

        var context = new PaneContext(assets, reduceMotion)
        {
            Stop = () => StopClicked?.Invoke(),
            Tap = () => TapClicked?.Invoke(),
            Cancel = () => CancelClicked?.Invoke(),
        };
        IReadOnlyDictionary<CompanionState, Size2> sizes = tokens.NotchSizes;
        _idle = new IdlePane(context, sizes[CompanionState.Idle]);
        Add(CompanionState.Idle, _idle);
        Add(CompanionState.Peek, new PeekPane(context, sizes[CompanionState.Peek]));
        Add(CompanionState.Listening, new ListeningPane(context, sizes[CompanionState.Listening]));
        Add(CompanionState.Thinking, new ThinkingPane(context, sizes[CompanionState.Thinking]));
        Add(CompanionState.Doing, new DoingPane(context, sizes[CompanionState.Doing]));
        Add(CompanionState.Pointing, new PointingPane(context, sizes[CompanionState.Pointing]));
        Add(CompanionState.Speaking, new SpeakingPane(context, sizes[CompanionState.Speaking]));
        Add(CompanionState.Reading, new ReadingPane(context, sizes[CompanionState.Reading]));
        Add(CompanionState.Confirm, new ConfirmPane(context, sizes[CompanionState.Confirm]));
        Add(CompanionState.Paid, new ResultPane(context, sizes[CompanionState.Paid], p.Ok, "M7.4 12.4 L10.6 15.4 L16.8 8.8", 2.2, "Paid"));
        Add(CompanionState.Undone, new ResultPane(context, sizes[CompanionState.Undone], p.Crest, "M8.2 9.4 H14 A3.3 3.3 0 0 1 14 16 H9.6 M10.6 6.8 L8.2 9.4 L10.6 12", 2, "Stopped. Everything is back as it was."));

        // The panes are clipped to the notch, so they are revealed as it opens.
        var notch = new Canvas { Width = Width, Height = Height };
        notch.Children.Add(_rim);

        // The shape and its big soft shadow only change while the notch morphs. Caching them keeps the
        // small resting light from re-rendering the whole shadow every time it pulses.
        var shapeLayer = new Canvas { CacheMode = new BitmapCache { SnapsToDevicePixels = true } };
        shapeLayer.Children.Add(_shape);
        notch.Children.Add(shapeLayer);
        notch.Children.Add(_panesLayer);
        Content = notch;
        Cursor = Cursors.Hand;
        _clickThrough = new ClickThrough(this, () => new Rect(_center - (Math.Max(0, _width.Value) / 2), 0, Math.Max(0, _width.Value), Math.Max(0, _height.Value)));
        _clickThrough.HoverChanged += OnHover;

        notch.MouseLeftButtonUp += (_, e) =>
        {
            if (!e.Handled)
            {
                Clicked?.Invoke();
            }
        };
    }

    public event Action? Clicked;

    public event Action? StopClicked;

    public event Action? TapClicked;

    public event Action? CancelClicked;

    /// <summary>The mouse moved over or off the notch while it rests.</summary>
    public event Action<bool>? Hovered;

    /// <summary>Makes the voice wave move by itself, for the demo; the real app feeds <see cref="SetVoiceLevel"/>.</summary>
    public bool DemoVoice { get; set; }

    public void SetVoiceLevel(double level) => _voiceTarget = Math.Clamp(level, 0, 1);

    /// <summary>The voice level as it moves on screen, for Hudhud to follow.</summary>
    public double VoiceValue => _voice.Value;

    public void SetAsleep(bool asleep)
    {
        _idle.Asleep = asleep;
        _loop.Wake();
    }

    public void Go(CompanionState state, CompanionContent? content)
    {
        _state = state;
        if (content is not null)
        {
            _panes[state].Apply(content);
        }

        if (state == CompanionState.Idle && _hovering)
        {
            state = CompanionState.Peek;
        }

        Morph(state);
        _loop.Wake();
    }

    /// <summary>A small squeeze of the notch, when a key press lands or it is pressed.</summary>
    public void Nudge(double width, double height)
    {
        _width.Kick(width);
        _height.Kick(height);
        _loop.Wake();
    }

    public FrameNeed Tick(double now, double dt)
    {
        bool moving = false;
        foreach (Spring spring in _springs)
        {
            moving |= spring.Step(dt);
        }

        DriveVoice(now);
        Redraw(now);

        FrameNeed need = moving ? FrameNeed.Fast : FrameNeed.None;
        foreach (Pane pane in _panes.Values)
        {
            if (pane.Root.Visibility != Visibility.Visible && !(pane.On && now >= pane.AppearAt))
            {
                continue;
            }

            if (pane.On && now >= pane.AppearAt)
            {
                pane.AppearAt = double.PositiveInfinity;
                pane.Root.Visibility = Visibility.Visible;
                pane.Fade.SetTarget(1);
                pane.Zoom.SetTarget(1);
                pane.Blur.SetTarget(0);
            }

            bool fading = pane.Fade.Step(dt) | pane.Zoom.Step(dt) | pane.Blur.Step(dt);
            pane.DrawTransition();
            if (!pane.On && pane.Fade.Value < 0.01)
            {
                pane.Root.Visibility = Visibility.Collapsed;
                continue;
            }

            FrameNeed own = pane.Animate(now, dt, _voice.Value);
            if (fading)
            {
                own = FrameNeed.Fast;
            }

            if (own > need)
            {
                need = own;
            }
        }

        // A pane waiting to appear needs the clock to keep running.
        if (_shown is { AppearAt: < double.PositiveInfinity })
        {
            need = FrameNeed.Fast;
        }

        return need;
    }

    private Spring NewSpring(double value, SpringToken token, double precision = 0.001)
    {
        var spring = new Spring(value, token, precision) { Instant = _reduce };
        _springs.Add(spring);
        return spring;
    }

    private void Add(CompanionState state, Pane pane)
    {
        _panes[state] = pane;
        Canvas.SetLeft(pane.Root, _center - (pane.Size.Width / 2));
        Canvas.SetTop(pane.Root, 0);
        _panesLayer.Children.Add(pane.Root);
    }

    private void Morph(CompanionState state)
    {
        Size2 size = _assets.Tokens.NotchSizes[state];
        _width.SetTarget(size.Width);
        _height.SetTarget(size.Height);
        _radius.SetTarget(Math.Min(size.Height / 2, _assets.Tokens.NotchMaxRadius));
        _rimLevel.SetTarget(state switch
        {
            CompanionState.Listening => 1,
            CompanionState.Speaking => 0.7,
            _ => 0,
        });
        Show(_panes[state]);
    }

    private void Show(Pane pane)
    {
        if (ReferenceEquals(_shown, pane))
        {
            return;
        }

        if (_shown is not null)
        {
            _shown.On = false;
            _shown.AppearAt = double.PositiveInfinity;
            _shown.Fade.SetTarget(0);
            _shown.Zoom.SetTarget(0.95);
            _shown.Blur.SetTarget(6);
        }

        _shown = pane;
        pane.On = true;
        bool fresh = pane.Root.Visibility != Visibility.Visible || pane.Fade.Value < 0.02;
        if (fresh)
        {
            pane.Fade.Jump(0);
            pane.Zoom.Jump(1.05);
            pane.Blur.Jump(_assets.Tokens.ContentBlur);
        }

        // The notch moves first and the content follows a moment later.
        pane.AppearAt = _loop.Now + (fresh && !_reduce ? _assets.Tokens.ContentDelayMs / 1000 : 0);
    }

    private void DriveVoice(double now)
    {
        if (_state is not (CompanionState.Listening or CompanionState.Speaking))
        {
            _voice.SetTarget(0.08);
            return;
        }

        if (!DemoVoice)
        {
            _voice.SetTarget(_voiceTarget);
            return;
        }

        if (_state == CompanionState.Listening)
        {
            if (now > _nextSyllable)
            {
                bool pause = _random.NextDouble() < 0.18;
                _voice.SetTarget(pause ? 0.1 : 0.3 + (_random.NextDouble() * 0.7));
                _nextSyllable = now + (pause ? 0.28 : 0.08 + (_random.NextDouble() * 0.16));
            }
        }
        else
        {
            _voice.SetTarget(0.32 + (0.5 * Math.Abs(Math.Sin(now * 7.1) * Math.Sin((now * 2.3) + 1))));
        }
    }

    private void Redraw(double now)
    {
        double w = Math.Max(0, _width.Value);
        double h = Math.Max(0, _height.Value);
        double fillet = Math.Min(_assets.Tokens.NotchFilletMax, h / 2);
        double r = Math.Clamp(_radius.Value, 0, Math.Max(0, Math.Min(h - fillet, w / 2)));

        // Hudhud keeps the clock running, but a notch at rest must not redraw its shadow every frame.
        if (w != _drawnWidth || h != _drawnHeight || r != _drawnRadius)
        {
            _drawnWidth = w;
            _drawnHeight = h;
            _drawnRadius = r;
            _shape.Data = Outline(_center, w, h, r, fillet);
            _panesLayer.Clip = Outline(_center, w, h, r, 0);
        }

        double level = Math.Clamp(_rimLevel.Value, 0, 1) * (0.75 + (0.25 * Math.Min(1, _voice.Value * 1.4)));
        if (level > 0.005)
        {
            _rim.Visibility = Visibility.Visible;
            _rim.Opacity = level;
            _rim.Data = Outline(_center, w + 12, h + 6, r + 6, 0);
            if (!_reduce)
            {
                _rimTurn.Angle = (now * 70) % 360;
            }
        }
        else
        {
            _rim.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>The notch's outline: flat along the top edge, with concave fillets into the screen edge and
    /// rounded bottom corners.</summary>
    private static StreamGeometry Outline(double center, double width, double height, double radius, double fillet)
    {
        double left = center - (width / 2);
        double right = center + (width / 2);
        var geometry = new StreamGeometry();
        using (StreamGeometryContext c = geometry.Open())
        {
            c.BeginFigure(new Point(left - fillet, 0), isFilled: true, isClosed: true);
            c.LineTo(new Point(right + fillet, 0), isStroked: false, isSmoothJoin: false);
            if (fillet > 0)
            {
                c.ArcTo(new Point(right, fillet), new Size(fillet, fillet), 0, false, SweepDirection.Counterclockwise, false, false);
            }

            c.LineTo(new Point(right, height - radius), isStroked: false, isSmoothJoin: false);
            if (radius > 0)
            {
                c.ArcTo(new Point(right - radius, height), new Size(radius, radius), 0, false, SweepDirection.Clockwise, false, false);
            }

            c.LineTo(new Point(left + radius, height), isStroked: false, isSmoothJoin: false);
            if (radius > 0)
            {
                c.ArcTo(new Point(left, height - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, false, false);
            }

            c.LineTo(new Point(left, fillet), isStroked: false, isSmoothJoin: false);
            if (fillet > 0)
            {
                c.ArcTo(new Point(left - fillet, 0), new Size(fillet, fillet), 0, false, SweepDirection.Counterclockwise, false, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private void OnHover(bool over)
    {
        _hovering = over;
        if (_state == CompanionState.Idle)
        {
            Morph(over ? CompanionState.Peek : CompanionState.Idle);
            Hovered?.Invoke(over);
            _loop.Wake();
        }
    }

    private void Place()
    {
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
    }

    private void OnSystemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.PrimaryScreenWidth) or nameof(SystemParameters.WorkArea))
        {
            Place();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemChanged;
        base.OnClosed(e);
    }
}
