using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mubsir.Core.Motion;
using WpfEllipse = System.Windows.Shapes.Ellipse;

namespace Mubsir.Overlay;

/// <summary>
/// Hudhud, the paper-cut hoopoe, standing bottom-right above the taskbar. A cut-out puppet: every part
/// is a picture that turns around its own joint (assets/hudhud/rig.json), and each casts a soft shadow
/// on the parts under it. It breathes, blinks, shakes dust off, preens, stretches, hops, and falls
/// asleep when there is nothing to do.
/// </summary>
internal sealed class HudhudWindow : Window, IAnimated
{
    // Room around the bird's box for the raised crest, the dust and the z's.
    private const double Pad = 44;
    private const int DustCount = 14;
    private readonly OverlayAssets _assets;
    private readonly HudhudRig _rig;
    private readonly FrameLoop _loop;
    private readonly bool _reduce;
    private readonly HudhudPoser _poser;
    private readonly HudhudFrame _frame;
    private readonly Random _random = new();
    private readonly List<Spring> _springs = [];

    private readonly Spring _crest;
    private readonly Spring _raise;
    private readonly Spring _tilt;
    private readonly Spring _bodyY;
    private readonly Spring _glow;
    private readonly Spring _enter;
    private readonly Spring _wing;
    private readonly Spring _spread;
    private readonly Spring _flip;
    private readonly Spring _sleepy;
    private readonly Spring _zzz;

    private readonly Canvas _root = new();
    private readonly MatrixTransform _body = new();
    private readonly MatrixTransform _head = new();
    private readonly MatrixTransform _faceFlip = new();
    private readonly MatrixTransform _crestGroup = new();
    private readonly MatrixTransform _wingPart = new();
    private readonly MatrixTransform _tailPart = new();
    private readonly List<MatrixTransform> _primaries = [];
    private readonly List<MatrixTransform> _feathers = [];
    private readonly Image _faceOpen;
    private readonly Image _faceClosed;
    private readonly WpfEllipse _glowDisc;
    private readonly ScaleTransform _glowScale = new(1, 1);
    private readonly WpfEllipse[] _dustDots = new WpfEllipse[DustCount];
    private readonly List<Dust> _dust = [];
    private readonly TextBlock[] _zs = new TextBlock[3];

    private readonly DispatcherTimer _lifeTimer = new();
    private readonly ClickThrough _clickThrough;
    private CompanionState _state = CompanionState.Idle;
    private bool _asleep;
    private bool _peeking;
    private bool _held;
    private double _sleepStart;
    private double _lastActivity;
    private double _shakeStart = -1;
    private double _preenStart = -1;
    private double _blinkAt = 1.4;
    private bool _blinkTwice;
    private double _puffAt = double.PositiveInfinity;
    private double _wakeShakeAt = double.PositiveInfinity;
    private double _backToPoseAt = double.PositiveInfinity;
    private double _flapAt = double.PositiveInfinity;

