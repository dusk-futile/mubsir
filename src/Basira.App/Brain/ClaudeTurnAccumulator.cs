using System.Text;
using System.Text.Json;
using Basira.Core.Agent;
using Sdk = Anthropic.Models.Messages;

namespace Basira.App.Brain;

/// <summary>
/// Rebuilds one streamed assistant message: text, thinking (with signature), redacted thinking, tool use with its
/// JSON input, and web search server-tool blocks, so the exact content can be appended to the session history.
/// </summary>
internal sealed class ClaudeTurnAccumulator
{
    private readonly SortedDictionary<long, Block> _blocks = [];

    public long InputTokens { get; private set; }

    public long CacheReadTokens { get; private set; }

    public long CacheWriteTokens { get; private set; }

    public long OutputTokens { get; private set; }

    public int WebSearches { get; private set; }

    public string? StopReason { get; private set; }

    public string? RefusalCategory { get; private set; }

    /// <summary>The message_stop event arrived (the stream was not cut).</summary>
    public bool Completed { get; private set; }

    public bool SawFirstText { get; private set; }

    /// <summary>Applies one stream event; returns answer text to speak when the event is a text delta.</summary>
    public string? Apply(Sdk.RawMessageStreamEvent ev)
    {
        if (ev.TryPickContentBlockDelta(out var delta))
        {
            return ApplyDelta(delta);
        }

        if (ev.TryPickContentBlockStart(out var start))
        {
            StartBlock(start);
            return null;
        }

        if (ev.TryPickStart(out var messageStart))
        {
            var u = messageStart.Message.Usage;
            InputTokens = u.InputTokens;
            CacheReadTokens = u.CacheReadInputTokens ?? 0;
            CacheWriteTokens = u.CacheCreationInputTokens ?? 0;
            OutputTokens = u.OutputTokens;
            return null;
        }

        if (ev.TryPickDelta(out var messageDelta))
        {
            if (messageDelta.Delta.StopReason is { } reason)
            {
                StopReason = reason.Raw();
            }

            if (messageDelta.Delta.StopDetails is { } details)
            {
                RefusalCategory = details.Category?.Raw();
            }

            var u = messageDelta.Usage;
            OutputTokens = u.OutputTokens;
            if (u.InputTokens is { } input)
            {
                InputTokens = input;
            }

            if (u.CacheReadInputTokens is { } read)
            {
                CacheReadTokens = read;
            }

            if (u.CacheCreationInputTokens is { } write)
            {
                CacheWriteTokens = write;
            }

            if (u.ServerToolUse?.WebSearchRequests is { } searches)
            {
                WebSearches = (int)searches;
            }

            return null;
        }

        if (ev.TryPickStop(out _))
        {
            Completed = true;
        }

        return null;
    }

    /// <summary>
    /// The assistant content for the history and the tool calls to run. False when a tool's input is not valid JSON
    /// (for example cut off by max_tokens).
    /// </summary>
    public bool TryBuild(out List<Sdk.ContentBlockParam> content, out List<ToolCall> calls)
    {
        content = [];
        calls = [];
        foreach (var block in _blocks.Values)
        {
            switch (block.Kind)
            {
                case BlockKind.Text:
                    if (block.Text.Length > 0)
                    {
                        content.Add(new Sdk.TextBlockParam { Text = block.Text.ToString() });
                    }

                    break;

                case BlockKind.Thinking:
                    // The signature must come back unchanged, or the API rejects the history.
                    content.Add(new Sdk.ThinkingBlockParam { Thinking = block.Text.ToString(), Signature = block.Signature ?? "" });
                    break;

                case BlockKind.RedactedThinking:
                    content.Add(new Sdk.RedactedThinkingBlockParam { Data = block.Data ?? "" });
                    break;

                case BlockKind.ToolUse:
                {
                    if (!ClaudeMapping.TryParseInput(block.Json.ToString(), out var element, out var input))
                    {
                        return false;
                    }

                    content.Add(new Sdk.ToolUseBlockParam { ID = block.Id!, Name = block.Name!, Input = input });
                    calls.Add(new ToolCall(block.Id!, block.Name!, element));
                    break;
                }

                case BlockKind.ServerToolUse:
                {
                    if (!ClaudeMapping.TryParseInput(block.Json.ToString(), out _, out var input))
                    {
                        return false;
                    }

                    content.Add(new Sdk.ServerToolUseBlockParam { ID = block.Id!, Name = block.ServerToolName!, Input = input });
                    break;
                }

                case BlockKind.Passthrough when block.Param is not null:
                    content.Add(block.Param);
                    break;
            }
        }

        return true;
    }

