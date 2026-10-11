using System.Text.Json;
using Basira.Core.Brain;
using Basira.Core.Common;

namespace Basira.Core.Agent;

/// <summary>Quick answers send no tools and take one turn; the agent mode loops over tool calls.</summary>
public enum BrainMode
{
    QuickAnswer,
    Agent,
}

/// <summary>Everything a provider session needs up front. System prompt, tools and profile form the cached prefix.</summary>
public sealed record BrainSessionOptions(
    BrainMode Mode,
    string SystemPrompt,
    string ProfileBlock,
    IReadOnlyList<ToolSchema> Tools,
    bool AllowWebSearch,
    Lang Lang);

/// <summary>One input to a brain session.</summary>
public abstract record BrainInput;

/// <summary>The user's request with the (already redacted) screen context and an optional announced screenshot.</summary>
public sealed record UserTurn(string Text, string? ScreenContextText, byte[]? JpegImage) : BrainInput;

/// <summary>Results for every tool call of the previous turn, in one message.</summary>
public sealed record ToolResults(IReadOnlyList<ToolResult> Results) : BrainInput;

/// <summary>A tool call requested by the model.</summary>
public sealed record ToolCall(string Id, string Name, JsonElement Arguments);

/// <summary>The result returned to the model for one tool call.</summary>
public sealed record ToolResult(string ToolCallId, string Content, bool IsError = false);

public enum BrainErrorKind
{
    Network,
    RateLimited,
    Overloaded,
    Auth,
    NoApiKey,
    Refusal,
    BadRequest,
    Other,
}

/// <summary>One streamed event from a brain session.</summary>
public abstract record BrainEvent;

public sealed record TextDelta(string Text) : BrainEvent;

public sealed record ToolCallRequested(ToolCall Call) : BrainEvent;

public sealed record UsageReported(UsageRecord Usage) : BrainEvent;

/// <summary>The model finished its turn (e.g. "end_turn", "tool_use", "max_tokens").</summary>
public sealed record TurnCompleted(string StopReason) : BrainEvent;

/// <summary>The call failed; <paramref name="Code"/> is a short provider code, safe to log.</summary>
public sealed record BrainFailed(BrainErrorKind Kind, string Code) : BrainEvent;

/// <summary>
/// One conversation with a model. Provider-specific history (thinking blocks, cache markers) stays inside the
/// implementation, so the loop only sends new inputs.
/// </summary>
public interface IBrainSession : IAsyncDisposable
{
    IAsyncEnumerable<BrainEvent> SendAsync(BrainInput input, CancellationToken ct);
}

public interface IBrain
{
    IBrainSession StartSession(BrainSessionOptions options);
}
