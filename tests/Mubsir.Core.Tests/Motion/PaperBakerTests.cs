using Mubsir.Core.Motion;

namespace Mubsir.Core.Tests.Motion;

public class PaperBakerTests
{
    private static readonly PaperBakeOptions Options = new(
        ShadowOffsetX: 0,
        ShadowOffsetY: 2,
        ShadowBlur: 1.2,
        Shadow: new BakeColor(5, 8, 38, 0.55),
        EdgeBlur: 1.8,
        Edge: new BakeColor(255, 244, 222, 0.8),
        HaloBlur: 5,
        Halo: new BakeColor(0, 0, 18, 0.28));

    /// <summary>An opaque amber square in the middle of a transparent bitmap.</summary>
    private static byte[] Square(int size, int from, int to)
    {
        var pixels = new byte[size * size * 4];
        for (int y = from; y < to; y++)
        {
            for (int x = from; x < to; x++)
            {
                int i = ((y * size) + x) * 4;
                pixels[i] = 0x43;
                pixels[i + 1] = 0xC3;
                pixels[i + 2] = 0xF3;
                pixels[i + 3] = 255;
            }
        }

        return pixels;
    }

    private static byte AlphaAt(byte[] pixels, int width, int x, int y) => pixels[(((y * width) + x) * 4) + 3];

    [Fact]
    public void The_part_itself_is_unchanged()
    {
        BakedPart baked = PaperBaker.Bake(Square(20, 5, 15), 20, 20, Options);
        int center = baked.Pad + 10;
        int i = ((center * baked.Width) + center) * 4;

        Assert.Equal(0x43, baked.Main[i]);
        Assert.Equal(0xC3, baked.Main[i + 1]);
        Assert.Equal(0xF3, baked.Main[i + 2]);
        Assert.Equal(255, baked.Main[i + 3]);
    }

    [Fact]
    public void The_shadow_falls_below_the_part_and_not_above_it()
    {
        BakedPart baked = PaperBaker.Bake(Square(20, 5, 15), 20, 20, Options);
        int x = baked.Pad + 10;
        int below = AlphaAt(baked.Main, baked.Width, x, baked.Pad + 15 + 1);
        int above = AlphaAt(baked.Main, baked.Width, x, baked.Pad + 5 - 2);

        Assert.True(below > 60, "shadow below is too faint: " + below);
        Assert.True(below > above * 3, "below " + below + ", above " + above);
    }

    [Fact]
    public void The_backing_glows_around_the_part_and_fades_out()
    {
        BakedPart baked = PaperBaker.Bake(Square(20, 5, 15), 20, 20, Options);
        int y = baked.Pad + 10;
        int near = AlphaAt(baked.Backing, baked.Width, baked.Pad + 15, y);
        int far = AlphaAt(baked.Backing, baked.Width, baked.Width - 1, y);

        Assert.True(near > 50, "edge is too faint: " + near);
        Assert.True(far < 8, "does not fade out: " + far);
    }

    [Fact]
    public void The_edge_outside_the_part_is_light()
    {
        BakedPart baked = PaperBaker.Bake(Square(20, 5, 15), 20, 20, Options);
        int i = (((baked.Pad + 10) * baked.Width) + baked.Pad + 15) * 4;
        double alpha = baked.Backing[i + 3] / 255.0;

        // Un-premultiply: right next to the part it should be mostly the cream edge colour.
        Assert.True(baked.Backing[i + 2] / alpha > 170, "red channel " + baked.Backing[i + 2] / alpha);
    }

    [Fact]
    public void Room_is_left_for_the_blur()
    {
        BakedPart baked = PaperBaker.Bake(Square(20, 5, 15), 20, 20, Options);

        Assert.Equal(20 + (2 * baked.Pad), baked.Width);
        Assert.Equal(20 + (2 * baked.Pad), baked.Height);
        Assert.Equal(baked.Width * baked.Height * 4, baked.Main.Length);
        Assert.Equal(baked.Main.Length, baked.Backing.Length);
        Assert.True(baked.Pad >= 15, "halo reaches 3 sigma: " + baked.Pad);
    }

    [Fact]
    public void Blur_spreads_a_point_and_keeps_its_weight()
    {
        const int Size = 41;
        var plane = new float[Size * Size];
        plane[(20 * Size) + 20] = 1;

        float[] blurred = PaperBaker.Blur(plane, Size, Size, 2.5);

        Assert.InRange(blurred.Sum(), 0.98f, 1.02f);
        Assert.True(blurred[(20 * Size) + 20] < 0.1f);
        Assert.True(blurred[(20 * Size) + 22] > 0.005f);
        Assert.True(blurred[(20 * Size) + 20] > blurred[(20 * Size) + 22], "peak stays at the centre");
    }

    [Fact]
    public void A_tiny_blur_changes_nothing()
    {
        var plane = new float[] { 0, 1, 0, 1 };

        Assert.Equal(plane, PaperBaker.Blur(plane, 2, 2, 0.1));
    }

    [Fact]
    public void Pixels_that_do_not_match_the_size_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => PaperBaker.Bake(new byte[4], 2, 2, Options));
    }
}
