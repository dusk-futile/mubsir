using System.Text.Json;
using System.Text.Json.Nodes;
using Mubsir.Core.Brain;

namespace Mubsir.Core.Agent;

/// <summary>
/// The chat format most AI services share (OpenAI's "chat completions"), so the brain can be any service the
/// person gives an address for: OpenRouter, a router such as OmniRoute, or a model running on this PC.
/// </summary>
public static class OpenAiWire
{
    private const int MaxOutputTokens = 2048;
    private const string Path = "/chat/completions";

    public sealed record Answer(JsonObject Message, string? Text, IReadOnlyList<ToolCall> Calls);

    /// <summary>
    /// Where to send requests, from the address the person typed, such as https://openrouter.ai/api/v1 or
    /// http://localhost:11434/v1. Null if it isn't usable. Plain http is allowed only for this PC, because
    /// what's on the screen goes over this connection.
    /// </summary>
    public static Uri? Endpoint(string? address)
    {
        string text = (address ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) || uri.Host.Length == 0
            || !(uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
        {
            return null;
        }

        return text.EndsWith(Path, StringComparison.OrdinalIgnoreCase) ? uri : new Uri(text + Path);
    }

    public static string Request(BrainSessionOptions options, JsonArray messages, string model)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(messages);
        string system = string.IsNullOrWhiteSpace(options.ProfileBlock) ? options.SystemPrompt : options.SystemPrompt + "\n" + options.ProfileBlock;
        var all = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = system });
        foreach (JsonNode? message in messages)
        {
            all.Add(message?.DeepClone());
        }

        var request = new JsonObject { ["model"] = model, ["messages"] = all, ["max_tokens"] = MaxOutputTokens, ["stream"] = false };
        if (options.Tools.Count > 0)
        {
            var tools = new JsonArray();
            foreach (ToolSchema tool in options.Tools)
            {
                tools.Add((JsonNode)new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = tool.Name, ["description"] = tool.Description, ["parameters"] = JsonNode.Parse(tool.InputSchemaJson) },
                });
            }

            request["tools"] = tools;
        }

        return request.ToJsonString();
    }

    public static JsonObject UserTurn(UserTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        string text = string.IsNullOrEmpty(turn.ScreenContextText) ? turn.Text : turn.ScreenContextText + "\n\n" + turn.Text;
        if (turn.JpegImage is not { Length: > 0 } jpeg)
        {
            return new JsonObject { ["role"] = "user", ["content"] = text };
        }

        return new JsonObject
        {
            ["role"] = "user",
            ["content"] = new JsonArray(
                new JsonObject { ["type"] = "text", ["text"] = text },
                new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg) } }),
        };
    }

    public static IEnumerable<JsonObject> ToolAnswers(IReadOnlyList<ToolResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Select(r => new JsonObject { ["role"] = "tool", ["tool_call_id"] = r.ToolCallId, ["content"] = r.IsError ? "Error: " + r.Content : r.Content });
    }

    /// <summary>Reads one answer. Throws <see cref="JsonException"/> or <see cref="InvalidOperationException"/> if it isn't one.</summary>
    public static Answer Parse(string json, Func<string> newId)
    {
        ArgumentNullException.ThrowIfNull(newId);
        if (JsonNode.Parse(json)?["choices"]?[0]?["message"] is not JsonObject message)
        {
            throw new InvalidOperationException("no message in the answer");
        }

        var calls = new List<ToolCall>();
        foreach (JsonNode? call in message["tool_calls"] as JsonArray ?? [])
        {
            if (call?["function"]?["name"]?.GetValue<string>() is not { Length: > 0 } name)
            {
                continue;
            }

            if (call["id"]?.GetValue<string>() is not { Length: > 0 } id)
            {
                call["id"] = id = newId();
            }

            calls.Add(new ToolCall(id, name, Arguments(call["function"]?["arguments"])));
        }

        string? text = message["content"] is JsonValue content && content.TryGetValue(out string? said) && !string.IsNullOrWhiteSpace(said) ? said : null;
        return new Answer((JsonObject)message.DeepClone(), text, calls);
    }

    /// <summary>Most services send the arguments as text holding JSON; a few send the object itself.</summary>
    private static JsonElement Arguments(JsonNode? node)
    {
        try
        {
            string json = node is JsonValue value && value.TryGetValue(out string? text) ? text : node?.ToJsonString() ?? "{}";
            using JsonDocument parsed = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
        }
    }
}
