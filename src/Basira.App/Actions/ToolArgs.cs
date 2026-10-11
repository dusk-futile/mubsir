using System.Globalization;
using System.Text.Json;

namespace Basira.App.Actions;

/// <summary>Pure conversion of a tool call's JSON arguments to the string map the policy reads.</summary>
internal static class ToolArgs
{
    /// <summary>Top-level properties as strings; arrays become newline-joined lists, null stays null.</summary>
    public static Dictionary<string, string?> Parse(JsonElement arguments)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var p in arguments.EnumerateObject())
        {
            result[p.Name] = AsString(p.Value);
        }

        return result;
    }

    public static string? AsString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Array => string.Join('\n', v.EnumerateArray().Select(AsString).Where(s => !string.IsNullOrWhiteSpace(s))),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => v.GetRawText(),
    };

    public static string? Str(IReadOnlyDictionary<string, string?> args, string key) =>
        args.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    public static int? Int(IReadOnlyDictionary<string, string?> args, string key)
    {
        if (Str(args, key) is not { } s)
        {
            return null;
        }

        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
        {
            return i;
        }

        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && d is >= int.MinValue and <= int.MaxValue
            ? (int)Math.Round(d)
            : null;
    }

    public static bool? Bool(IReadOnlyDictionary<string, string?> args, string key) => Str(args, key) switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

    public static List<string> List(IReadOnlyDictionary<string, string?> args, string key) =>
        Str(args, key) is { } s
            ? s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];
}
