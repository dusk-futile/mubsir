using System.Diagnostics;
using System.Runtime.CompilerServices;
using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Logging;
using Sdk = Anthropic.Models.Messages;

namespace Mubsir.App.Brain;

internal sealed class ClaudeSession : IBrainSession
{
    public const int AgentMaxTokens = 4096;
    public const int QuickMaxTokens = 1024;

    private const int MaxPauseContinuations = 3;

    private readonly ClaudeBrain _brain;
    private readonly BrainSessionOptions _options;
    private readonly string _model;
    private readonly IEventLog _log;
    private readonly List<Sdk.MessageParam> _history = [];
    private readonly List<Sdk.TextBlockParam> _system;
    private readonly List<Sdk.ToolUnion> _tools;
    private readonly SemaphoreSlim _busy = new(1, 1);
    private bool _disposed;

    public ClaudeSession(ClaudeBrain brain, BrainSessionOptions options, string model, IEventLog log)
    {
        _brain = brain;
        _options = options;
        _model = model;
        _log = log;
        _system = BuildSystem(options);
        _tools = BuildTools(options);
    }

    private bool Quick => _options.Mode == BrainMode.QuickAnswer;

    public async IAsyncEnumerable<BrainEvent> SendAsync(BrainInput input, [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var client = _brain.Client();
        if (client is null)
        {
            yield return new BrainFailed(BrainErrorKind.NoApiKey, "no_api_key");
            yield break;
        }

        await _busy.WaitAsync(ct).ConfigureAwait(false);
        int mark = _history.Count;
        try
        {
            var appended = AppendInput(input);
            if (appended is not null)
            {
                yield return appended;
                yield break;
            }

            var clock = Stopwatch.StartNew();
            for (int round = 0; ; round++)
            {
                var acc = new ClaudeTurnAccumulator();
                BrainFailed? failure = null;
                long firstTextMs = -1;
                IAsyncEnumerator<Sdk.RawMessageStreamEvent>? stream = null;
                try
                {
                    stream = client.Messages.CreateStreaming(BuildParams(), ct).GetAsyncEnumerator(ct);
                }
                catch (Exception e) when (!IsUserCancel(e, ct) && e is not OutOfMemoryException)
                {
                    failure = ClaudeMapping.MapException(e);
                }

                if (stream is not null)
                {
                    try
                    {
                        while (true)
                        {
                            bool has;
                            try
                            {
                                has = await stream.MoveNextAsync().ConfigureAwait(false);
                            }
                            catch (Exception e) when (!IsUserCancel(e, ct) && e is not OutOfMemoryException)
                            {
                                failure = ClaudeMapping.MapException(e);
                                break;
                            }

                            if (!has)
                            {
                                break;
                            }

                            var text = acc.Apply(stream.Current);
                            if (text is not null)
                            {
                                if (firstTextMs < 0)
                                {
                                    firstTextMs = clock.ElapsedMilliseconds;
                                }

                                yield return new TextDelta(text);
                            }
                        }
                    }
                    finally
                    {
                        try
                        {
                            await stream.DisposeAsync().ConfigureAwait(false);
                        }
                        catch (Exception e) when (e is not OutOfMemoryException)
                        {
                        }
                    }
                }

                if (failure is null && !acc.Completed)
                {
                    failure = new BrainFailed(BrainErrorKind.Network, "stream_cut");
                }

                if (failure is not null)
                {
                    Rollback(mark);
                    _log.Fail(LogKind.BrainRequest, failure.Code, latencyMs: clock.ElapsedMilliseconds);
                    yield return failure;
                    yield break;
                }

                var usage = new UsageRecord(
                    DateTimeOffset.UtcNow,
                    _model,
                    acc.InputTokens,
                    acc.CacheReadTokens,
                    acc.CacheWriteTokens,
                    acc.OutputTokens,
                    WebSearches: acc.WebSearches);
                Log(acc, clock.ElapsedMilliseconds, firstTextMs);

                var stop = acc.StopReason ?? "end_turn";
                if (stop == "refusal")
                {
                    Rollback(mark);
                    yield return new UsageReported(usage);
                    yield return new BrainFailed(BrainErrorKind.Refusal, ClaudeMapping.RefusalCode(acc.RefusalCategory));
                    yield break;
                }

                if (!acc.TryBuild(out var content, out var calls))
                {
                    Rollback(mark);
                    yield return new UsageReported(usage);
                    yield return new BrainFailed(BrainErrorKind.BadRequest, "tool_json");
                    yield break;
                }

                if (content.Count > 0)
                {
                    _history.Add(new Sdk.MessageParam { Role = Sdk.Role.Assistant, Content = content });
                }

                yield return new UsageReported(usage);

                if (stop == "pause_turn" && round < MaxPauseContinuations && content.Count > 0)
                {
                    continue;
                }

                foreach (var call in calls)
                {
                    yield return new ToolCallRequested(call);
                }

                yield return new TurnCompleted(stop);
                yield break;
            }
        }
        finally
        {
            _busy.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _history.Clear();
        return ValueTask.CompletedTask;
    }

    private static bool IsUserCancel(Exception e, CancellationToken ct) => e is OperationCanceledException && ct.IsCancellationRequested;

    private static List<Sdk.TextBlockParam> BuildSystem(BrainSessionOptions options)
    {
        var blocks = new List<Sdk.TextBlockParam>();
        bool hasProfile = !string.IsNullOrWhiteSpace(options.ProfileBlock);
        var prompt = new Sdk.TextBlockParam { Text = string.IsNullOrWhiteSpace(options.SystemPrompt) ? "You are Mubsir." : options.SystemPrompt };
        if (!hasProfile)
        {
            prompt = prompt with { CacheControl = new Sdk.CacheControlEphemeral() };
        }

        blocks.Add(prompt);
        if (hasProfile)
        {
            blocks.Add(new Sdk.TextBlockParam { Text = options.ProfileBlock, CacheControl = new Sdk.CacheControlEphemeral() });
        }

        return blocks;
    }

    private static List<Sdk.ToolUnion> BuildTools(BrainSessionOptions options)
    {
        var tools = new List<Sdk.ToolUnion>();
        if (options.Mode == BrainMode.Agent)
        {
            foreach (var schema in options.Tools)
            {
                tools.Add(ClaudeMapping.ToSdkTool(schema));
            }
        }

        if (options.AllowWebSearch)
        {
            tools.Add(new Sdk.ToolUnion(new Sdk.WebSearchTool20250305 { MaxUses = 3 }));
        }

        return tools;
    }

    private BrainFailed? AppendInput(BrainInput input)
    {
        switch (input)
        {
            case UserTurn turn:
                {
                    var content = new List<Sdk.ContentBlockParam>();
                    if (!string.IsNullOrWhiteSpace(turn.ScreenContextText))
                    {
                        content.Add(new Sdk.TextBlockParam { Text = ClaudeMapping.WrapScreen(turn.ScreenContextText) });
                    }

                    if (turn.JpegImage is { Length: > 0 } jpeg)
                    {
                        content.Add(new Sdk.ImageBlockParam
                        {
                            Source = new Sdk.Base64ImageSource
                            {
                                Data = Convert.ToBase64String(jpeg),
                                MediaType = Sdk.MediaType.ImageJpeg,
                            },
                        });
                    }

                    content.Add(new Sdk.TextBlockParam { Text = string.IsNullOrWhiteSpace(turn.Text) ? "(no words)" : turn.Text });
                    _history.Add(new Sdk.MessageParam { Role = Sdk.Role.User, Content = content });
                    return null;
                }

            case ToolResults results when results.Results.Count > 0:
                {
                    var content = new List<Sdk.ContentBlockParam>(results.Results.Count);
                    foreach (var r in results.Results)
                    {
                        var block = new Sdk.ToolResultBlockParam
                        {
                            ToolUseID = r.ToolCallId,
                            Content = string.IsNullOrEmpty(r.Content) ? "(empty)" : r.Content,
                        };

                        content.Add(r.IsError ? block with { IsError = true } : block);
                    }

                    _history.Add(new Sdk.MessageParam { Role = Sdk.Role.User, Content = content });
                    return null;
                }

            default:
                return new BrainFailed(BrainErrorKind.BadRequest, "empty_input");
        }
    }

    private Sdk.MessageCreateParams BuildParams()
    {
        var p = new Sdk.MessageCreateParams
        {
            Model = _model,
            MaxTokens = Quick ? QuickMaxTokens : AgentMaxTokens,
            System = _system,
            Messages = [.. _history],
            OutputConfig = new Sdk.OutputConfig { Effort = Sdk.Effort.Low },
        };

        if (Quick)
        {
            p = p with { Thinking = new Sdk.ThinkingConfigDisabled() };
        }
        else
        {
            p = p with
            {
                Thinking = new Sdk.ThinkingConfigAdaptive(),

                CacheControl = new Sdk.CacheControlEphemeral(),
            };
        }

        if (_tools.Count > 0)
        {
            p = p with { Tools = _tools };
        }

        return p;
    }

    private void Rollback(int mark)
    {
        if (mark >= 0 && mark < _history.Count)
        {
            _history.RemoveRange(mark, _history.Count - mark);
        }
    }

    private void Log(ClaudeTurnAccumulator acc, long totalMs, long firstTextMs)
    {
        try
        {
            _log.Write(new LogEvent(
                DateTimeOffset.UtcNow,
                LogKind.BrainRequest,
                LogOutcome.Ok,
                Tool: Quick ? "quick" : "agent",
                LatencyMs: totalMs,
                InputTokens: acc.InputTokens + acc.CacheReadTokens + acc.CacheWriteTokens,
                OutputTokens: acc.OutputTokens));
            if (firstTextMs >= 0)
            {
                _log.Metric("brain.first_text_ms", firstTextMs);
            }
        }
        catch (Exception)
        {
        }
    }
}