    public HudhudWindow(OverlayAssets assets, FrameLoop loop, bool reduceMotion)
    {
        _assets = assets;
        _rig = assets.Rig;
        _loop = loop;
        _reduce = reduceMotion;
        _poser = new HudhudPoser(_rig);
        _frame = _poser.NewFrame();
        Win32.Configure(this);
        Title = "Hudhud";

        MotionTokens tokens = assets.Tokens;
        HudhudPose idle = tokens.Hudhud.Pose("idle");
        _crest = NewSpring(idle.Crest, tokens.Spring("crest"));
        _raise = NewSpring(0, tokens.Spring("crest"));
        _tilt = NewSpring(0, tokens.Spring("head"), 0.01);
        _bodyY = NewSpring(reduceMotion ? 0 : 150, tokens.Spring("body"), 0.05);
        _glow = NewSpring(0, tokens.Spring("glow"));
        _enter = NewSpring(reduceMotion ? 1 : 0, tokens.Spring("content"));
        _wing = NewSpring(0, tokens.Spring("wing"), 0.01);
        _spread = NewSpring(0, tokens.Spring("wing"));
        _flip = NewSpring(1, tokens.Spring("turn"));
        _sleepy = NewSpring(0, tokens.Spring("sleep"));
        _zzz = NewSpring(0, tokens.Spring("content"));

        Size2 box = tokens.Hudhud.Size;
        Width = box.Width + (Pad * 2);
        Height = box.Height + (Pad * 2);
        Place();
        SystemParameters.StaticPropertyChanged += OnSystemChanged;

        // The glow behind the bird, for the moments that need attention.
        Palette p = assets.Palette;
        var glowFill = new RadialGradientBrush();
        glowFill.GradientStops.Add(new GradientStop(Palette.WithAlpha(p.Amber, 0.42), 0));
        glowFill.GradientStops.Add(new GradientStop(Palette.WithAlpha(p.Amber, 0), 1));
        glowFill.Freeze();
        _glowDisc = new WpfEllipse
        {
            Width = box.Width + 40,
            Height = box.Height + 40,
            Fill = glowFill,
            Opacity = 0,
            RenderTransform = _glowScale,
            RenderTransformOrigin = new Point(0.5, 0.5),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(_glowDisc, Pad - 20);
        Canvas.SetTop(_glowDisc, Pad - 20);
        _root.Children.Add(_glowDisc);

        // Rig units to screen, then the mirror that makes the right-facing rig look left.
        double scale = box.Width / _rig.ViewWidth;
        var stage = new Canvas();
        var toScreen = Matrix.Identity;
        toScreen.Translate(-_rig.ViewX, -_rig.ViewY);
        toScreen.Scale(scale, scale);
        toScreen.Translate(Pad, Pad);
        stage.RenderTransform = new MatrixTransform(toScreen);

        var mirror = new Canvas();
        var flip = Matrix.Identity;
        flip.Scale(-1, 1);
        flip.Translate(_rig.MirrorX, 0);
        mirror.RenderTransform = new MatrixTransform(flip);

        // Prepared once for this screen; a frame only moves these pictures. The backing layer holds the light
        // paper edge and soft halo and goes under the whole bird; both layers share one set of joints.
        var art = new HudhudArt(assets, scale * VisualTreeHelper.GetDpi(_root).DpiScaleX);
        var backing = new Canvas { RenderTransform = _body, IsHitTestVisible = false };
        var body = new Canvas { RenderTransform = _body };
        foreach (RigPart part in _rig.BackToFront)
        {
            MatrixTransform turn = part.Id switch
            {
                "hWing" => _wingPart,
                "hTail" => _tailPart,
                _ when part.Id.StartsWith("hP", StringComparison.Ordinal) => AddTo(_primaries),
                _ => new MatrixTransform(),
            };
            turn.Matrix = Rotation(part.Rest);
            HudhudArt.Piece piece = art.Bake(part.Image, part.PivotX, part.PivotY, part.ScaleX, part.ScaleY, part.Rest);
            var at = new TranslateTransform(part.AtX, part.AtY);
            backing.Children.Add(Placed(Picture(piece, piece.Backing), turn, at));
            body.Children.Add(Placed(Picture(piece, piece.Main), turn, at));
        }

        var feathers = new List<HudhudArt.Piece>();
        foreach (RigFeather f in _rig.Crest)
        {
            _feathers.Add(new MatrixTransform());
            double rest = f.ClosedAngle + ((f.OpenAngle - f.ClosedAngle) * idle.Crest);
            feathers.Add(art.Bake(f.Image, f.PivotX, f.PivotY, f.ScaleX, f.ScaleY, rest));
        }

        HudhudArt.Piece open = art.Bake(_rig.OpenFace.Image, _rig.OpenFace.PivotX, _rig.OpenFace.PivotY, 1, 1, 0);
        HudhudArt.Piece shut = art.Bake(_rig.ClosedFace.Image, _rig.ClosedFace.PivotX, _rig.ClosedFace.PivotY, 1, 1, 0);
        _faceOpen = Picture(open, open.Main);
        _faceClosed = Picture(shut, shut.Main);
        _faceClosed.Visibility = Visibility.Hidden;
        backing.Children.Add(BuildHead(feathers.ConvertAll(f => Picture(f, f.Backing)), Picture(open, open.Backing)));
        body.Children.Add(BuildHead(feathers.ConvertAll(f => Picture(f, f.Main)), _faceOpen, _faceClosed));
        var dustLayer = new Canvas { IsHitTestVisible = false };
        for (int i = 0; i < DustCount; i++)
        {
            _dustDots[i] = new WpfEllipse { Visibility = Visibility.Collapsed };
            dustLayer.Children.Add(_dustDots[i]);
        }

        body.Children.Add(dustLayer);
        mirror.Children.Add(backing);
        mirror.Children.Add(body);
        stage.Children.Add(mirror);

        // The z's float outside the mirror, so the letters read the right way round.
        for (int i = 0; i < _zs.Length; i++)
        {
            _zs[i] = Ui.Text("z", 60, p.CreamBrush, FontWeights.SemiBold);
            _zs[i].Opacity = 0;
            _zs[i].IsHitTestVisible = false;
            stage.Children.Add(_zs[i]);
        }

        _root.Children.Add(stage);
        _root.Opacity = _enter.Value;
        Content = _root;

        MouseLeftButtonUp += (_, _) =>
        {
            if (!Wake())
            {
                Clicked?.Invoke();
            }
        };
        Cursor = Cursors.Hand;
        _clickThrough = new ClickThrough(this, () => new Rect(Pad, Pad, box.Width, box.Height));
        _clickThrough.HoverChanged += over =>
        {
            if (over && !_asleep)
            {
                _crest.Kick(4);
                _loop.Wake();
            }
        };

        _lastActivity = _loop.Now;
        _lifeTimer.Tick += (_, _) => OnLifeTimer();
        ScheduleLife();
    }

    /// <summary>Clicked while awake (a click while asleep only wakes it).</summary>
    public event Action? Clicked;

    /// <summary>Hudhud fell asleep or woke up.</summary>
    public event Action<bool>? AsleepChanged;

    public bool IsAsleep => _asleep;

    /// <summary>Where the voice level comes from while speaking.</summary>
    public Func<double>? VoiceSource { get; set; }

    /// <summary>The cursor is on the bird right now.</summary>
    public bool CursorOver => _clickThrough.CheckNow();

    /// <summary>While the panel is open Hudhud stays awake and keeps still.</summary>
    public void Hold(bool held)
    {
        _held = held;
        _lastActivity = _loop.Now;
        _loop.Wake();
    }

    /// <summary>Brings the bird in. Call once, after the windows are showing.</summary>
    public void Enter()
    {
        _enter.SetTarget(1);
        _bodyY.SetTarget(0);
        _loop.Wake();
    }

    public void Go(CompanionState state)
    {
        CompanionState previous = _state;
        _state = state;
        _lastActivity = _loop.Now;
        _backToPoseAt = double.PositiveInfinity;
        if (_asleep && state != CompanionState.Idle)
        {
            Wake();
        }

        if (!_asleep)
        {
            SetPose(state.Key());
        }

        _bodyY.SetTarget(0);
        if (state == CompanionState.Paid && previous != CompanionState.Paid)
        {
            // A little hop and a flap when it is done.
            _bodyY.Kick(-900);
            _crest.Kick(6);
            _wing.Kick(260);
            _flapAt = _loop.Now + 0.28;
        }

        ScheduleLife();
        _loop.Wake();
    }

    /// <summary>The notch is being looked at while resting; Hudhud lifts its head a little.</summary>
    public void Peek(bool peeking)
    {
        _peeking = peeking;
        _lastActivity = _loop.Now;
        if (!_asleep && _state == CompanionState.Idle)
        {
            SetPose(peeking ? "peek" : "idle");
            _loop.Wake();
        }
    }

    /// <summary>Wakes Hudhud if it sleeps. Returns true if it was asleep.</summary>
    public bool Wake()
    {
        _lastActivity = _loop.Now;
        if (!_asleep)
        {
            return false;
        }

        _asleep = false;
        _sleepy.SetTarget(0);
        _zzz.SetTarget(0);
        SetPose(_state.Key());
        _blinkAt = _loop.Now + 0.15;
        _blinkTwice = true;
        _wakeShakeAt = _loop.Now + 0.38;
        AsleepChanged?.Invoke(false);
        ScheduleLife();
        _loop.Wake();
        return true;
    }

    public void Sleep()
    {
        if (_asleep)
        {
            return;
        }

        _asleep = true;
        _sleepStart = _loop.Now;
        _shakeStart = -1;
        _preenStart = -1;
        _backToPoseAt = double.PositiveInfinity;
        SetPose("sleep");
        _sleepy.SetTarget(1);
        _zzz.SetTarget(1);
        _glow.SetTarget(0);
        _lifeTimer.Stop();
        AsleepChanged?.Invoke(true);
        _loop.Wake();
    }

    public FrameNeed Tick(double now, double dt)
    {
        bool moving = false;
        foreach (Spring spring in _springs)
        {
            moving |= spring.Step(dt);
        }

        RunTimers(now);
        moving |= StepDust(dt);

        // Sleep after a while with nothing to do.
        if (_state == CompanionState.Idle && !_asleep && !_peeking && !_held && now - _lastActivity > _assets.Tokens.Hudhud.SleepAfterSeconds)
        {
            Sleep();
            moving = true;
        }

        double shakeAge = _shakeStart >= 0 ? now - _shakeStart : -1;
        if (shakeAge >= HudhudPoser.ShakeSeconds)
        {
            _shakeStart = -1;
            shakeAge = -1;
        }

        double preenAge = _preenStart >= 0 ? now - _preenStart : -1;
        if (preenAge >= 1.6)
        {
            _preenStart = -1;
            preenAge = -1;
        }

        var input = new HudhudInput
        {
            Time = now,
            Crest = _crest.Value,
            Raise = _raise.Value,
            Tilt = _tilt.Value,
            Wing = _wing.Value,
            Spread = _spread.Value,
            Flip = _flip.Value,
            Sleepy = _sleepy.Value,
            BodyY = _bodyY.Value,
            BodyVelocity = _bodyY.Velocity,
            Voice = VoiceSource?.Invoke() ?? 0.4,
            Activity = _state switch
            {
                CompanionState.Thinking => HudhudActivity.Thinking,
                CompanionState.Doing => HudhudActivity.Doing,
                CompanionState.Speaking => HudhudActivity.Speaking,
                _ => HudhudActivity.Resting,
            },
            ShakeAge = shakeAge,
            PreenAge = preenAge,
            ReduceMotion = _reduce,
        };
        _poser.Pose(input, _frame);
        Apply(now);

        if (_reduce)
        {
            // Motion is off, but it still has to notice when it is time to sleep.
            return moving ? FrameNeed.Fast : _asleep ? FrameNeed.None : FrameNeed.Slow;
        }

        // Real movement gets every screen refresh. A resting breath moves a small fraction of a pixel per
        // frame, so twenty frames a second look the same at a third of the cost, and a sleeping bird's slow
        // breath needs only ten.
        if (moving || shakeAge >= 0 || preenAge >= 0 || _state != CompanionState.Idle)
        {
            return FrameNeed.Fast;
        }

        return _asleep ? FrameNeed.Slow : FrameNeed.Medium;
    }

    private Spring NewSpring(double value, SpringToken token, double precision = 0.001)
    {
        var spring = new Spring(value, token, precision) { Instant = _reduce };
        _springs.Add(spring);
        return spring;
    }

    private void SetPose(string name)
    {
        HudhudPose pose = _assets.Tokens.Hudhud.Pose(name);
        _crest.SetTarget(pose.Crest);
        _tilt.SetTarget(pose.Tilt);
        _raise.SetTarget(pose.Raise);
        _wing.SetTarget(pose.Wing);
        _spread.SetTarget(pose.Spread);
        _flip.SetTarget(1);
        _preenStart = -1;
        _glow.SetTarget(name switch
        {
            "listening" => 1,
            "confirm" => 0.65,
            _ => 0,
        });
    }

    private static MatrixTransform AddTo(List<MatrixTransform> list)
    {
        var transform = new MatrixTransform();
        list.Add(transform);
        return transform;
    }

    private static Matrix Rotation(double degrees)
    {
        var m = Matrix.Identity;
        m.Rotate(degrees);
        return m;
    }

    private static Image Picture(HudhudArt.Piece piece, BitmapSource source)
    {
        var image = new Image { Source = source, Stretch = Stretch.Fill, Width = piece.Width, Height = piece.Height };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Linear);
        Canvas.SetLeft(image, piece.Left);
        Canvas.SetTop(image, piece.Top);
        return image;
    }

