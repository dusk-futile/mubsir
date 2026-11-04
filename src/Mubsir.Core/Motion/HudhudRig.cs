using System.Text.Json;

namespace Mubsir.Core.Motion;

/// <summary>One cut-out part: its PNG, how much it is scaled, its joint inside the image, where that joint
/// sits on the bird, and its resting angle (clockwise degrees). Units are the art sheets' pixels.</summary>
public sealed record RigPart(string Id, string Image, double ScaleX, double ScaleY, double PivotX, double PivotY, double AtX, double AtY, double Rest);

/// <summary>A crest feather. It slides along the crown and turns between its folded and raised pose,
/// and gets a little shorter as it folds.</summary>
public sealed record RigFeather(
    string Id,
    string Image,
    double ScaleX,
    double ScaleY,
    double PivotX,
    double PivotY,
    double OpenX,
    double OpenY,
    double ClosedX,
    double ClosedY,
    double OpenAngle,
    double ClosedAngle,
    double LengthClosed);

public sealed record RigFace(string Image, double PivotX, double PivotY);

/// <summary>The soft shadow each paper layer casts on the layers under it.</summary>
public sealed record PaperShadow(double OffsetY, double Blur, string Color, double Opacity);

/// <summary>
/// Hudhud's cut-out puppet, read from assets/hudhud/rig.json. The same file drives the HTML prototype
/// (prototypes/hudhud-companion/build.py) and the Windows overlay. The bird faces right in rig units and
/// is mirrored on screen.
/// </summary>
public sealed class HudhudRig
{
    private HudhudRig()
    {
    }

    public double ViewX { get; private init; }

    public double ViewY { get; private init; }

    public double ViewWidth { get; private init; }

    public double ViewHeight { get; private init; }

    public double FeetX { get; private init; }

    public double FeetY { get; private init; }

    /// <summary>The PNGs were saved at this fraction of the art's size (0.45 = 45 %).</summary>
    public double ExportScale { get; private init; }

    public PaperShadow? Shadow { get; private init; }

    public IReadOnlyList<RigPart> BackToFront { get; private init; } = [];

    public double NeckX { get; private init; }

    public double NeckY { get; private init; }

    public double FaceCenterX { get; private init; }

    public double CrestPivotX { get; private init; }

    public double CrestPivotY { get; private init; }

    public IReadOnlyList<RigFeather> Crest { get; private init; } = [];

    public RigFace OpenFace { get; private init; } = new(string.Empty, 0, 0);

    public RigFace ClosedFace { get; private init; } = new(string.Empty, 0, 0);

    /// <summary>x → MirrorX − x turns the right-facing rig into the left-facing bird on screen.</summary>
    public double MirrorX => (2 * ViewX) + ViewWidth;

    public RigPart Part(string id) =>
        BackToFront.FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException("rig has no part " + id);

    /// <summary>Every image file the rig uses.</summary>
    public IEnumerable<string> Images() =>
        BackToFront.Select(p => p.Image).Concat(Crest.Select(f => f.Image)).Append(OpenFace.Image).Append(ClosedFace.Image).Distinct();

    public static HudhudRig Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        double[] view = Numbers(Required(root, "viewBox"), 4, "viewBox");
        double[] feet = Numbers(Required(root, "feet"), 2, "feet");
        JsonElement head = Required(root, "head");
        double[] neck = Numbers(Required(head, "neck"), 2, "head.neck");
        double[] crestPivot = Numbers(Required(head, "crest_pivot"), 2, "head.crest_pivot");

        PaperShadow? shadow = null;
        if (root.TryGetProperty("paper_shadow", out JsonElement sh))
        {
            shadow = new PaperShadow(Number(sh, "dy"), Number(sh, "blur"), Required(sh, "color").GetString() ?? "#000000", Number(sh, "opacity"));
        }

        var parts = new List<RigPart>();
        foreach (JsonElement p in Required(root, "back_to_front").EnumerateArray())
        {
            double s = Optional(p, "s", 1);
            double[] pivot = Numbers(Required(p, "pivot"), 2, "pivot");
            double[] at = Numbers(Required(p, "at"), 2, "at");
            parts.Add(new RigPart(
                Text(p, "id"),
                Text(p, "img"),
                Optional(p, "sx", s),
                Optional(p, "sy", s),
                pivot[0],
                pivot[1],
                at[0],
                at[1],
                Optional(p, "rot", 0)));
        }

        var crest = new List<RigFeather>();
        foreach (JsonElement f in Required(head, "crest_back_to_front").EnumerateArray())
        {
            double s = Optional(f, "s", 1);
            double[] pivot = Numbers(Required(f, "pivot"), 2, "pivot");
            double[] open = Numbers(Required(f, "at"), 2, "at");
            double[] closed = Numbers(Required(f, "at_closed"), 2, "at_closed");
            crest.Add(new RigFeather(
                Text(f, "id"),
                Text(f, "img"),
                Optional(f, "sx", s),
                Optional(f, "sy", s),
                pivot[0],
                pivot[1],
                open[0],
                open[1],
                closed[0],
                closed[1],
                Number(f, "open"),
                Number(f, "closed"),
                Optional(f, "len_closed", 1)));
        }

        return new HudhudRig
        {
            ViewX = view[0],
            ViewY = view[1],
            ViewWidth = view[2],
            ViewHeight = view[3],
            FeetX = feet[0],
            FeetY = feet[1],
            ExportScale = Number(root, "export_scale"),
            Shadow = shadow,
            BackToFront = parts,
            NeckX = neck[0],
            NeckY = neck[1],
            FaceCenterX = Number(head, "face_center_x"),
            CrestPivotX = crestPivot[0],
            CrestPivotY = crestPivot[1],
            Crest = crest,
            OpenFace = Face(Required(head, "open")),
            ClosedFace = Face(Required(head, "closed")),
        };
    }

    private static RigFace Face(JsonElement e)
    {
        double[] pivot = Numbers(Required(e, "pivot"), 2, "face pivot");
        return new RigFace(Text(e, "img"), pivot[0], pivot[1]);
    }

    private static JsonElement Required(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value) ? value : throw new FormatException("rig: missing " + name);

    private static double Number(JsonElement parent, string name) => Required(parent, name).GetDouble();

    private static double Optional(JsonElement parent, string name, double fallback) =>
        parent.TryGetProperty(name, out JsonElement value) ? value.GetDouble() : fallback;

    private static string Text(JsonElement parent, string name) =>
        Required(parent, name).GetString() ?? throw new FormatException("rig: " + name + " must be text");

    private static double[] Numbers(JsonElement value, int count, string what)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != count)
        {
            throw new FormatException("rig: " + what + " must have " + count + " numbers");
        }

        var result = new double[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = value[i].GetDouble();
        }

        return result;
    }
}
