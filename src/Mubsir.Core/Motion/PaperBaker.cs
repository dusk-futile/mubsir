namespace Mubsir.Core.Motion;

/// <summary>A colour with an opacity, for a baked shadow or edge.</summary>
public readonly record struct BakeColor(byte R, byte G, byte B, double Opacity);

/// <summary>How to bake one part. Lengths are pixels of the bitmap being baked.</summary>
public readonly record struct PaperBakeOptions(
    double ShadowOffsetX,
    double ShadowOffsetY,
    double ShadowBlur,
    BakeColor Shadow,
    double EdgeBlur,
    BakeColor Edge,
    double HaloBlur,
    BakeColor Halo);

/// <summary>
/// A part with its effects baked in. Both bitmaps are premultiplied BGRA, <see cref="Pad"/> pixels larger than
/// the original on every side, to leave room for the shadow.
/// </summary>
public sealed record BakedPart(int Width, int Height, int Pad, byte[] Main, byte[] Backing);

/// <summary>
/// Bakes Hudhud's paper look into its pictures once, at startup, so drawing a frame only moves images. Live blur
/// effects on every part, every frame, cost most of a CPU core without a GPU.
/// <list type="bullet">
/// <item><c>Main</c> is the part over the soft shadow it casts on the parts under it.</item>
/// <item><c>Backing</c> is the light paper edge and soft dark halo, drawn under the whole bird, so dark parts
/// stay readable on dark wallpapers and the bird lifts off light ones.</item>
/// </list>
/// </summary>
public static class PaperBaker
{
    public static BakedPart Bake(byte[] bgra, int width, int height, in PaperBakeOptions o)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
        {
            throw new ArgumentException("the pixels don't match the size", nameof(bgra));
        }

        double reach = Math.Max(
            (3 * o.ShadowBlur) + Math.Max(Math.Abs(o.ShadowOffsetX), Math.Abs(o.ShadowOffsetY)),
            3 * Math.Max(o.EdgeBlur, o.HaloBlur));
        int pad = (int)Math.Ceiling(reach) + 1;
        int w = width + (2 * pad);
        int h = height + (2 * pad);

        var alpha = new float[w * h];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                alpha[((y + pad) * w) + x + pad] = bgra[(((y * width) + x) * 4) + 3] / 255f;
            }
        }

        float[] shadow = Blur(alpha, w, h, o.ShadowBlur);
        float[] edge = Blur(alpha, w, h, o.EdgeBlur);
        float[] halo = Blur(alpha, w, h, o.HaloBlur);
        var main = new byte[w * h * 4];
        var backing = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int to = ((y * w) + x) * 4;

                // The part over its shadow.
                int sx = x - pad;
                int sy = y - pad;
                bool inside = sx >= 0 && sy >= 0 && sx < width && sy < height;
                int from = inside ? ((sy * width) + sx) * 4 : -1;
                double a = inside ? bgra[from + 3] / 255.0 : 0;
                double shadowAlpha = Sample(shadow, w, h, x - o.ShadowOffsetX, y - o.ShadowOffsetY) * o.Shadow.Opacity * (1 - a);
                main[to] = ToByte((inside ? bgra[from] : 0) + (o.Shadow.B * shadowAlpha));
                main[to + 1] = ToByte((inside ? bgra[from + 1] : 0) + (o.Shadow.G * shadowAlpha));
                main[to + 2] = ToByte((inside ? bgra[from + 2] : 0) + (o.Shadow.R * shadowAlpha));
                main[to + 3] = ToByte((a + shadowAlpha) * 255);

                // The light edge over the dark halo.
                int i = (y * w) + x;
                double edgeAlpha = edge[i] * o.Edge.Opacity;
                double haloAlpha = halo[i] * o.Halo.Opacity * (1 - edgeAlpha);
                backing[to] = ToByte((o.Edge.B * edgeAlpha) + (o.Halo.B * haloAlpha));
                backing[to + 1] = ToByte((o.Edge.G * edgeAlpha) + (o.Halo.G * haloAlpha));
                backing[to + 2] = ToByte((o.Edge.R * edgeAlpha) + (o.Halo.R * haloAlpha));
                backing[to + 3] = ToByte((edgeAlpha + haloAlpha) * 255);
            }
        }

        return new BakedPart(w, h, pad, main, backing);
    }

    /// <summary>A Gaussian blur, close enough for shadows: three box blurs in each direction.</summary>
    public static float[] Blur(float[] plane, int width, int height, double sigma)
    {
        ArgumentNullException.ThrowIfNull(plane);
        var result = (float[])plane.Clone();
        if (sigma < 0.3)
        {
            return result;
        }

        var scratch = new float[plane.Length];
        int radius = Math.Max(1, (int)Math.Round((Math.Sqrt((12 * sigma * sigma / 3) + 1) - 1) / 2));
        for (int pass = 0; pass < 3; pass++)
        {
            BoxRows(result, scratch, width, height, radius);
            BoxColumns(scratch, result, width, height, radius);
        }

        return result;
    }

    private static void BoxRows(float[] source, float[] target, int width, int height, int radius)
    {
        float scale = 1f / ((2 * radius) + 1);
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            float sum = 0;
            for (int x = -radius; x <= radius; x++)
            {
                sum += x >= 0 && x < width ? source[row + x] : 0;
            }

            for (int x = 0; x < width; x++)
            {
                target[row + x] = sum * scale;
                int leaving = x - radius;
                int entering = x + radius + 1;
                sum += (entering < width ? source[row + entering] : 0) - (leaving >= 0 ? source[row + leaving] : 0);
            }
        }
    }

    private static void BoxColumns(float[] source, float[] target, int width, int height, int radius)
    {
        float scale = 1f / ((2 * radius) + 1);
        for (int x = 0; x < width; x++)
        {
            float sum = 0;
            for (int y = -radius; y <= radius; y++)
            {
                sum += y >= 0 && y < height ? source[(y * width) + x] : 0;
            }

            for (int y = 0; y < height; y++)
            {
                target[(y * width) + x] = sum * scale;
                int leaving = y - radius;
                int entering = y + radius + 1;
                sum += (entering < height ? source[(entering * width) + x] : 0) - (leaving >= 0 ? source[(leaving * width) + x] : 0);
            }
        }
    }

    /// <summary>Reads a plane between pixels; outside it is empty.</summary>
    private static double Sample(float[] plane, int width, int height, double x, double y)
    {
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        double fx = x - x0;
        double fy = y - y0;
        double top = (At(plane, width, height, x0, y0) * (1 - fx)) + (At(plane, width, height, x0 + 1, y0) * fx);
        double bottom = (At(plane, width, height, x0, y0 + 1) * (1 - fx)) + (At(plane, width, height, x0 + 1, y0 + 1) * fx);
        return (top * (1 - fy)) + (bottom * fy);
    }

    private static float At(float[] plane, int width, int height, int x, int y) =>
        x >= 0 && y >= 0 && x < width && y < height ? plane[(y * width) + x] : 0;

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
