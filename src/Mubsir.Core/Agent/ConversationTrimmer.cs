namespace Mubsir.Core.Agent;

public enum ConversationRole
{
    User,
    Assistant,
}

public abstract record ConversationBlock
{
    public sealed record Text(string Content, object? Raw = null) : ConversationBlock;
    public sealed record Image(byte[] Bytes, string MediaType = "image/jpeg", object? Raw = null) : ConversationBlock;
    public sealed record ToolUse(string Id, string Name, object? Raw = null) : ConversationBlock;
    public sealed record ToolResult(string ToolCallId, string Content, bool IsError = false, object? Raw = null) : ConversationBlock;
    public sealed record Other(object? Raw = null) : ConversationBlock;
}

public sealed class ConversationMessage
{
    public ConversationRole Role { get; set; }
    public List<ConversationBlock> Blocks { get; set; } = [];

    public ConversationMessage(ConversationRole role, IEnumerable<ConversationBlock>? blocks = null)
    {
        Role = role;
        if (blocks is not null)
        {
            Blocks.AddRange(blocks);
        }
    }

    public ConversationMessage(ConversationRole role, params ConversationBlock[] blocks)
        : this(role, (IEnumerable<ConversationBlock>)blocks)
    {
    }
}

public static class ConversationTrimmer
{
    public const string OmittedScreenshotNote = "[previous screenshot omitted]";
    public const int DefaultMaxMessages = 20;

    /// <summary>
    /// Replaces all older screenshots with a text note, keeping only the newest screenshot in the conversation.
    /// Returns the number of images replaced.
    /// </summary>
    public static int PruneOlderScreenshots(IList<ConversationMessage> messages, string note = OmittedScreenshotNote)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var imagePositions = new List<(int MessageIndex, int BlockIndex)>();
        for (int m = 0; m < messages.Count; m++)
        {
            var msg = messages[m];
            for (int b = 0; b < msg.Blocks.Count; b++)
            {
                if (msg.Blocks[b] is ConversationBlock.Image)
                {
                    imagePositions.Add((m, b));
                }
            }
        }

        if (imagePositions.Count <= 1)
        {
            return 0;
        }

        int replaced = 0;
        for (int i = 0; i < imagePositions.Count - 1; i++)
        {
            var (m, b) = imagePositions[i];
            messages[m].Blocks[b] = new ConversationBlock.Text(note);
            replaced++;
        }

        return replaced;
    }

    /// <summary>
    /// Trims long conversations so message count does not exceed maxMessages, without breaking:
    /// 1. Alternating roles (User, Assistant, ...)
    /// 2. Conversation starting with a User message
    /// 3. ToolUse and ToolResult pairing
    /// 4. Prompt caching prefix (keeping root User prompt and trimming oldest intermediate tool turns first)
    /// </summary>
    public static int TrimConversation(IList<ConversationMessage> messages, int maxMessages = DefaultMaxMessages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        if (maxMessages < 2)
        {
            maxMessages = 2;
        }

        int removedTotal = 0;
        while (messages.Count > maxMessages)
        {
            // Case A: Multi-turn pure chat turn at the very beginning (index 0 and 1).
            // We can drop [messages[0], messages[1]] if messages[2] is a clean User turn (no tool results).
            if (messages.Count >= 4
                && messages[0].Role == ConversationRole.User
                && messages[1].Role == ConversationRole.Assistant
                && messages[2].Role == ConversationRole.User
                && !HasToolResult(messages[2]))
            {
                messages.RemoveAt(1);
                messages.RemoveAt(0);
                removedTotal += 2;
                continue;
            }

            // Case B: Ongoing agent task where Message 0 is the root User turn/prompt.
            // Trim the oldest intermediate tool pair [Assistant, User] so that Message 0 (the user's root goal)
            // is preserved, and the latest turns are preserved.
            int pairIndex = -1;
            for (int i = 1; i + 2 < messages.Count; i++)
            {
                if (messages[i].Role == ConversationRole.Assistant && messages[i + 1].Role == ConversationRole.User)
                {
                    pairIndex = i;
                    break;
                }
            }

            if (pairIndex >= 1)
            {
                messages.RemoveAt(pairIndex + 1);
                messages.RemoveAt(pairIndex);
                removedTotal += 2;
                continue;
            }

            // If no valid pair could be trimmed without breaking pairing or role alternation, stop.
            break;
        }

        return removedTotal;
    }

    /// <summary>
    /// Prepares conversation history by replacing older screenshots and trimming message count.
    /// </summary>
    public static void PrepareHistory(IList<ConversationMessage> messages, int maxMessages = DefaultMaxMessages)
    {
        PruneOlderScreenshots(messages);
        TrimConversation(messages, maxMessages);
    }

    private static bool HasToolResult(ConversationMessage message)
    {
        for (int i = 0; i < message.Blocks.Count; i++)
        {
            if (message.Blocks[i] is ConversationBlock.ToolResult)
            {
                return true;
            }
        }

        return false;
    }
}