    private static Canvas Placed(Image image, Transform turn, Transform at)
    {
        var inner = new Canvas { RenderTransform = turn };
        inner.Children.Add(image);
        var outer = new Canvas { RenderTransform = at };
        outer.Children.Add(inner);
        return outer;
    }

    /// <summary>The crest behind the face, turning with the head. Both layers share the same joints.</summary>
    private Canvas BuildHead(List<Image> feathers, params Image[] faces)
    {
        var crest = new Canvas { RenderTransform = _crestGroup };
        for (int i = 0; i < feathers.Count; i++)
        {
            var holder = new Canvas { RenderTransform = _feathers[i] };
            holder.Children.Add(feathers[i]);
            crest.Children.Add(holder);
        }

        var faceLayer = new Canvas { RenderTransform = new TranslateTransform(_rig.NeckX, _rig.NeckY) };
        foreach (Image face in faces)
        {
            faceLayer.Children.Add(face);
        }

        var flip = new Canvas { RenderTransform = _faceFlip };
        flip.Children.Add(crest);
        flip.Children.Add(faceLayer);
        var head = new Canvas { RenderTransform = _head };
        head.Children.Add(flip);
        return head;
    }

    private void Apply(double now)
    {
        HudhudFrame f = _frame;
        _root.Opacity = Math.Clamp(_enter.Value, 0, 1);

        var m = Matrix.Identity;
        m.Translate(-_rig.FeetX, -_rig.FeetY);
        m.Scale(f.BodyScaleX, f.BodyScaleY);
        m.Rotate(f.BodySway);
        m.Translate(_rig.FeetX, _rig.FeetY + f.BodyY);
        _body.Matrix = m;

        m = Matrix.Identity;
        m.Translate(-_rig.NeckX, -_rig.NeckY);
        m.Rotate(f.HeadAngle);
        m.Translate(_rig.NeckX, _rig.NeckY + f.HeadY);
        _head.Matrix = m;

        m = Matrix.Identity;
        m.Translate(-_rig.FaceCenterX, 0);
        m.Scale(f.Flip, 1);
        m.Translate(_rig.FaceCenterX, 0);
        _faceFlip.Matrix = m;

        m = Matrix.Identity;
        m.Translate(-_rig.CrestPivotX, -_rig.CrestPivotY);
        m.Scale(f.CrestScale, f.CrestScale);
        m.Translate(_rig.CrestPivotX, _rig.CrestPivotY);
        _crestGroup.Matrix = m;

        for (int i = 0; i < _feathers.Count; i++)
        {
            m = Matrix.Identity;
            m.Scale(1, f.FeatherLength[i]);
            m.Rotate(f.FeatherAngle[i]);
            m.Translate(f.FeatherX[i], f.FeatherY[i]);
            _feathers[i].Matrix = m;
        }

        _wingPart.Matrix = Rotation(f.WingAngle);
        _tailPart.Matrix = Rotation(f.TailAngle);
        for (int i = 0; i < _primaries.Count; i++)
        {
            _primaries[i].Matrix = Rotation(f.PrimaryAngle[i]);
        }

        bool closed = _asleep || Blink(now);
        _faceClosed.Visibility = closed ? Visibility.Visible : Visibility.Hidden;
        _faceOpen.Visibility = closed ? Visibility.Hidden : Visibility.Visible;

        double glow = Math.Max(0, _glow.Value);
        _glowDisc.Opacity = glow;
        _glowScale.ScaleX = _glowScale.ScaleY = 0.88 + (glow * 0.12) + (f.Breath * 0.04 * glow);

        DrawDust();
        DrawZs(now);
    }

