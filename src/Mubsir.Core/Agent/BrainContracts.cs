using System.Text.Json;
using Mubsir.Core.Brain;
using Mubsir.Core.Common;

namespace Mubsir.Core.Agent;

public enum BrainMode
{
    QuickAnswer,
    Agent,
}

public sealed record BrainSessionOptions(
    BrainMode Mode,
    string SystemPrompt,
    string ProfileBlock,
    IReadOnlyList<ToolSchema> Tools,
    bool AllowWebSearch,
    Lang Lang);

public abstract record BrainInput;

public sealed record UserTurn(string Text, string? ScreenContextText, byte[]? JpegImage) : BrainInput;

public sealed record ToolResults(IReadOnlyList<ToolResult> Results) : BrainInput;

public sealed record ToolCall(string Id, string Name, JsonElement Arguments);

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

public abstract record BrainEvent;

public sealed record TextDelta(string Text) : BrainEvent;

public sealed record ToolCallRequested(ToolCall Call) : BrainEvent;

public sealed record UsageReported(UsageRecord Usage) : BrainEvent;

public sealed record TurnCompleted(string StopReason) : BrainEvent;

public sealed record BrainFailed(BrainErrorKind Kind, string Code) : BrainEvent;

public interface IBrainSession : IAsyncDisposable
{
    IAsyncEnumerable<BrainEvent> SendAsync(BrainInput input, CancellationToken ct);
}

public interface IBrain
{
    IBrainSession StartSession(BrainSessionOptions options);
}
