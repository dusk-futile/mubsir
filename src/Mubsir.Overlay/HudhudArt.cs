using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mubsir.Core.Motion;

namespace Mubsir.Overlay;

/// <summary>
/// Prepares Hudhud's pictures once, at startup: sizes them for this screen and bakes in the paper look
/// (<see cref="PaperBaker"/>), so drawing a frame only moves images around.
/// </summary>
internal sealed class HudhudArt
{
    // Kept at twice their on-screen size, so edges stay smooth as parts turn.
    private const double Supersample = 2;

    // In the rig's units (about ten to a screen pixel at the bird's size). Same look as the prototype's
    // light paper edge and soft shadow.
    private const double EdgeBlurUnits = 9;
    private const double HaloBlurUnits = 25;
    private static readonly BakeColor HaloColor = new(0, 0, 18, 0.28);
    private readonly OverlayAssets _assets;
    private readonly HudhudRig _rig;
    private readonly double _pixelsPerUnit;
    private readonly double _shrink;
    private readonly BakeColor _edge;

    public HudhudArt(OverlayAssets assets, double screenPixelsPerUnit)
    {
        _assets = assets;
        _rig = assets.Rig;
        _shrink = Math.Min(1, screenPixelsPerUnit * Supersample / _rig.ExportScale);
        _pixelsPerUnit = _rig.ExportScale * _shrink;
        Color cream = assets.Palette.Cream;
        _edge = new BakeColor(cream.R, cream.G, cream.B, 0.8);
    }

    /// <summary>One part, ready to place: its pictures, and where they go relative to its joint.</summary>
    public sealed record Piece(BitmapSource Main, BitmapSource Backing, double Left, double Top, double Width, double Height);

    /// <param name="image">The part's picture.</param>
    /// <param name="pivotX">The joint inside the picture, in rig units.</param>
    /// <param name="pivotY">The joint inside the picture, in rig units.</param>
    /// <param name="scaleX">The part's horizontal scale.</param>
    /// <param name="scaleY">The part's vertical scale.</param>
    /// <param name="restDegrees">Its resting angle, so its shadow falls straight down when it rests.</param>
    public Piece Bake(string image, double pivotX, double pivotY, double scaleX, double scaleY, double restDegrees)
    {
        BitmapSource source = _assets.Images[image];
        var scaled = new FormatConvertedBitmap(
            new TransformedBitmap(source, new ScaleTransform(_shrink, _shrink)),
            PixelFormats.Pbgra32,
            null,
            0);
        int width = scaled.PixelWidth;
        int height = scaled.PixelHeight;
        var pixels = new byte[width * height * 4];
        scaled.CopyPixels(pixels, width * 4, 0);

        // The picture turns with the part, so "down" in the picture depends on how the part rests.
        double rest = restDegrees * Math.PI / 180;
        double offsetY = 0;
        double blur = 0;
        var shadowColor = new BakeColor(0, 0, 0, 0);
        if (_rig.Shadow is { } shadow)
        {
            offsetY = shadow.OffsetY;
            blur = shadow.Blur;
            Color c = (Color)ColorConverter.ConvertFromString(shadow.Color);
            shadowColor = new BakeColor(c.R, c.G, c.B, shadow.Opacity);
        }

        double average = (scaleX + scaleY) / 2;
        var options = new PaperBakeOptions(
            ShadowOffsetX: offsetY * Math.Sin(rest) / scaleX * _pixelsPerUnit,
            ShadowOffsetY: offsetY * Math.Cos(rest) / scaleY * _pixelsPerUnit,
            ShadowBlur: blur / average * _pixelsPerUnit,
            Shadow: shadowColor,
            EdgeBlur: EdgeBlurUnits / average * _pixelsPerUnit,
            Edge: _edge,
            HaloBlur: HaloBlurUnits / average * _pixelsPerUnit,
            Halo: HaloColor);
        BakedPart baked = PaperBaker.Bake(pixels, width, height, options);

        // Rounding the size may change the scale a hair; these keep the picture exactly in place.
        double unitsX = source.PixelWidth / _rig.ExportScale / width;
        double unitsY = source.PixelHeight / _rig.ExportScale / height;
        return new Piece(
            Create(baked.Main, baked.Width, baked.Height),
            Create(baked.Backing, baked.Width, baked.Height),
            -(pivotX + (baked.Pad * unitsX)) * scaleX,
            -(pivotY + (baked.Pad * unitsY)) * scaleY,
            baked.Width * unitsX * scaleX,
            baked.Height * unitsY * scaleY);
    }

    private static BitmapSource Create(byte[] pixels, int width, int height)
    {
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
