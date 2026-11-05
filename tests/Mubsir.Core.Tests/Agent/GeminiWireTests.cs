using System.Text.Json;
using System.Text.Json.Nodes;
using Mubsir.Core.Agent;
using Mubsir.Core.Brain;
using Mubsir.Core.Common;
using Xunit;

namespace Mubsir.Core.Tests.Agent;

public class GeminiWireTests
{
    private static BrainSessionOptions Options(IReadOnlyList<ToolSchema>? tools = null) =>
        new(BrainMode.Agent, "Be brief.", "Name: Sami", tools ?? ToolCatalog.All, AllowWebSearch: false, Lang.Arabic);

    [Fact]
    public void Every_tool_becomes_a_format_gemini_accepts()
    {
        JsonNode request = JsonNode.Parse(GeminiWire.Request(Options(), new JsonArray(GeminiWire.UserTurn(new UserTurn("hi", null, null)))))!;
        string json = request["tools"]!.ToJsonString();
        Assert.DoesNotContain("additionalProperties", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"type\":[", json, StringComparison.Ordinal);
        Assert.DoesNotContain("null,", json.Replace("\"nullable\"", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(ToolCatalog.All.Count, request["tools"]![0]!["functionDeclarations"]!.AsArray().Count);
        Assert.Contains("Name: Sami", request["system_instruction"]!.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_nullable_choice_loses_its_null_and_says_nullable()
    {
        JsonNode schema = GeminiWire.Schema("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"e\":{\"type\":[\"string\",\"null\"],\"enum\":[\"a\",null]}}}");
        JsonNode e = schema["properties"]!["e"]!;
        Assert.Equal("string", e["type"]!.GetValue<string>());
        Assert.True(e["nullable"]!.GetValue<bool>());
        Assert.Single(e["enum"]!.AsArray());
    }

    [Fact]
    public void A_tool_call_keeps_its_thought_signature_for_the_next_turn()
    {
        const string Data = "{\"candidates\":[{\"content\":{\"parts\":[{\"functionCall\":{\"name\":\"read_screen\",\"args\":{\"scope\":\"window\"}},\"thoughtSignature\":\"abc\"}]},\"finishReason\":\"STOP\"}]}";
        GeminiWire.ChunkResult chunk = GeminiWire.Chunk(Data, () => "id1");
        ToolCall call = Assert.Single(chunk.Calls);
        Assert.Equal(("id1", "read_screen", "window"), (call.Id, call.Name, call.Arguments.GetProperty("scope").GetString()));
        Assert.Contains("abc", chunk.Parts.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal("STOP", chunk.FinishReason);
    }

    [Fact]
    public void Thinking_is_never_spoken_but_the_answer_is()
    {
        const string Data = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"secret plan\",\"thought\":true},{\"text\":\"مرحبا\"}]}}]}";
        Assert.Equal("مرحبا", GeminiWire.Chunk(Data, () => "x").Text);
    }

    [Fact]
    public void A_blocked_prompt_is_seen() =>
        Assert.Equal("SAFETY", GeminiWire.Chunk("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}", () => "x").BlockReason);

    [Fact]
    public void Tool_answers_use_the_tool_name_and_mark_errors()
    {
        JsonObject answer = GeminiWire.ToolAnswers([new ToolResult("1", "done"), new ToolResult("2", "no", IsError: true)], new Dictionary<string, string> { ["1"] = "a", ["2"] = "b" });
        Assert.Equal("a", answer["parts"]![0]!["functionResponse"]!["name"]!.GetValue<string>());
        Assert.Equal("no", answer["parts"]![1]!["functionResponse"]!["response"]!["error"]!.GetValue<string>());
    }

    [Fact]
    public void A_picture_goes_with_the_turn() =>
        Assert.Contains("image/jpeg", GeminiWire.UserTurn(new UserTurn("look", "screen", [1, 2, 3])).ToJsonString(), StringComparison.Ordinal);

    [Fact]
    public void Trimming_keeps_the_request_and_never_starts_on_a_tool_answer()
    {
        var contents = new JsonArray { new JsonObject { ["role"] = "user", ["n"] = "first" } };
        for (int i = 0; i < 40; i++)
        {
            contents.Add(new JsonObject { ["role"] = "model" });
            contents.Add(new JsonObject { ["role"] = "user" });
        }

        GeminiWire.Trim(contents);
        Assert.True(contents.Count <= GeminiWire.KeepContents);
        Assert.Equal("first", contents[0]!["n"]!.GetValue<string>());
        Assert.Equal("model", contents[1]!["role"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(400, "API_KEY_INVALID", BrainErrorKind.Auth)]
    [InlineData(400, "other", BrainErrorKind.BadRequest)]
    [InlineData(403, null, BrainErrorKind.Auth)]
    [InlineData(429, null, BrainErrorKind.RateLimited)]
    [InlineData(503, null, BrainErrorKind.Overloaded)]
    public void Each_failure_gets_the_right_message(int status, string? body, BrainErrorKind kind) =>
        Assert.Equal(kind, GeminiWire.KindOf(status, body));
}
