using System.Text.Json;
using System.Text.RegularExpressions;
using Basira.Core.Brain;

namespace Basira.Core.Tests.Brain;

public class ToolCatalogTests
{
    private static JsonElement Schema(ToolSchema tool)
    {
        using var doc = JsonDocument.Parse(tool.InputSchemaJson); // strict JSON: no comments, no trailing commas
        return doc.RootElement.Clone();
    }

    private static Dictionary<string, JsonElement> Properties(ToolSchema tool) =>
        Schema(tool).GetProperty("properties").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

    public static TheoryData<string> ToolNamesData()
    {
        var data = new TheoryData<string>();
        foreach (var t in ToolCatalog.All)
        {
            data.Add(t.Name);
        }

        return data;
    }

    [Fact]
    public void HasEveryToolExactlyOnce()
    {
        var names = ToolCatalog.All.Select(t => t.Name).ToList();
        Assert.Equal(29, names.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ToolNames.All.OrderBy(n => n, StringComparer.Ordinal), names.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ToolNamesData))]
    public void SchemasAreStrictObjects(string name)
    {
        var tool = ToolCatalog.Get(name);
        Assert.Matches(new Regex("^[a-z][a-z_]{1,63}$"), tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.True(tool.Description.Length < 600, "descriptions stay concise");

        var schema = Schema(tool);
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["type", "properties", "required", "additionalProperties"], schema.EnumerateObject().Select(p => p.Name));

        var properties = Properties(tool);
        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Equal(properties.Keys.Order(StringComparer.Ordinal), required.Order(StringComparer.Ordinal));
        Assert.Equal(required.Count, required.Distinct(StringComparer.Ordinal).Count());

        foreach (var (propName, prop) in properties)
        {
            Assert.Matches(new Regex("^[a-z][a-z_]*$"), propName);
            Assert.False(string.IsNullOrWhiteSpace(prop.GetProperty("description").GetString()));
            var type = prop.GetProperty("type");
            if (type.ValueKind == JsonValueKind.Array)
            {
                var types = type.EnumerateArray().Select(t => t.GetString()).ToList();
                Assert.Equal(2, types.Count);
                Assert.Equal("null", types[1]);
                Assert.Contains(types[0], new[] { "string", "integer", "boolean" });
            }
            else
            {
                Assert.Contains(type.GetString(), new[] { "string", "integer", "boolean", "array" });
            }

            if (prop.TryGetProperty("enum", out var values))
            {
                Assert.True(values.GetArrayLength() >= 2);
            }
        }
    }

    public static TheoryData<string, string[]> ExpectedProperties() => new()
    {
        { ToolNames.ReadScreen, ["scope"] },
        { ToolNames.Invoke, ["id"] },
        { ToolNames.SetText, ["id", "text"] },
        { ToolNames.TypeText, ["text"] },
        { ToolNames.PressKeys, ["keys"] },
        { ToolNames.Scroll, ["direction", "amount", "id"] },
        { ToolNames.Focus, ["id"] },
        { ToolNames.ListWindows, [] },
        { ToolNames.SwitchTo, ["window"] },
        { ToolNames.OpenApp, ["name"] },
        { ToolNames.OpenUrl, ["url"] },
        { ToolNames.BrowseSearch, ["query", "engine"] },
        { ToolNames.DescribeScreen, ["question"] },
        { ToolNames.OcrWindow, [] },
        { ToolNames.ReadDocument, ["path", "page", "question"] },
        { ToolNames.Media, ["action"] },
        { ToolNames.ListFolder, ["path"] },
        { ToolNames.FindFiles, ["query", "folder"] },
        { ToolNames.MoveFile, ["from", "to"] },
        { ToolNames.CopyFile, ["from", "to"] },
        { ToolNames.RenameFile, ["path", "new_name"] },
        { ToolNames.Recycle, ["paths"] },
        { ToolNames.SystemStatus, ["what"] },
        { ToolNames.SetVolume, ["level", "mute"] },
        { ToolNames.OpenSettings, ["page"] },
        { ToolNames.WindowsUpdateStatus, [] },
        { ToolNames.RestartPc, [] },
        { ToolNames.Remember, ["fact"] },
        { ToolNames.AskUser, ["question"] },
    };

    [Theory]
    [MemberData(nameof(ExpectedProperties))]
    public void EachToolHasTheSpecifiedInputs(string name, string[] expected) =>
        Assert.Equal(expected, Properties(ToolCatalog.Get(name)).Keys);

    public static TheoryData<string, string, string, bool> PropertyTypes() => new()
    {
        { ToolNames.Invoke, "id", "integer", false },
        { ToolNames.SetText, "id", "integer", false },
        { ToolNames.SetText, "text", "string", false },
        { ToolNames.Focus, "id", "integer", false },
        { ToolNames.Scroll, "id", "integer", true },
        { ToolNames.ReadDocument, "path", "string", true },
        { ToolNames.ReadDocument, "page", "integer", true },
        { ToolNames.ReadDocument, "question", "string", true },
        { ToolNames.FindFiles, "folder", "string", true },
        { ToolNames.FindFiles, "query", "string", false },
        { ToolNames.SetVolume, "level", "integer", true },
        { ToolNames.SetVolume, "mute", "boolean", true },
        { ToolNames.BrowseSearch, "engine", "string", true },
        { ToolNames.Recycle, "paths", "array", false },
    };

    [Theory]
    [MemberData(nameof(PropertyTypes))]
    public void PropertyTypesAndNullability(string tool, string prop, string type, bool nullable)
    {
        var t = Properties(ToolCatalog.Get(tool))[prop].GetProperty("type");
        if (nullable)
        {
            Assert.Equal([type, "null"], t.EnumerateArray().Select(e => e.GetString()));
        }
        else
        {
            Assert.Equal(type, t.GetString());
        }
    }

    public static TheoryData<string, string, string[]> Enums() => new()
    {
        { ToolNames.ReadScreen, "scope", ["window", "focus", "document"] },
        { ToolNames.Scroll, "direction", ["up", "down", "left", "right"] },
        { ToolNames.Scroll, "amount", ["line", "page", "top", "bottom"] },
        { ToolNames.Media, "action", ["play_pause", "next", "previous", "seek_forward", "seek_back"] },
        { ToolNames.SystemStatus, "what", ["battery", "wifi", "volume", "time", "all"] },
    };

    [Theory]
    [MemberData(nameof(Enums))]
    public void EnumValues(string tool, string prop, string[] values) =>
        Assert.Equal(values, Properties(ToolCatalog.Get(tool))[prop].GetProperty("enum").EnumerateArray().Select(e => e.GetString()));

    [Fact]
    public void NullableEnumAllowsNull()
    {
        var engine = Properties(ToolCatalog.Get(ToolNames.BrowseSearch))["engine"].GetProperty("enum").EnumerateArray().ToList();
        Assert.Equal(["google", "youtube"], engine.Take(2).Select(e => e.GetString()));
        Assert.Equal(JsonValueKind.Null, engine[2].ValueKind);
    }

    [Fact]
    public void RecycleTakesAListOfStrings() =>
        Assert.Equal("string", Properties(ToolCatalog.Get(ToolNames.Recycle))["paths"].GetProperty("items").GetProperty("type").GetString());

    [Theory]
    [InlineData(ToolNames.Invoke)]
    [InlineData(ToolNames.SetText)]
    [InlineData(ToolNames.Focus)]
    public void IdToolsExplainWhereIdsComeFrom(string name)
    {
        var id = Properties(ToolCatalog.Get(name))["id"].GetProperty("description").GetString()!;
        Assert.Contains("[12]", id);
        Assert.Contains("screen context", id);
    }

    [Fact]
    public void DescriptionsSteerTheModelTowardSafeUse()
    {
        Assert.Contains("ids", ToolCatalog.Get(ToolNames.ReadScreen).Description);
        Assert.Contains("Prefer", ToolCatalog.Get(ToolNames.Invoke).Description);
        Assert.Contains("passwords", ToolCatalog.Get(ToolNames.SetText).Description);
        Assert.Contains("http", ToolCatalog.Get(ToolNames.OpenUrl).Description);
        Assert.Contains("Never a file path", ToolCatalog.Get(ToolNames.OpenApp).Description);
        Assert.Contains("one call", ToolCatalog.Get(ToolNames.Recycle).Description);
        Assert.Contains("own words", ToolCatalog.Get(ToolNames.Remember).Description);
        Assert.Contains("search for", ToolCatalog.Get(ToolNames.BrowseSearch).Description);
    }

    [Fact]
    public void GetThrowsForUnknownTools()
    {
        Assert.Throws<KeyNotFoundException>(() => ToolCatalog.Get("run_shell"));
        Assert.Throws<KeyNotFoundException>(() => ToolCatalog.Get("Invoke"));
        Assert.True(ToolCatalog.TryGet(ToolNames.Invoke, out var tool));
        Assert.Same(ToolCatalog.Get(ToolNames.Invoke), tool);
        Assert.False(ToolCatalog.TryGet("nope", out _));
    }

    [Fact]
    public void CatalogIsStableBetweenCalls()
    {
        Assert.Same(ToolCatalog.All, ToolCatalog.All);
        Assert.Equal(ToolCatalog.All.Select(t => t.InputSchemaJson), ToolCatalog.All.Select(t => t.InputSchemaJson));
    }

    [Fact]
    public void NoInstallOrShellTools()
    {
        var names = ToolCatalog.All.Select(t => t.Name).ToList();
        Assert.DoesNotContain(names, n => n.Contains("install") || n.Contains("shell") || n.Contains("command") || n.Contains("run"));
    }

    [Fact]
    public void ToolNamesHelpers()
    {
        Assert.True(ToolNames.IsKnown(ToolNames.AskUser));
        Assert.False(ToolNames.IsKnown(null));
        Assert.False(ToolNames.IsKnown("ASK_USER"));
        Assert.All(ToolNames.FileTools, t => Assert.True(ToolNames.IsKnown(t)));
    }
}