    private bool Blink(double now)
    {
        if (_reduce)
        {
            return false;
        }

        double d = now - _blinkAt;
        if (d < 0)
        {
            return false;
        }

        if (d > 0.12)
        {
            if (_blinkTwice)
            {
                _blinkTwice = false;
                _blinkAt = now + 0.13;
            }
            else
            {
                _blinkAt = now + 2.4 + (_random.NextDouble() * 3.8);
                _blinkTwice = _random.NextDouble() < 0.25;
            }

            return false;
        }

        return true;
    }

    private void Shake()
    {
        if (_reduce)
        {
            return;
        }

        _shakeStart = _loop.Now;
        _puffAt = _loop.Now + 0.06;
        _crest.Kick(3);
        _loop.Wake();
    }

    private void RunTimers(double now)
    {
        if (now >= _puffAt)
        {
            _puffAt = double.PositiveInfinity;
            Puff();
        }

        if (now >= _wakeShakeAt)
        {
            _wakeShakeAt = double.PositiveInfinity;
            if (!_asleep)
            {
                _bodyY.Kick(-500);
                Shake();
            }
        }

        if (now >= _flapAt)
        {
            _flapAt = double.PositiveInfinity;
            _wing.Kick(220);
        }

        if (now >= _backToPoseAt)
        {
            _backToPoseAt = double.PositiveInfinity;
            if (_state == CompanionState.Idle && !_asleep)
            {
                SetPose(_peeking ? "peek" : "idle");
                _bodyY.SetTarget(0);
            }
        }
    }

