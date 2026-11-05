using Mubsir.Core.Router;

namespace Mubsir.Core.Tests.Router;

public class DialectAndSettingsRouterTests
{
    [Theory]
    // Maghrebi
    [InlineData("قرا الشاشة", LocalCommand.ReadScreen)]
    [InlineData("قرا ليكرون", LocalCommand.ReadScreen)]
    [InlineData("قرا ليا ليكرون", LocalCommand.ReadScreen)]
    [InlineData("قرا الشاشة كاملة", LocalCommand.ReadScreen)]
    [InlineData("قرا من هنايا", LocalCommand.ReadFromHere)]
    [InlineData("كمل من هنايا", LocalCommand.ReadFromHere)]
    [InlineData("فين راني", LocalCommand.WhereAmI)]
    [InlineData("وين راني دابا", LocalCommand.WhereAmI)]
    [InlineData("حدر لتحت", LocalCommand.ScrollDown)]
    [InlineData("اهبط لوطا شوية", LocalCommand.ScrollDown)]
    [InlineData("اطلع للفوق", LocalCommand.ScrollUp)]
    [InlineData("صافي باراكا", LocalCommand.Stop)]
    [InlineData("حبس عافاك", LocalCommand.Stop)]
    [InlineData("يزي عيشك", LocalCommand.Stop)]
    [InlineData("عاود ليا", LocalCommand.Repeat)]
    [InlineData("شنو قلتي", LocalCommand.Repeat)]
    [InlineData("واش قلتي", LocalCommand.Repeat)]
    [InlineData("زرب شوية", LocalCommand.SpeakFaster)]
    [InlineData("غير بشوية", LocalCommand.SpeakSlower)]
    [InlineData("سد التاب", LocalCommand.CloseTab)]
    [InlineData("شحال الساعة دابا", LocalCommand.WhatTime)]
    [InlineData("شنو التاريخ اليوم", LocalCommand.WhatDate)]
    [InlineData("زيد فالصوت", LocalCommand.VolumeUp)]
    [InlineData("نقص فالصوت", LocalCommand.VolumeDown)]
    [InlineData("قطع الصوت", LocalCommand.Mute)]
    // Gulf
    [InlineData("اقرا الشاشة كاملة", LocalCommand.ReadScreen)]
    [InlineData("اقرا كل اللي بالشاشة", LocalCommand.ReadScreen)]
    [InlineData("اقرا من هني", LocalCommand.ReadFromHere)]
    [InlineData("كمل من هني", LocalCommand.ReadFromHere)]
    [InlineData("وين أنا الحين", LocalCommand.WhereAmI)]
    [InlineData("انزل تحت شوي", LocalCommand.ScrollDown)]
    [InlineData("حدر تحت", LocalCommand.ScrollDown)]
    [InlineData("ارقى فوق", LocalCommand.ScrollUp)]
    [InlineData("ارقى شوي", LocalCommand.ScrollUp)]
    [InlineData("وقّف عاد", LocalCommand.Stop)]
    [InlineData("تكفى وقف", LocalCommand.Stop)]
    [InlineData("عيد وش قلت", LocalCommand.Repeat)]
    [InlineData("عجل شوي", LocalCommand.SpeakFaster)]
    [InlineData("سرع شوي", LocalCommand.SpeakFaster)]
    [InlineData("ركد شوي", LocalCommand.SpeakSlower)]
    [InlineData("بشويش شوي", LocalCommand.SpeakSlower)]
    [InlineData("صك التاب", LocalCommand.CloseTab)]
    [InlineData("بند التاب", LocalCommand.CloseTab)]
    [InlineData("تاب يديد", LocalCommand.NewTab)]
    [InlineData("كم الساعة الحين", LocalCommand.WhatTime)]
    [InlineData("وش الساعة الحين", LocalCommand.WhatTime)]
    [InlineData("كم التاريخ اليوم", LocalCommand.WhatDate)]
    [InlineData("وشو تاريخ اليوم", LocalCommand.WhatDate)]
    [InlineData("عل الصوت", LocalCommand.VolumeUp)]
    [InlineData("قصر الصوت", LocalCommand.VolumeDown)]
    [InlineData("صمت الصوت", LocalCommand.Mute)]
    // Levantine
    [InlineData("اقرا كل الشاشة", LocalCommand.ReadScreen)]
    [InlineData("اقرا الصفحة كاملة", LocalCommand.ReadScreen)]
    [InlineData("اقرا من هون", LocalCommand.ReadFromHere)]
    [InlineData("اقرالي من هون", LocalCommand.ReadFromHere)]
    [InlineData("وين أنا هلق", LocalCommand.WhereAmI)]
    [InlineData("وين أنا هلأ", LocalCommand.WhereAmI)]
    [InlineData("وين صرت", LocalCommand.WhereAmI)]
    [InlineData("نزال لتحت", LocalCommand.ScrollDown)]
    [InlineData("نزال شوي", LocalCommand.ScrollDown)]
    [InlineData("اطلاع لفوق", LocalCommand.ScrollUp)]
    [InlineData("اطلاع شوي", LocalCommand.ScrollUp)]
    [InlineData("حاجي", LocalCommand.Stop)]
    [InlineData("حاجتك", LocalCommand.Stop)]
    [InlineData("روق شوي", LocalCommand.Stop)]
    [InlineData("عيد شو حكيت", LocalCommand.Repeat)]
    [InlineData("شو حكيت", LocalCommand.Repeat)]
    [InlineData("احكي أسرع", LocalCommand.SpeakFaster)]
    [InlineData("احكي بسرعة", LocalCommand.SpeakFaster)]
    [InlineData("احكي أبطأ", LocalCommand.SpeakSlower)]
    [InlineData("على مهلك شوي", LocalCommand.SpeakSlower)]
    [InlineData("سكر التاب", LocalCommand.CloseTab)]
    [InlineData("قديش الساعة هلق", LocalCommand.WhatTime)]
    [InlineData("شو تاريخ اليوم", LocalCommand.WhatDate)]
    [InlineData("علّي الصوت شوي", LocalCommand.VolumeUp)]
    [InlineData("وطّي الصوت شوي", LocalCommand.VolumeDown)]
    public void Dialect_commands_match_expected_local_command(string utterance, LocalCommand expected)
    {
        var match = CommandGrammar.Match(utterance);
        Assert.NotNull(match);
        Assert.Equal(expected, match.Command);
    }

