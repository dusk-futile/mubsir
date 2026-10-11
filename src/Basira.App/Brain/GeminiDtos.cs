using System.Text.Json;
using System.Text.Json.Serialization;

namespace Basira.App.Brain;

// Wire types for Gemini generateContent (REST, camelCase JSON). Only the fields Basira uses.

internal sealed class GeminiRequest
{
    public GeminiContent? SystemInstruction { get; set; }

    public List<GeminiContent> Contents { get; set; } = [];

    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

internal sealed class GeminiContent
{
    public string? Role { get; set; }

    public List<GeminiPart>? Parts { get; set; }
}

internal sealed class GeminiPart
{
    public string? Text { get; set; }

    public GeminiInlineData? InlineData { get; set; }

    /// <summary>True on thought-summary parts in responses; those are never transcript text.</summary>
    public bool? Thought { get; set; }
}

internal sealed class GeminiInlineData
{
    public string MimeType { get; set; } = "";

    public string Data { get; set; } = "";
}

internal sealed class GeminiGenerationConfig
{
    public string? ResponseMimeType { get; set; }

    public JsonElement? ResponseSchema { get; set; }

    public GeminiThinkingConfig? ThinkingConfig { get; set; }
}

internal sealed class GeminiThinkingConfig
{
    public string? ThinkingLevel { get; set; }
}

internal sealed class GeminiResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }

    public GeminiPromptFeedback? PromptFeedback { get; set; }

    public GeminiUsage? UsageMetadata { get; set; }
}

internal sealed class GeminiCandidate
{
    public GeminiContent? Content { get; set; }

    public string? FinishReason { get; set; }
}

internal sealed class GeminiPromptFeedback
{
    public string? BlockReason { get; set; }
}

internal sealed class GeminiUsage
{
    public long PromptTokenCount { get; set; }

    public long CandidatesTokenCount { get; set; }

    public long ThoughtsTokenCount { get; set; }

    public long CachedContentTokenCount { get; set; }

    public List<GeminiModalityCount>? PromptTokensDetails { get; set; }
}

internal sealed class GeminiModalityCount
{
    public string? Modality { get; set; }

    public long TokenCount { get; set; }
}

/// <summary>The structured transcript the model returns (snake_case, as in the response schema).</summary>
internal sealed class GeminiTranscript
{
    [JsonPropertyName("transcript")]
    public string? Transcript { get; set; }

    [JsonPropertyName("local_command")]
    public string? LocalCommand { get; set; }

    [JsonPropertyName("local_argument")]
    public string? LocalArgument { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(GeminiRequest))]
[JsonSerializable(typeof(GeminiResponse))]
[JsonSerializable(typeof(GeminiTranscript))]
internal sealed partial class GeminiJson : JsonSerializerContext;