    private void Puff()
    {
        Color[] colors =
        [
            _assets.Palette.Cream,
            _assets.Palette.Amber,
            Color.FromRgb(0xE9, 0xB0, 0x4A),
            Color.FromRgb(0xFC, 0xEB, 0xD0),
        ];
        for (int i = 0; i < 12; i++)
        {
            int side = _random.NextDouble() < 0.5 ? -1 : 1;
            _dust.Add(new Dust
            {
                X = 470 + (_random.NextDouble() * 140),
                Y = 470 + (_random.NextDouble() * 220),
                VelocityX = side * (180 + (_random.NextDouble() * 360)),
                VelocityY = -(220 + (_random.NextDouble() * 420)),
                MaxLife = 0.7 + (_random.NextDouble() * 0.45),
                Radius = 13 + (_random.NextDouble() * 12),
                Fill = Palette.Solid(colors[i % colors.Length]),
            });
        }

        while (_dust.Count > DustCount)
        {
            _dust.RemoveAt(0);
        }
    }

    private bool StepDust(double dt)
    {
        for (int i = _dust.Count - 1; i >= 0; i--)
        {
            Dust d = _dust[i];
            d.Life += dt;
            d.VelocityY += 1400 * dt;
            d.X += d.VelocityX * dt;
            d.Y += d.VelocityY * dt;
            if (d.Life > d.MaxLife)
            {
                _dust.RemoveAt(i);
            }
        }

        return _dust.Count > 0;
    }

