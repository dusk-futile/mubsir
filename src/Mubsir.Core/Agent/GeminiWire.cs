using System.Text.Json;
using System.Text.Json.Nodes;
using Mubsir.Core.Brain;

namespace Mubsir.Core.Agent;

/// <summary>What Mubsir sends to and reads from Gemini when it is the brain. No network here, so all of it can be tested.</summary>
public static class GeminiWire
{
    public const int MaxOutputTokens = 4096;
    public const int KeepContents = 24;

    public sealed record ChunkResult(string? Text, IReadOnlyList<ToolCall> Calls, JsonArray Parts, string? FinishReason, string? BlockReason);

    public static string Request(BrainSessionOptions options, JsonArray contents)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contents);
        string system = string.IsNullOrWhiteSpace(options.ProfileBlock) ? options.SystemPrompt : options.SystemPrompt + "\n" + options.ProfileBlock;
        var request = new JsonObject
        {
            ["system_instruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = system }) },
            ["contents"] = contents.DeepClone(),
            ["generationConfig"] = new JsonObject { ["maxOutputTokens"] = MaxOutputTokens },
        };
        if (options.Tools.Count > 0)
        {
            var declarations = new JsonArray();
            foreach (ToolSchema tool in options.Tools)
            {
                declarations.Add((JsonNode)new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = Schema(tool.InputSchemaJson),
                });
            }

            request["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = declarations });
        }

        return request.ToJsonString();
    }

    /// <summary>Gemini's tool format has no type lists or additionalProperties. A nullable value is written its own way.</summary>
    public static JsonNode Schema(string json)
    {
        JsonNode node = JsonNode.Parse(json) ?? new JsonObject();
        Clean(node);
        return node;
    }

    public static JsonObject UserTurn(UserTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        var parts = new JsonArray();
        if (!string.IsNullOrEmpty(turn.ScreenContextText))
        {
            parts.Add((JsonNode)new JsonObject { ["text"] = turn.ScreenContextText });
        }

        parts.Add((JsonNode)new JsonObject { ["text"] = turn.Text });
        if (turn.JpegImage is { Length: > 0 } jpeg)
        {
            parts.Add((JsonNode)new JsonObject
            {
                ["inlineData"] = new JsonObject { ["mimeType"] = "image/jpeg", ["data"] = Convert.ToBase64String(jpeg) },
            });
        }

        return new JsonObject { ["role"] = "user", ["parts"] = parts };
    }

    public static JsonObject ToolAnswers(IReadOnlyList<ToolResult> results, IReadOnlyDictionary<string, string> namesById)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(namesById);
        var parts = new JsonArray();
        foreach (ToolResult result in results)
        {
            parts.Add((JsonNode)new JsonObject
            {
                ["functionResponse"] = new JsonObject
                {
                    ["name"] = namesById.GetValueOrDefault(result.ToolCallId, "unknown"),
                    ["response"] = new JsonObject { [result.IsError ? "error" : "result"] = result.Content },
                },
            });
        }

        return new JsonObject { ["role"] = "user", ["parts"] = parts };
    }

    /// <summary>
    /// One streamed piece. Parts are kept exactly as they came, because newer models need their thought signatures
    /// sent back with the tool call.
    /// </summary>
    public static ChunkResult Chunk(string data, Func<string> newId)
    {
        ArgumentNullException.ThrowIfNull(newId);
        JsonNode? root = JsonNode.Parse(data);
        string? block = root?["promptFeedback"]?["blockReason"]?.GetValue<string>();
        JsonNode? candidate = root?["candidates"]?[0];
        string? finish = candidate?["finishReason"]?.GetValue<string>();
        var kept = new JsonArray();
        var calls = new List<ToolCall>();
        string? text = null;
        if (candidate?["content"]?["parts"] is JsonArray parts)
        {
            foreach (JsonNode? part in parts)
            {
                if (part is null)
                {
                    continue;
                }

                kept.Add(part.DeepClone());
                if (part["thought"]?.GetValue<bool>() == true)
                {
                    continue;
                }

                if (part["text"]?.GetValue<string>() is { Length: > 0 } piece)
                {
                    text += piece;
                }

                if (part["functionCall"] is JsonObject call && call["name"]?.GetValue<string>() is { Length: > 0 } name)
                {
                    using JsonDocument args = JsonDocument.Parse(call["args"]?.ToJsonString() ?? "{}");
                    calls.Add(new ToolCall(call["id"]?.GetValue<string>() ?? newId(), name, args.RootElement.Clone()));
                }
            }
        }

        return new ChunkResult(text, calls, kept, finish, block);
    }

    /// <summary>Long tasks drop their oldest steps, keeping the first request. It always cuts where a model turn starts, so calls and answers stay paired.</summary>
    public static void Trim(JsonArray contents, int keep = KeepContents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        while (contents.Count > keep)
        {
            contents.RemoveAt(1);
            while (contents.Count > 1 && contents[1]?["role"]?.GetValue<string>() != "model")
            {
                contents.RemoveAt(1);
            }
        }
    }

    public static BrainErrorKind KindOf(int status, string? body) => status switch
    {
        400 when body?.Contains("API_KEY_INVALID", StringComparison.Ordinal) == true => BrainErrorKind.Auth,
        400 => BrainErrorKind.BadRequest,
        401 or 403 => BrainErrorKind.Auth,
        402 => BrainErrorKind.NoCredit,
        429 => BrainErrorKind.RateLimited,
        >= 500 => BrainErrorKind.Overloaded,
        _ => BrainErrorKind.Other,
    };

    private static void Clean(JsonNode? node)
    {
        if (node is JsonObject o)
        {
            o.Remove("additionalProperties");
            o.Remove("strict");
            o.Remove("$schema");
            if (o["type"] is JsonArray types)
            {
                string? real = types.Select(t => t?.GetValue<string>()).FirstOrDefault(t => t != "null");
                bool nullable = types.Any(t => t?.GetValue<string>() == "null");
                o["type"] = real;
                if (nullable)
                {
                    o["nullable"] = true;
                }
            }

            if (o["enum"] is JsonArray values)
            {
                for (int i = values.Count - 1; i >= 0; i--)
                {
                    if (values[i] is null)
                    {
                        values.RemoveAt(i);
                        o["nullable"] = true;
                    }
                }
            }

            foreach (KeyValuePair<string, JsonNode?> pair in o.ToArray())
            {
                Clean(pair.Value);
            }
        }
        else if (node is JsonArray a)
        {
            foreach (JsonNode? item in a)
            {
                Clean(item);
            }
        }
    }
}