    [Theory]
    // Settings commands in Arabic, English and mixed
    [InlineData("شغل صوت فيش", LocalCommand.FishVoiceOn)]
    [InlineData("fish voice on", LocalCommand.FishVoiceOn)]
    [InlineData("turn on fish voice", LocalCommand.FishVoiceOn)]
    [InlineData("صوت fish on", LocalCommand.FishVoiceOn)]
    [InlineData("اطفي صوت فيش", LocalCommand.FishVoiceOff)]
    [InlineData("fish voice off", LocalCommand.FishVoiceOff)]
    [InlineData("turn off fish voice", LocalCommand.FishVoiceOff)]
    [InlineData("اقرأ الكتب بصوت فيش", LocalCommand.ReadBooksWithFishOn)]
    [InlineData("read books with fish", LocalCommand.ReadBooksWithFishOn)]
    [InlineData("أوقف صوت فيش للكتب", LocalCommand.ReadBooksWithFishOff)]
    [InlineData("turn off read books with fish", LocalCommand.ReadBooksWithFishOff)]
    [InlineData("شغل الشريط", LocalCommand.NotchOn)]
    [InlineData("أظهر الشريط", LocalCommand.NotchOn)]
    [InlineData("notch on", LocalCommand.NotchOn)]
    [InlineData("turn on notch", LocalCommand.NotchOn)]
    [InlineData("اطفي الشريط", LocalCommand.NotchOff)]
    [InlineData("أخفِ الشريط", LocalCommand.NotchOff)]
    [InlineData("notch off", LocalCommand.NotchOff)]
    [InlineData("turn off notch", LocalCommand.NotchOff)]
    [InlineData("احفظ المحادثات", LocalCommand.SaveChatsOn)]
    [InlineData("save chats on", LocalCommand.SaveChatsOn)]
    [InlineData("turn on save chats", LocalCommand.SaveChatsOn)]
    [InlineData("لا تحفظ المحادثات", LocalCommand.SaveChatsOff)]
    [InlineData("save chats off", LocalCommand.SaveChatsOff)]
    [InlineData("turn off save chats", LocalCommand.SaveChatsOff)]
    [InlineData("افتح مجلد المحادثات", LocalCommand.OpenChatsFolder)]
    [InlineData("open chats folder", LocalCommand.OpenChatsFolder)]
    [InlineData("open my chats folder", LocalCommand.OpenChatsFolder)]
    [InlineData("الإعدادات", LocalCommand.Settings)]
    [InlineData("افتح الإعدادات", LocalCommand.Settings)]
    [InlineData("open settings", LocalCommand.Settings)]
    [InlineData("open mubsir settings", LocalCommand.Settings)]
    [InlineData("تراجع", LocalCommand.Undo)]
    [InlineData("undo that", LocalCommand.Undo)]
    [InlineData("undo", LocalCommand.Undo)]
    public void Settings_and_undo_commands_match_expected_local_command(string utterance, LocalCommand expected)
    {
        var match = CommandGrammar.Match(utterance);
        Assert.NotNull(match);
        Assert.Equal(expected, match.Command);
    }

    [Theory]
    // Normal sentences that must NOT match any local command
    [InlineData("أنا أبحث عن كتاب في التاريخ")]
    [InlineData("ما رأيك في هذا المقال العلمي")]
    [InlineData("كيف أتعلم البرمجة بلغة سي شارب")]
    [InlineData("اشرح لي هذه الفقرة بالتفصيل")]
    [InlineData("ترجم هذا النص إلى اللغة الإنجليزية")]
    [InlineData("أريد تلخيص هذه الصفحة")]
    [InlineData("ساعدني في كتابة رسالة لصديقي")]
    [InlineData("حل المسألة الرياضية التالية")]
    [InlineData("اكتب لي قصة قصيرة عن البحر")]
    [InlineData("how do i bake a chocolate cake")]
    [InlineData("what is the capital of Egypt")]
    [InlineData("summarize the following article for me")]
    [InlineData("tell me about hoopoe birds")]
    public void Normal_sentences_are_not_matched_as_commands(string utterance)
    {
        var match = CommandGrammar.Match(utterance);
        Assert.Null(match);
    }
}
