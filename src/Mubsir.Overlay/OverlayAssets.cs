using System.IO;
using System.Reflection;
using Mubsir.Core.Motion;

namespace Mubsir.Overlay;

/// <summary>The notch's tokens and colours, embedded in this assembly.</summary>
internal sealed class OverlayAssets
{
    private OverlayAssets(MotionTokens tokens)
    {
        Tokens = tokens;
        Palette = new Palette(tokens);
    }

    public MotionTokens Tokens { get; }

    public Palette Palette { get; }

    public static OverlayAssets Load()
    {
        const string name = "Mubsir.Overlay.motion-tokens.json";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new FileNotFoundException("missing resource " + name);
        using var reader = new StreamReader(stream);
        return new OverlayAssets(MotionTokens.Parse(reader.ReadToEnd()));
    }
}
