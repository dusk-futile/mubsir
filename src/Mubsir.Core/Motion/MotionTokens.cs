using System.Text.Json;

namespace Mubsir.Core.Motion;

public readonly record struct Size2(double Width, double Height);

/// <summary>
/// Sizes, springs and colours for the notch, read from motion-tokens.json. The values
/// come from the approved HTML prototype, so the native overlay moves the way the owner signed off on.
/// </summary>
public sealed class MotionTokens
{
    private static readonly string[] RequiredSprings =
        ["notchWidth", "notchHeight", "content", "voice", "glow"];

    private MotionTokens(
        IReadOnlyDictionary<string, SpringToken> springs,
        IReadOnlyDictionary<CompanionState, Size2> notchSizes,
        double notchMaxRadius,
        double notchFilletMax,
        double contentBlur,
        double contentDelayMs,
        IReadOnlyDictionary<string, string> colors)
    {
        Springs = springs;
        NotchSizes = notchSizes;
        NotchMaxRadius = notchMaxRadius;
        NotchFilletMax = notchFilletMax;
        ContentBlur = contentBlur;
        ContentDelayMs = contentDelayMs;
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
