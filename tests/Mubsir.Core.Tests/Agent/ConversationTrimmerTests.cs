using Mubsir.Core.Agent;

namespace Mubsir.Core.Tests.Agent;

public class ConversationTrimmerTests
{
    [Fact]
    public void PruneOlderScreenshots_NoImages_ReturnsZero()
    {
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.User, new ConversationBlock.Text("Hello")),
            new(ConversationRole.Assistant, new ConversationBlock.Text("Hi there")),
        };

        int replaced = ConversationTrimmer.PruneOlderScreenshots(messages);
        Assert.Equal(0, replaced);
        Assert.IsType<ConversationBlock.Text>(messages[0].Blocks[0]);
    }

    [Fact]
    public void PruneOlderScreenshots_SingleImage_KeepsImage()
    {
        byte[] bytes = [1, 2, 3];
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.User, new ConversationBlock.Text("Look at this"), new ConversationBlock.Image(bytes)),
            new(ConversationRole.Assistant, new ConversationBlock.Text("I see it")),
        };

        int replaced = ConversationTrimmer.PruneOlderScreenshots(messages);
        Assert.Equal(0, replaced);
        Assert.IsType<ConversationBlock.Image>(messages[0].Blocks[1]);
    }

    [Fact]
    public void PruneOlderScreenshots_MultipleImages_KeepsOnlyNewestScreenshot()
    {
        byte[] img1 = [1, 1, 1];
        byte[] img2 = [2, 2, 2];
        byte[] img3 = [3, 3, 3];

        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.User, new ConversationBlock.Text("Step 1"), new ConversationBlock.Image(img1)),
            new(ConversationRole.Assistant, new ConversationBlock.ToolUse("call_1", "describe_screen")),
            new(ConversationRole.User, new ConversationBlock.ToolResult("call_1", "ok"), new ConversationBlock.Image(img2)),
            new(ConversationRole.Assistant, new ConversationBlock.ToolUse("call_2", "describe_screen")),
            new(ConversationRole.User, new ConversationBlock.ToolResult("call_2", "ok"), new ConversationBlock.Image(img3)),
        };

        int replaced = ConversationTrimmer.PruneOlderScreenshots(messages);
        Assert.Equal(2, replaced);

        // img1 replaced with note
        var block1 = Assert.IsType<ConversationBlock.Text>(messages[0].Blocks[1]);
        Assert.Equal(ConversationTrimmer.OmittedScreenshotNote, block1.Content);

        // img2 replaced with note
        var block2 = Assert.IsType<ConversationBlock.Text>(messages[2].Blocks[1]);
        Assert.Equal(ConversationTrimmer.OmittedScreenshotNote, block2.Content);

        // img3 (the newest) is kept as an image
        var block3 = Assert.IsType<ConversationBlock.Image>(messages[4].Blocks[1]);
        Assert.Same(img3, block3.Bytes);
    }

    [Fact]
    public void TrimConversation_UnderLimit_DoesNotTrim_PreservingPromptCache()
    {
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.User, new ConversationBlock.Text("Prompt")),
            new(ConversationRole.Assistant, new ConversationBlock.ToolUse("c1", "tool")),
            new(ConversationRole.User, new ConversationBlock.ToolResult("c1", "res")),
        };

        int removed = ConversationTrimmer.TrimConversation(messages, maxMessages: 10);
        Assert.Equal(0, removed);
        Assert.Equal(3, messages.Count);
    }

    [Fact]
    public void TrimConversation_PreservesRootUserPrompt_AndAlternatesRoles()
    {
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.User, new ConversationBlock.Text("User goal")),
            new(ConversationRole.Assistant, new ConversationBlock.ToolUse("c1", "step1")),
            new(ConversationRole.User, new ConversationBlock.ToolResult("c1", "res1")),
            new(ConversationRole.Assistant, new ConversationBlock.ToolUse("c2", "step2")),
            new(ConversationRole.User, new ConversationBlock.ToolResult("c2", "res2")),
            new(ConversationRole.Assistant, new ConversationBlock.ToolUse("c3", "step3")),
            new(ConversationRole.User, new ConversationBlock.ToolResult("c3", "res3")),
        };

        // 7 messages total. Trim to 5.
        int removed = ConversationTrimmer.TrimConversation(messages, maxMessages: 5);
        Assert.Equal(2, removed);
        Assert.Equal(5, messages.Count);

        // Index 0 must still be the root User goal!
        Assert.Equal(ConversationRole.User, messages[0].Role);
        var rootText = Assert.IsType<ConversationBlock.Text>(messages[0].Blocks[0]);
        Assert.Equal("User goal", rootText.Content);

        // Pair [c1, res1] should be trimmed. Next pair is [c2, res2].
        Assert.Equal(ConversationRole.Assistant, messages[1].Role);
        var tu2 = Assert.IsType<ConversationBlock.ToolUse>(messages[1].Blocks[0]);
        Assert.Equal("c2", tu2.Id);

        Assert.Equal(ConversationRole.User, messages[2].Role);
        var tr2 = Assert.IsType<ConversationBlock.ToolResult>(messages[2].Blocks[0]);
        Assert.Equal("c2", tr2.ToolCallId);

        // Followed by [c3, res3]
        Assert.Equal(ConversationRole.Assistant, messages[3].Role);
        var tu3 = Assert.IsType<ConversationBlock.ToolUse>(messages[3].Blocks[0]);
        Assert.Equal("c3", tu3.Id);

        Assert.Equal(ConversationRole.User, messages[4].Role);
        var tr3 = Assert.IsType<ConversationBlock.ToolResult>(messages[4].Blocks[0]);
        Assert.Equal("c3", tr3.ToolCallId);

        // Verify roles strictly alternate
        for (int i = 0; i < messages.Count; i++)
        {
            Assert.Equal(i % 2 == 0 ? ConversationRole.User : ConversationRole.Assistant, messages[i].Role);
        }
    }

    [Fact]
    public void TrimConversation_PureChat_DropsOldestTurnsFromFront()
    {
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.User, new ConversationBlock.Text("Question 1")),
            new(ConversationRole.Assistant, new ConversationBlock.Text("Answer 1")),
            new(ConversationRole.User, new ConversationBlock.Text("Question 2")),
            new(ConversationRole.Assistant, new ConversationBlock.Text("Answer 2")),
            new(ConversationRole.User, new ConversationBlock.Text("Question 3")),
            new(ConversationRole.Assistant, new ConversationBlock.Text("Answer 3")),
        };

        int removed = ConversationTrimmer.TrimConversation(messages, maxMessages: 4);
        Assert.Equal(2, removed);
        Assert.Equal(4, messages.Count);

        // New first message is Question 2 (User)
        Assert.Equal(ConversationRole.User, messages[0].Role);
        var q2 = Assert.IsType<ConversationBlock.Text>(messages[0].Blocks[0]);
        Assert.Equal("Question 2", q2.Content);
    }

    [Fact]
    public void TrimConversation_NeverBreaksToolPairing_OrLeavesToolResultAtStart()
    {
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.User, new ConversationBlock.Text("Root prompt")),
            new(ConversationRole.Assistant, new ConversationBlock.ToolUse("c1", "step1")),
            new(ConversationRole.User, new ConversationBlock.ToolResult("c1", "res1")),
            new(ConversationRole.Assistant, new ConversationBlock.ToolUse("c2", "step2")),
            new(ConversationRole.User, new ConversationBlock.ToolResult("c2", "res2")),
        };

        // Trim to 3. Should remove [c1, res1], leaving Root, c2, res2.
        int removed = ConversationTrimmer.TrimConversation(messages, maxMessages: 3);
        Assert.Equal(2, removed);
        Assert.Equal(3, messages.Count);

        Assert.Equal(ConversationRole.User, messages[0].Role);
        Assert.IsNotType<ConversationBlock.ToolResult>(messages[0].Blocks[0]);

        Assert.Equal(ConversationRole.Assistant, messages[1].Role);
        var tu = Assert.IsType<ConversationBlock.ToolUse>(messages[1].Blocks[0]);
        Assert.Equal("c2", tu.Id);

        Assert.Equal(ConversationRole.User, messages[2].Role);
        var tr = Assert.IsType<ConversationBlock.ToolResult>(messages[2].Blocks[0]);
        Assert.Equal("c2", tr.ToolCallId);
    }
}
