using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mubsir.Overlay;

/// <summary>Small helpers for building the notch's content in code.</summary>
internal static class Ui
{
    // Segoe UI Variable on Windows 11, Segoe UI on Windows 10; both cover Arabic.
    private static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI, Tahoma");
    private static readonly Dictionary<Color, ImageSource> Marks = [];

    public static TextBlock Text(string text, double size, Brush brush, FontWeight? weight = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Font,
            FontSize = size,
            Foreground = brush,
            FontWeight = weight ?? FontWeights.Normal,
            TextTrimming = TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Center,
        };
        TextOptions.SetTextRenderingMode(block, TextRenderingMode.Grayscale);
        return block;
    }

    public static TextBlock Label(string text, Brush brush) => Text(Tracked(text), 11.5, brush, FontWeights.SemiBold);

    /// <summary>A small uppercase label with the prototype's letter spacing. WPF has no letter-spacing
    /// setting, so a hair space goes between the letters.</summary>
    public static string Tracked(string text) => string.Join('\u200A', text.ToUpperInvariant().ToCharArray());

    /// <summary>The app icon's eye. Drawn once as vectors and shared, so it stays sharp at any scale.</summary>
    public static Image Mark(Color color, double width, double height)
    {
        if (!Marks.TryGetValue(color, out ImageSource? source))
        {
            source = BuildMark(color);
            Marks[color] = source;
        }

        return new Image { Source = source, Width = width, Height = height, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
    }

    private static ImageSource BuildMark(Color color)
    {
        Brush fill = Palette.Solid(color);
        var ring = new Pen(fill, 1.9);
        ring.Freeze();
        var group = new DrawingGroup();
        // A transparent box fixes the drawing's bounds to the icon's view box (4 12 72 58).
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(4, 12, 72, 58))));
        group.Children.Add(new GeometryDrawing(fill, null, Geometry.Parse("M18.7 37.9 C19.2 30 29.5 17.2 44 16.6 C57.5 16.4 66.5 26 71.9 33.2 C64.5 30.6 55.5 23.1 44 21.6 C31.5 21.6 22.6 29.9 18.7 37.9 Z")));
        group.Children.Add(new GeometryDrawing(fill, null, Geometry.Parse("M71.9 33.3 C68 35.2 65 36.9 61.5 38.7 C58.5 40.4 55.5 42.1 52.5 43.2 C48.5 44.8 45.5 45.7 42 45.7 C37.5 45.7 33.6 43.6 30 41.7 C25.6 39.3 21.2 37.6 18.3 37.6 C14.8 40 10.4 43.4 8.4 47.4 C7 49.9 6.5 52 6.5 54 C6.5 56.5 6.6 57.8 7 59.2 C7.8 63.6 10.4 66.6 13.9 66.6 C17.5 66.6 19.9 64.2 19.9 61.4 C19.9 60 19.4 58.9 18.6 58.3 C18 59.9 16.5 60.7 14.9 60.6 C13 60.5 10.9 59.3 10.8 57.3 C10.7 55.4 10.5 53.6 10.7 52 C11.1 49.4 13.4 47.4 16.8 46.2 C18.4 45.6 19.8 44.7 21.2 44.7 C24.2 44.7 27.6 46.8 31 48.4 C34.5 50 37.5 50.5 41 50.5 C45.5 50.5 49.5 50 53 48.4 C56.2 46.8 59.2 44.7 61.6 42.7 C65.2 39.8 68.6 36.8 71.9 33.3 Z")));
        group.Children.Add(new GeometryDrawing(fill, null, new EllipseGeometry(new Point(44, 34), 3.4, 3.4)));
        group.Children.Add(new GeometryDrawing(null, ring, new EllipseGeometry(new Point(44, 34), 6.3, 6.3)));
        group.Children.Add(new GeometryDrawing(null, ring, new EllipseGeometry(new Point(44, 34), 10.05, 10.05)));
        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
