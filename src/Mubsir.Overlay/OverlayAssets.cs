using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Mubsir.Core.Motion;

namespace Mubsir.Overlay;

/// <summary>The tokens, the rig and Hudhud's part images, all embedded in this assembly.</summary>
internal sealed class OverlayAssets
{
    private readonly Dictionary<string, BitmapSource> _images;

    private OverlayAssets(MotionTokens tokens, HudhudRig rig, Dictionary<string, BitmapSource> images)
    {
        Tokens = tokens;
        Rig = rig;
        _images = images;
        Palette = new Palette(tokens);
    }

    public MotionTokens Tokens { get; }

    public HudhudRig Rig { get; }

    public IReadOnlyDictionary<string, BitmapSource> Images => _images;

    public Palette Palette { get; }

    /// <summary>Frees the full-size pictures once Hudhud has made its own screen-sized copies.</summary>
    public void ReleaseImages() => _images.Clear();

    public static OverlayAssets Load()
    {
        MotionTokens tokens = MotionTokens.Parse(ReadText("Mubsir.Overlay.motion-tokens.json"));
        HudhudRig rig = HudhudRig.Parse(ReadText("Mubsir.Overlay.Hudhud.rig.json"));
        var images = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);
        foreach (string image in rig.Images())
        {
            images[image] = ReadPng("Mubsir.Overlay.Hudhud." + image + ".png");
        }

        return new OverlayAssets(tokens, rig, images);
    }

    private static Stream Open(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new FileNotFoundException("missing resource " + name);

    private static string ReadText(string name)
    {
        using Stream stream = Open(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static BitmapSource ReadPng(string name)
    {
        using Stream stream = Open(name);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }
}