    private string? ApplyDelta(Sdk.RawContentBlockDeltaEvent e)
    {
        if (!_blocks.TryGetValue(e.Index, out var block))
        {
            block = new Block(BlockKind.Text);
            _blocks[e.Index] = block;
        }

        var d = e.Delta;
        if (d.TryPickText(out var text))
        {
            block.Text.Append(text.Text);
            if (block.Kind == BlockKind.Text && text.Text.Length > 0)
            {
                SawFirstText = true;
                return text.Text;
            }

            return null;
        }

        if (d.TryPickThinking(out var thinking))
        {
            block.Text.Append(thinking.Thinking);
        }
        else if (d.TryPickSignature(out var signature))
        {
            block.Signature = (block.Signature ?? "") + signature.Signature;
        }
        else if (d.TryPickInputJson(out var json))
        {
            block.Json.Append(json.PartialJson);
        }

        // Citation deltas are not kept: text is sent back without citations, which the API accepts.
        return null;
    }

    private void StartBlock(Sdk.RawContentBlockStartEvent e)
    {
        var b = e.ContentBlock;
        Block block;
        if (b.TryPickText(out var text))
        {
            block = new Block(BlockKind.Text);
            block.Text.Append(text.Text);
        }
        else if (b.TryPickThinking(out var thinking))
        {
            block = new Block(BlockKind.Thinking) { Signature = string.IsNullOrEmpty(thinking.Signature) ? null : thinking.Signature };
            block.Text.Append(thinking.Thinking);
        }
        else if (b.TryPickRedactedThinking(out var redacted))
        {
            block = new Block(BlockKind.RedactedThinking) { Data = redacted.Data };
        }
        else if (b.TryPickToolUse(out var toolUse))
        {
            block = new Block(BlockKind.ToolUse) { Id = toolUse.ID, Name = toolUse.Name };
            AppendInitialInput(block, toolUse.Input);
        }
        else if (b.TryPickServerToolUse(out var serverToolUse))
        {
            block = new Block(BlockKind.ServerToolUse) { Id = serverToolUse.ID, ServerToolName = serverToolUse.Name.Raw() };
            AppendInitialInput(block, serverToolUse.Input);
        }
        else if (b.TryPickWebSearchToolResult(out var searchResult))
        {
            block = new Block(BlockKind.Passthrough) { Param = ClaudeServerBlocks.ToParam(searchResult) };
        }
        else
        {
            // A block type this version does not know: it cannot be echoed back, so leave it out of the history.
            block = new Block(BlockKind.Unknown);
        }

        _blocks[e.Index] = block;
    }

    private static void AppendInitialInput(Block block, IReadOnlyDictionary<string, JsonElement>? input)
    {
        // Streaming starts tool blocks with "input": {} and sends the real JSON as deltas.
        if (input is { Count: > 0 })
        {
            block.Json.Append(JsonSerializer.Serialize(input));
        }
    }

    private enum BlockKind
    {
        Text,
        Thinking,
        RedactedThinking,
        ToolUse,
        ServerToolUse,
        Passthrough,
        Unknown,
    }

    private sealed class Block(BlockKind kind)
    {
        public BlockKind Kind { get; } = kind;

        public StringBuilder Text { get; } = new();

        public StringBuilder Json { get; } = new();

        public string? Signature { get; set; }

        public string? Data { get; set; }

        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? ServerToolName { get; set; }

        public Sdk.ContentBlockParam? Param { get; set; }
    }
}
