using System.Windows.Media;
using Mubsir.Core.Motion;

namespace Mubsir.Overlay;

/// <summary>Colours from the tokens, as frozen brushes (safe and cheap to share).</summary>
internal sealed class Palette
{
    public Palette(MotionTokens tokens)
    {
        Island = Parse(tokens, "island");
        Amber = Parse(tokens, "amber");
        Cream = Parse(tokens, "cream");
        Crest = Parse(tokens, "crest");
        Ok = Parse(tokens, "ok");
        Lilac = Parse(tokens, "lilac");
        Teal = Parse(tokens, "teal");
        Sky = Parse(tokens, "sky");
        Pink = Parse(tokens, "pink");
        IslandBrush = Solid(Island);
        AmberBrush = Solid(Amber);
        CreamBrush = Solid(Cream);
        CreamDim = Solid(Cream, 0.62);
        CreamFaint = Solid(Cream, 0.38);
        CrestBrush = Solid(Crest);
        OkBrush = Solid(Ok);
    }

    public Color Island { get; }

    public Color Amber { get; }

    public Color Cream { get; }

    public Color Crest { get; }

    public Color Ok { get; }

    public Color Lilac { get; }

    public Color Teal { get; }

    public Color Sky { get; }

    public Color Pink { get; }

    public Brush IslandBrush { get; }

    public Brush AmberBrush { get; }

    public Brush CreamBrush { get; }

    public Brush CreamDim { get; }

    public Brush CreamFaint { get; }

    public Brush CrestBrush { get; }

    public Brush OkBrush { get; }

    public static SolidColorBrush Solid(Color color, double opacity = 1)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    public static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);

    private static Color Parse(MotionTokens tokens, string name) =>
        (Color)ColorConverter.ConvertFromString(tokens.Colors[name]);
}
