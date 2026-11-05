using Mubsir.Core.Chat;
using Xunit;

namespace Mubsir.Core.Tests.Chat;

public class ChatRoutingTests
{
    [Theory]
    [InlineData("what does cat mean", true)]
    [InlineData("يعني ايه قطة", true)]
    [InlineData("مين هو ابن سينا", true)]
    [InlineData("افتح يوتيوب", false)]
    [InlineData("اقرا الشاشة", false)]
    [InlineData("what is on my screen", false)]
    [InlineData("ايه اللي في الملف ده", false)]
    [InlineData("what time is it", false)]
    [InlineData("كام الساعة", false)]
    [InlineData("", false)]
    public void Only_plain_questions_go_to_the_chat(string said, bool plain) => Assert.Equal(plain, ChatRouting.IsPlainQuestion(said));

    [Fact]
    public void With_no_chat_chosen_a_plain_question_stays_with_the_brain() =>
        Assert.Null(ChatRouting.Pick("what does cat mean", ChatSite.None));

    [Fact]
    public void A_chosen_chat_gets_plain_questions()
    {
        ChatRequest? r = ChatRouting.Pick("what does cat mean", ChatSite.Gemini);
        Assert.Equal(new ChatRequest(ChatSite.Gemini, "what does cat mean", false), r);
    }

    [Theory]
    [InlineData("اسأل شات جي بي تي يعني ايه قطة", ChatSite.ChatGpt, "يعني ايه قطة")]
    [InlineData("ask gemini, what is rain", ChatSite.Gemini, "what is rain")]
    public void Naming_a_chat_always_uses_it(string said, ChatSite site, string question)
    {
        ChatRequest? r = ChatRouting.Pick(said, ChatSite.None);
        Assert.Equal(new ChatRequest(site, question, true), r);
    }

    [Fact]
    public void The_answer_is_what_comes_after_the_last_time_it_was_asked()
    {
        const string Page = "New chat\nwhat is rain\nOld answer\nwhat is rain\nChatGPT said:\nRain is water falling from clouds.\nCopy\nGood response\nAsk anything\nChatGPT can make mistakes.";
        Assert.Equal("Rain is water falling from clouds.", ChatAnswer.Extract(Page, "what is rain"));
    }

    [Theory]
    [InlineData("no question here")]
    [InlineData("what is rain\nCopy\nAsk anything")]
    [InlineData(null)]
    public void No_answer_yet_means_nothing_is_read(string? page) => Assert.Null(ChatAnswer.Extract(page, "what is rain"));

    [Fact]
    public void A_sign_in_page_is_recognised()
    {
        Assert.True(ChatAnswer.WantsSignIn("Log in to get smarter responses"));
        Assert.False(ChatAnswer.WantsSignIn("Rain is water."));
    }
}
