using System.Text.Json;

namespace Mubsir.Core.Motion;

public readonly record struct Size2(double Width, double Height);

/// <summary>Where Hudhud's crest, head, wing and crest scale rest in one state (see the prototype's POSE table).</summary>
public readonly record struct HudhudPose(double Crest, double Tilt, double Raise, double Wing, double Spread);

public sealed class HudhudTokens
{
    internal HudhudTokens(Size2 size, double marginRight, double marginBottom, double sleepAfterSeconds, IReadOnlyDictionary<string, HudhudPose> poses)
    {
        Size = size;
        MarginRight = marginRight;
        MarginBottom = marginBottom;
        SleepAfterSeconds = sleepAfterSeconds;
        Poses = poses;
    }

    /// <summary>Hudhud's box in device-independent pixels.</summary>
    public Size2 Size { get; }

    public double MarginRight { get; }

    /// <summary>Gap between Hudhud's feet and the top of the taskbar.</summary>
    public double MarginBottom { get; }

    public double SleepAfterSeconds { get; }

    public IReadOnlyDictionary<string, HudhudPose> Poses { get; }

    /// <summary>The pose for a state name, or idle's when the state has none of its own.</summary>
    public HudhudPose Pose(string name) => Poses.TryGetValue(name, out HudhudPose pose) ? pose : Poses["idle"];
}

/// <summary>
/// Sizes, springs and colours for the normal-mode companion, read from motion-tokens.json. The values
/// come from the approved HTML prototype, so the native overlay moves the way the owner signed off on.
/// </summary>
public sealed class MotionTokens
{
    private static readonly string[] RequiredSprings =
        ["notchWidth", "notchHeight", "content", "voice", "crest", "head", "wing", "turn", "body", "glow", "sleep", "panel"];

    private MotionTokens(
        IReadOnlyDictionary<string, SpringToken> springs,
        IReadOnlyDictionary<CompanionState, Size2> notchSizes,
        double notchMaxRadius,
        double notchFilletMax,
        double contentBlur,
        double contentDelayMs,
        HudhudTokens hudhud,
        IReadOnlyDictionary<string, string> colors)
    {
        Springs = springs;
        NotchSizes = notchSizes;
        NotchMaxRadius = notchMaxRadius;
        NotchFilletMax = notchFilletMax;
        ContentBlur = contentBlur;
        ContentDelayMs = contentDelayMs;
        Hudhud = hudhud;
        Colors = colors;
    }

    public IReadOnlyDictionary<string, SpringToken> Springs { get; }

    public IReadOnlyDictionary<CompanionState, Size2> NotchSizes { get; }

    /// <summary>Largest bottom corner radius of the notch.</summary>
    public double NotchMaxRadius { get; }

    /// <summary>Largest concave fillet where the notch meets the top edge of the screen.</summary>
    public double NotchFilletMax { get; }

    /// <summary>Blur (px) a pane starts from when it fades in.</summary>
    public double ContentBlur { get; }

    /// <summary>Delay before a new pane starts to appear, so the notch leads and the content follows.</summary>
    public double ContentDelayMs { get; }

    public HudhudTokens Hudhud { get; }

    /// <summary>Named colours as #RRGGBB strings.</summary>
    public IReadOnlyDictionary<string, string> Colors { get; }

    public SpringToken Spring(string name) =>
        Springs.TryGetValue(name, out SpringToken token) ? token : throw new KeyNotFoundException("no spring named " + name);

    public static MotionTokens Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        JsonElement root = doc.RootElement;

        var springs = new Dictionary<string, SpringToken>(StringComparer.Ordinal);
        foreach (JsonProperty p in Required(root, "springs").EnumerateObject())
        {
            springs[p.Name] = new SpringToken(Number(p.Value, "response"), Number(p.Value, "damping"));
        }

        foreach (string name in RequiredSprings)
        {
            if (!springs.ContainsKey(name))
            {
                throw new FormatException("motion tokens: missing spring " + name);
            }
        }

        JsonElement notch = Required(root, "notch");
        var sizes = new Dictionary<CompanionState, Size2>();
        foreach (JsonProperty p in Required(notch, "sizes").EnumerateObject())
        {
            if (!CompanionStates.TryParse(p.Name, out CompanionState state))
            {
                throw new FormatException("motion tokens: unknown notch state " + p.Name);
            }

            sizes[state] = Pair(p.Value, "notch.sizes." + p.Name);
        }

        foreach (CompanionState state in Enum.GetValues<CompanionState>())
        {
            if (!sizes.ContainsKey(state))
            {
                throw new FormatException("motion tokens: no notch size for " + state.Key());
            }
        }

        JsonElement hud = Required(root, "hudhud");
        Size2 margin = Pair(Required(hud, "margin"), "hudhud.margin");
        var poses = new Dictionary<string, HudhudPose>(StringComparer.Ordinal);
        foreach (JsonProperty p in Required(hud, "poses").EnumerateObject())
        {
            poses[p.Name] = new HudhudPose(
                Number(p.Value, "crest"),
                Optional(p.Value, "tilt"),
                Optional(p.Value, "raise"),
                Optional(p.Value, "wing"),
                Optional(p.Value, "spread"));
        }

        if (!poses.ContainsKey("idle") || !poses.ContainsKey("sleep"))
        {
            throw new FormatException("motion tokens: hudhud.poses needs idle and sleep");
        }

        var hudhud = new HudhudTokens(
            Pair(Required(hud, "size"), "hudhud.size"),
            margin.Width,
            margin.Height,
            Number(hud, "sleepAfterSeconds"),
            poses);

        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty p in Required(root, "colors").EnumerateObject())
        {
            string value = p.Value.GetString() ?? string.Empty;
            if (value.Length != 7 || value[0] != '#')
            {
                throw new FormatException("motion tokens: colour " + p.Name + " must be #RRGGBB");
            }

            colors[p.Name] = value;
        }

        return new MotionTokens(
            springs,
            sizes,
            Number(notch, "maxRadius"),
            Number(notch, "filletMax"),
            Number(notch, "contentBlur"),
            Optional(notch, "contentDelayMs"),
            hudhud,
            colors);
    }

    private static JsonElement Required(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value) ? value : throw new FormatException("motion tokens: missing " + name);

    private static double Number(JsonElement parent, string name) => Required(parent, name).GetDouble();

    private static double Optional(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value) ? value.GetDouble() : 0;

    private static Size2 Pair(JsonElement value, string what)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2)
        {
            throw new FormatException("motion tokens: " + what + " must be [width, height]");
        }

        return new Size2(value[0].GetDouble(), value[1].GetDouble());
    }
}
