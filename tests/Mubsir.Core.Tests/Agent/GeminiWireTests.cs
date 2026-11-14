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

    [Theory]
    [InlineData("افتح يوتيوب", true)]
    [InlineData("open youtube", true)]
    [InlineData("امسح الملف", true)]
    [InlineData("what time is it", false)]
    [InlineData("الكتاب ده بيتكلم عن إيه", false)]
    [InlineData(null, false)]
    public void Requests_to_act_are_recognised(string? said, bool acts) => Assert.Equal(acts, GeminiWire.AsksForAction(said));

    [Fact]
    public void A_request_that_must_act_forces_a_tool()
    {
        string json = GeminiWire.Request(Options(), [GeminiWire.UserTurn(new UserTurn("hi", null, null))], mustUseTool: true);
        Assert.Contains("\"mode\":\"ANY\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("toolConfig", GeminiWire.Request(Options(), [GeminiWire.UserTurn(new UserTurn("hi", null, null))]), StringComparison.Ordinal);
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

    [Theory]
    [InlineData("https://openrouter.ai/api/v1", "https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("https://openrouter.ai/api/v1/", "https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("http://localhost:20128/v1", "http://localhost:20128/v1/chat/completions")]
    [InlineData("http://127.0.0.1:11434/v1/chat/completions", "http://127.0.0.1:11434/v1/chat/completions")]
    [InlineData("http://example.com/v1", null)]
    [InlineData("ftp://example.com", null)]
    [InlineData("openrouter", null)]
    [InlineData("", null)]
    public void A_custom_address_must_be_https_unless_it_is_this_pc(string address, string? endpoint) =>
        Assert.Equal(endpoint, OpenAiWire.Endpoint(address)?.ToString());

    [Fact]
    public void The_common_chat_format_carries_the_prompt_the_tools_and_the_turns()
    {
        var messages = new JsonArray(OpenAiWire.UserTurn(new UserTurn("open mail", "[1] button \"Mail\"", null)));
        foreach (JsonObject answer in OpenAiWire.ToolAnswers([new ToolResult("call_1", "done"), new ToolResult("call_2", "no such window", IsError: true)]))
        {
            messages.Add((JsonNode)answer);
        }

        JsonNode request = JsonNode.Parse(OpenAiWire.Request(Options(), messages, "some/model"))!;

        Assert.Equal("some/model", request["model"]!.GetValue<string>());
        Assert.Equal("system", request["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Contains("Name: Sami", request["messages"]![0]!["content"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("[1] button \"Mail\"\n\nopen mail", request["messages"]![1]!["content"]!.GetValue<string>());
        Assert.Equal("Error: no such window", request["messages"]![3]!["content"]!.GetValue<string>());
        Assert.Equal(ToolCatalog.All.Count, request["tools"]!.AsArray().Count);
        Assert.Equal("object", request["tools"]![0]!["function"]!["parameters"]!["type"]!.GetValue<string>());
        Assert.Null(JsonNode.Parse(OpenAiWire.Request(Options([]), messages, "m"))!["tools"]);

        JsonNode picture = OpenAiWire.UserTurn(new UserTurn("what is this", null, [1, 2, 3]));
        Assert.Equal("data:image/jpeg;base64,AQID", picture["content"]![1]!["image_url"]!["url"]!.GetValue<string>());
    }

    [Fact]
    public void An_answer_gives_its_words_and_its_tool_calls_however_the_arguments_are_sent()
    {
        const string json = """
            {"choices":[{"finish_reason":"tool_calls","message":{"role":"assistant","content":"Opening Mail.","tool_calls":[
              {"id":"call_9","type":"function","function":{"name":"open_app","arguments":"{\"name\":\"Mail\"}"}},
              {"type":"function","function":{"name":"read_screen","arguments":{}}},
              {"id":"call_x","type":"function","function":{"name":"wait","arguments":"not json"}}]}}]}
            """;

        OpenAiWire.Answer answer = OpenAiWire.Parse(json, () => "made_up");

        Assert.Equal("Opening Mail.", answer.Text);
        Assert.Equal(["call_9", "made_up", "call_x"], answer.Calls.Select(c => c.Id));
        Assert.Equal("Mail", answer.Calls[0].Arguments.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Object, answer.Calls[2].Arguments.ValueKind);
        Assert.Equal("made_up", answer.Message["tool_calls"]![1]!["id"]!.GetValue<string>());

        Assert.Null(OpenAiWire.Parse("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null}}]}", () => "x").Text);
        Assert.Throws<InvalidOperationException>(() => OpenAiWire.Parse("{\"error\":{\"message\":\"bad key\"}}", () => "x"));
    }

    [Fact]
    public void Only_the_newest_screen_snapshot_is_kept_in_geminis_and_the_common_formats()
    {
        var names = new Dictionary<string, string> { ["a"] = "read_screen", ["b"] = "list_windows", ["c"] = "read_screen" };
        var contents = new JsonArray(
            GeminiWire.UserTurn(new UserTurn("open mail", null, null)),
            GeminiWire.ToolAnswers([new ToolResult("a", "OLD SCREEN"), new ToolResult("b", "Mail, Chrome")], names),
            GeminiWire.ToolAnswers([new ToolResult("c", "NEW SCREEN")], names));

        GeminiWire.Trim(contents);

        string sent = contents.ToJsonString();
        Assert.DoesNotContain("OLD SCREEN", sent, StringComparison.Ordinal);
        Assert.Contains("NEW SCREEN", sent, StringComparison.Ordinal);
        Assert.Contains("Mail, Chrome", sent, StringComparison.Ordinal);

        var messages = new JsonArray();
        void Add(string json) => messages.Add(JsonNode.Parse(json));
        Add("{\"role\":\"assistant\",\"tool_calls\":[{\"id\":\"a\",\"function\":{\"name\":\"read_screen\"}},{\"id\":\"b\",\"function\":{\"name\":\"list_windows\"}}]}");
        Add("{\"role\":\"tool\",\"tool_call_id\":\"a\",\"content\":\"OLD SCREEN\"}");
        Add("{\"role\":\"tool\",\"tool_call_id\":\"b\",\"content\":\"Mail, Chrome\"}");
        Add("{\"role\":\"assistant\",\"tool_calls\":[{\"id\":\"c\",\"function\":{\"name\":\"read_screen\"}}]}");
        Add("{\"role\":\"tool\",\"tool_call_id\":\"c\",\"content\":\"NEW SCREEN\"}");

        OpenAiWire.ShortenOldSnapshots(messages);

        Assert.Equal(ConversationTrimmer.OldSnapshotNote, messages[1]!["content"]!.GetValue<string>());
        Assert.Equal("Mail, Chrome", messages[2]!["content"]!.GetValue<string>());
        Assert.Equal("NEW SCREEN", messages[4]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void The_part_of_the_prompt_that_is_cached_is_the_same_on_every_request_and_for_every_user()
    {
        string arabic = SystemPrompt.Build(Lang.Arabic, ReplyStyle.Dialect, Verbosity.Brief, Mubsir.Core.Config.UserKind.Blind, "Jarvis");
        string english = SystemPrompt.Build(Lang.English, ReplyStyle.SimpleMsa, Verbosity.Detailed);
        int shared = arabic.Zip(english).TakeWhile(pair => pair.First == pair.Second).Count();

        Assert.Equal(arabic, SystemPrompt.Build(Lang.Arabic, ReplyStyle.Dialect, Verbosity.Brief, Mubsir.Core.Config.UserKind.Blind, "Jarvis"));
        Assert.True(shared > arabic.Length * 0.8, $"only {shared} of {arabic.Length} characters are shared");
        Assert.Contains("HOW TO SPEAK", arabic[..shared], StringComparison.Ordinal);
        Assert.Equal(ToolCatalog.All.Select(t => t.InputSchemaJson), ToolCatalog.All.Select(t => t.InputSchemaJson));
    }
}
