using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Mubsir.Core.Motion;

namespace Mubsir.Overlay;

/// <summary>What the user chose in the panel Hudhud opens.</summary>
public enum CompanionAction
{
    Talk,
    ReadScreen,
    Settings,
}

/// <summary>
/// The small Mubsir panel that springs out above Hudhud when it is clicked. It closes when the user
/// chooses something or clicks anywhere else.
/// </summary>
internal sealed class PanelWindow : Window, IAnimated
{
    // Room around the card for its soft shadow.
    private const double Pad = 44;
    private const double CardWidth = 252;
    private const double MaxCardHeight = 240;

    // How far above the taskbar the card's bottom edge sits: just above the bird.
    private const double AboveTaskbar = 112;
    private const double MarginRight = 22;
    private readonly Spring _scale;
    private readonly Spring _fade;
    private readonly Border _card;
    private readonly ScaleTransform _cardScale = new(0.9, 0.9);
    private readonly ClickThrough _clickThrough;
    private readonly FrameLoop _loop;
    private bool _open;

    public PanelWindow(OverlayAssets assets, FrameLoop loop, bool reduceMotion)
    {
        _loop = loop;
        Win32.Configure(this);
        Title = "Mubsir";
        Width = CardWidth + (Pad * 2);
        Height = MaxCardHeight + (Pad * 2);
        Place();
        SystemParameters.StaticPropertyChanged += OnSystemChanged;

        SpringToken token = assets.Tokens.Spring("panel");
        _scale = new Spring(0.9, token, 0.0005) { Instant = reduceMotion };
        _fade = new Spring(0, token) { Instant = reduceMotion };

        Palette p = assets.Palette;
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        Image mark = Ui.Mark(p.Amber, 30, 24);
        mark.Margin = new Thickness(0, 0, 10, 0);
        head.Children.Add(mark);
        var title = new StackPanel { Orientation = Orientation.Vertical };
        title.Children.Add(Ui.Text("Mubsir", 15, p.CreamBrush, FontWeights.SemiBold));
        title.Children.Add(Ui.Text("Ready · hold Right Ctrl to talk", 12, p.CreamDim));
        head.Children.Add(title);

        var content = new StackPanel { Orientation = Orientation.Vertical };
        content.Children.Add(head);
        content.Children.Add(Button(p, "●", "Talk to Mubsir", CompanionAction.Talk));
        content.Children.Add(Button(p, "¶", "Read this screen", CompanionAction.ReadScreen));
        Border last = Button(p, "⚙", "Settings", CompanionAction.Settings);
        last.Margin = new Thickness(0);
        content.Children.Add(last);

        _card = new Border
        {
            Width = CardWidth,
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(18),
            Background = Palette.Solid(Color.FromRgb(10, 12, 40), 0.96),
            BorderBrush = Palette.Solid(p.Cream, 0.12),
            BorderThickness = new Thickness(1),
            Child = content,
            RenderTransform = _cardScale,
            RenderTransformOrigin = new Point(0.85, 1),
            Opacity = 0,
            Visibility = Visibility.Collapsed,
            Effect = new DropShadowEffect { Color = Color.FromRgb(0, 0, 16), BlurRadius = 40, ShadowDepth = 16, Direction = 270, Opacity = 0.5, RenderingBias = RenderingBias.Performance },
        };
        var root = new Canvas { Width = Width, Height = Height };
        // Anchored at the bottom, so the card opens upward from just above the bird.
        Canvas.SetLeft(_card, Pad);
        Canvas.SetBottom(_card, Pad);
        root.Children.Add(_card);
        Content = root;

        _clickThrough = new ClickThrough(this, () => _open ? new Rect(Pad, Height - Pad - _card.ActualHeight, CardWidth, _card.ActualHeight) : Rect.Empty);
    }

    public event Action<CompanionAction>? ActionChosen;

    /// <summary>The panel opened or closed. Hudhud stays awake while it is open.</summary>
    public event Action<bool>? OpenChanged;

    /// <summary>True while a click must not count as "somewhere else": it is on the bird, which toggles the panel itself.</summary>
    public Func<bool>? CursorOnOpener { get; set; }

    public bool IsOpen => _open;

    public void Toggle() => SetOpen(!_open);

    public void SetOpen(bool open)
    {
        if (open == _open)
        {
            return;
        }

        _open = open;
        if (open)
        {
            _card.Visibility = Visibility.Visible;
        }

        _scale.SetTarget(open ? 1 : 0.92);
        _fade.SetTarget(open ? 1 : 0);
        OpenChanged?.Invoke(open);
        _loop.Wake();
    }

    public FrameNeed Tick(double now, double dt)
    {
        bool moving = _scale.Step(dt) | _fade.Step(dt);
        if (_card.Visibility == Visibility.Visible)
        {
            _cardScale.ScaleX = _cardScale.ScaleY = _scale.Value;
            _card.Opacity = Math.Clamp(_fade.Value, 0, 1);
            if (!_open && _fade.Value < 0.01)
            {
                _card.Visibility = Visibility.Collapsed;
            }
        }

        // The windows never get focus, so a click elsewhere is noticed by watching the button.
        if (_open && Win32.IsLeftButtonDown() && !_clickThrough.IsOver && !(CursorOnOpener?.Invoke() ?? false))
        {
            SetOpen(false);
            moving = true;
        }

        return moving ? FrameNeed.Fast : FrameNeed.None;
    }

    private Border Button(Palette p, string icon, string text, CompanionAction action)
    {
        Brush rest = Palette.Solid(Colors.White, 0.05);
        Brush hover = Palette.Solid(p.Amber, 0.16);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        TextBlock glyph = Ui.Text(icon, 13.5, p.AmberBrush);
        glyph.Width = 18;
        glyph.TextAlignment = TextAlignment.Center;
        glyph.Margin = new Thickness(0, 0, 10, 0);
        row.Children.Add(glyph);
        row.Children.Add(Ui.Text(text, 13.5, p.CreamBrush));
        var button = new Border
        {
            Padding = new Thickness(12, 8, 12, 9),
            Margin = new Thickness(0, 0, 0, 6),
            CornerRadius = new CornerRadius(11),
            BorderBrush = Palette.Solid(p.Cream, 0.1),
            BorderThickness = new Thickness(1),
            Background = rest,
            Child = row,
            Cursor = Cursors.Hand,
        };
        button.MouseEnter += (_, _) => button.Background = hover;
        button.MouseLeave += (_, _) => button.Background = rest;
        button.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            SetOpen(false);
            ActionChosen?.Invoke(action);
        };
        return button;
    }

    private void Place()
    {
        Rect area = SystemParameters.WorkArea;
        Left = area.Right - MarginRight - CardWidth - Pad;
        Top = area.Bottom - AboveTaskbar - Height + Pad;
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
        SystemParameters.StaticPropertyChanged -= OnSystemChanged;
        base.OnClosed(e);
    }
}