    private void DrawDust()
    {
        for (int i = 0; i < DustCount; i++)
        {
            WpfEllipse dot = _dustDots[i];
            if (i >= _dust.Count)
            {
                dot.Visibility = Visibility.Collapsed;
                continue;
            }

            Dust d = _dust[i];
            double r = d.Radius * (1 - (d.Life / d.MaxLife * 0.5));
            dot.Visibility = Visibility.Visible;
            dot.Fill = d.Fill;
            dot.Width = dot.Height = r * 2;
            dot.Opacity = Math.Clamp(1 - (d.Life / d.MaxLife), 0, 1);
            Canvas.SetLeft(dot, d.X - r);
            Canvas.SetTop(dot, d.Y - r);
        }
    }

    private void DrawZs(double now)
    {
        double strength = Math.Clamp(_zzz.Value, 0, 1);
        for (int i = 0; i < _zs.Length; i++)
        {
            TextBlock z = _zs[i];
            if (strength < 0.01)
            {
                z.Opacity = 0;
                continue;
            }

            double p = (((now - _sleepStart) / 2.6) + (i / 3.0)) % 1;
            z.FontSize = 52 + (p * 50);
            Canvas.SetLeft(z, 470 - (p * 70) + (Math.Sin((p * 6) + i) * 10));
            Canvas.SetTop(z, 250 - (p * 200) - z.FontSize);
            z.Opacity = Math.Sin(p * Math.PI) * 0.9 * strength;
        }
    }

    // Little things Hudhud does on its own while idle.
    private void ScheduleLife()
    {
        _lifeTimer.Stop();
        if (_reduce || _asleep)
        {
            return;
        }

        _lifeTimer.Interval = TimeSpan.FromSeconds(2.8 + (_random.NextDouble() * 3.6));
        _lifeTimer.Start();
    }

    private void OnLifeTimer()
    {
        if (_state == CompanionState.Idle && !_peeking && !_asleep && !_held)
        {
            PlayMove(_random.NextDouble());
            _loop.Wake();
        }

        ScheduleLife();
    }

    private void PlayMove(double r)
    {
        double now = _loop.Now;
        if ((r -= 0.22) < 0)
        {
            // A glance up at the notch.
            _tilt.SetTarget(7);
            _backToPoseAt = now + 1.4;
        }
        else if ((r -= 0.24) < 0)
        {
            // Shakes the dust off, wings and all.
            Shake();
        }
        else if ((r -= 0.16) < 0)
        {
            // Turns round and scratches its back, tail wiggling.
            _preenStart = now;
            _flip.SetTarget(-1);
            _tilt.SetTarget(34);
            _wing.SetTarget(16);
            _spread.SetTarget(0.7);
            _crest.SetTarget(0.35);
            _backToPoseAt = now + 1.5;
        }
        else if ((r -= 0.16) < 0)
        {
            // Stretches a wing.
            _wing.SetTarget(28);
            _spread.SetTarget(1.7);
            _crest.SetTarget(0.75);
            _bodyY.SetTarget(-12);
            _backToPoseAt = now + 0.95;
        }
        else if ((r -= 0.12) < 0)
        {
            _crest.Kick(5);
        }
        else
        {
            // A hop, with squash and stretch.
            _bodyY.Kick(-750);
            _crest.Kick(3);
            _wing.Kick(120);
        }
    }

    private void Place()
    {
        Rect area = SystemParameters.WorkArea;
        HudhudTokens tokens = _assets.Tokens.Hudhud;
        Left = area.Right - tokens.MarginRight - tokens.Size.Width - Pad;
        Top = area.Bottom - tokens.MarginBottom - tokens.Size.Height - Pad;
    }

    private void OnSystemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.WorkArea))
        {
            Place();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifeTimer.Stop();
        SystemParameters.StaticPropertyChanged -= OnSystemChanged;
        base.OnClosed(e);
    }

    private sealed class Dust
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double VelocityX { get; set; }

        public double VelocityY { get; set; }

        public double Life { get; set; }

        public double MaxLife { get; init; }

        public double Radius { get; init; }

        public Brush Fill { get; init; } = Brushes.White;
    }
}
