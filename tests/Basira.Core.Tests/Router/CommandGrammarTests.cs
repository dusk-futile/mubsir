using Basira.Core.Router;
using Basira.Core.Text;

namespace Basira.Core.Tests.Router;

public class CommandGrammarTests
{
    public static TheoryData<string, LocalCommand> Positives => new()
    {
        // Read the screen: MSA, Egyptian, Gulf, Levantine, Libyan, Maghrebi, English
        { "اقرأ الشاشة", LocalCommand.ReadScreen },
        { "إقرأ الشاشة", LocalCommand.ReadScreen },
        { "اقرا لي الشاشة", LocalCommand.ReadScreen },
        { "اقرالي الشاشه", LocalCommand.ReadScreen },
        { "اقرا ليا الشاشة", LocalCommand.ReadScreen },
        { "قرا ليا الشاشة", LocalCommand.ReadScreen },
        { "اقرا اللي عالشاشة", LocalCommand.ReadScreen },
        { "اقرأ الصفحة", LocalCommand.ReadScreen },
        { "Read the screen", LocalCommand.ReadScreen },
        { "read the screen please", LocalCommand.ReadScreen },
        { "read what's on the screen", LocalCommand.ReadScreen },

        // Read from here
        { "اقرأ من هنا", LocalCommand.ReadFromHere },
        { "اقرا من هون", LocalCommand.ReadFromHere },
        { "كمل القراءة", LocalCommand.ReadFromHere },
        { "continue reading", LocalCommand.ReadFromHere },
        { "start reading", LocalCommand.ReadFromHere },

        // Where am I
        { "أين أنا", LocalCommand.WhereAmI },
        { "أنا فين", LocalCommand.WhereAmI },
        { "انا وين", LocalCommand.WhereAmI },
        { "وين أنا الحين", LocalCommand.WhereAmI },
        { "انا فين دلوقتي", LocalCommand.WhereAmI },
        { "وينني", LocalCommand.WhereAmI },
        { "where am I?", LocalCommand.WhereAmI },
        { "where am i now", LocalCommand.WhereAmI },

        // Scroll down / up
        { "انزل", LocalCommand.ScrollDown },
        { "نزل", LocalCommand.ScrollDown },
        { "انزل تحت", LocalCommand.ScrollDown },
        { "نزّل لتحت", LocalCommand.ScrollDown },
        { "روح تحت", LocalCommand.ScrollDown },
        { "اهبط", LocalCommand.ScrollDown },
        { "تحت شوية", LocalCommand.ScrollDown },
        { "انزل شوي", LocalCommand.ScrollDown },
        { "اهبط لوطا", LocalCommand.ScrollDown },
        { "هبط", LocalCommand.ScrollDown },
        { "scroll down", LocalCommand.ScrollDown },
        { "go down", LocalCommand.ScrollDown },
        { "Scroll down a bit", LocalCommand.ScrollDown },
        { "اطلع فوق", LocalCommand.ScrollUp },
        { "طلّع لفوق", LocalCommand.ScrollUp },
        { "فوق شوية", LocalCommand.ScrollUp },
        { "اصعد", LocalCommand.ScrollUp },
        { "طلع شوي", LocalCommand.ScrollUp },
        { "scroll up", LocalCommand.ScrollUp },

        // Pages and ends
        { "انزل صفحة", LocalCommand.PageDown },
        { "page down", LocalCommand.PageDown },
        { "اطلع صفحة", LocalCommand.PageUp },
        { "page up", LocalCommand.PageUp },
        { "روح لأول الصفحة", LocalCommand.GoToTop },
        { "اذهب إلى أعلى الصفحة", LocalCommand.GoToTop },
        { "go to the top", LocalCommand.GoToTop },
        { "انزل لآخر الصفحة", LocalCommand.GoToBottom },
        { "تحت خالص", LocalCommand.GoToBottom },
        { "go to the bottom", LocalCommand.GoToBottom },

        // Stop
        { "اسكت", LocalCommand.Stop },
        { "بس", LocalCommand.Stop },
        { "كفاية", LocalCommand.Stop },
        { "كفايه", LocalCommand.Stop },
        { "خلاص", LocalCommand.Stop },
        { "وقف", LocalCommand.Stop },
        { "بكفي", LocalCommand.Stop },
        { "صافي", LocalCommand.Stop },
        { "يزي", LocalCommand.Stop },
        { "stop", LocalCommand.Stop },
        { "Stop!", LocalCommand.Stop },
        { "quiet", LocalCommand.Stop },
        { "ok stop", LocalCommand.Stop },

        // Repeat
        { "أعد", LocalCommand.Repeat },
        { "عيد", LocalCommand.Repeat },
        { "تاني", LocalCommand.Repeat },
        { "كرر", LocalCommand.Repeat },
        { "كرّر من فضلك", LocalCommand.Repeat },
        { "عاود", LocalCommand.Repeat },
        { "كمان مرة", LocalCommand.Repeat },
        { "شو قلت", LocalCommand.Repeat },
        { "شن قلت", LocalCommand.Repeat },
        { "repeat", LocalCommand.Repeat },
        { "again", LocalCommand.Repeat },
        { "say that again", LocalCommand.Repeat },

        // Sentences
        { "الجملة السابقة", LocalCommand.PreviousSentence },
        { "رجّع الجزء اللي فات", LocalCommand.PreviousSentence },
        { "previous sentence", LocalCommand.PreviousSentence },
        { "الجملة التالية", LocalCommand.NextSentence },
        { "تخطى", LocalCommand.NextSentence },
        { "skip", LocalCommand.NextSentence },

        // Speed
        { "أسرع", LocalCommand.SpeakFaster },
        { "بسرعة شوية", LocalCommand.SpeakFaster },
        { "اسرع شوي", LocalCommand.SpeakFaster },
        { "faster", LocalCommand.SpeakFaster },
        { "أبطأ", LocalCommand.SpeakSlower },
        { "بشويش", LocalCommand.SpeakSlower },
        { "شوية شوية", LocalCommand.SpeakSlower },
        { "على مهلك", LocalCommand.SpeakSlower },
        { "slow down", LocalCommand.SpeakSlower },

        // Navigation, tabs and windows
        { "ارجع", LocalCommand.GoBack },
        { "ارجع لورا", LocalCommand.GoBack },
        { "go back", LocalCommand.GoBack },
        { "روح لقدام", LocalCommand.GoForward },
        { "go forward", LocalCommand.GoForward },
        { "افتح تاب جديد", LocalCommand.NewTab },
        { "new tab", LocalCommand.NewTab },
        { "سكر التاب", LocalCommand.CloseTab },
        { "اقفل التاب", LocalCommand.CloseTab },
        { "close tab", LocalCommand.CloseTab },
        { "افتح نافذة جديدة", LocalCommand.NewWindow },
        { "افتح شباك تاني", LocalCommand.NewWindow },
        { "open a new window", LocalCommand.NewWindow },
        { "بدل النافذة", LocalCommand.SwitchWindow },
        { "switch window", LocalCommand.SwitchWindow },

        // Pause / resume / help
        { "توقف مؤقتا", LocalCommand.Pause },
        { "استنى", LocalCommand.Pause },
        { "pause", LocalCommand.Pause },
        { "كمل", LocalCommand.Resume },
        { "استمر", LocalCommand.Resume },
        { "resume", LocalCommand.Resume },
        { "مساعدة", LocalCommand.Help },
        { "ساعدني", LocalCommand.Help },
        { "help", LocalCommand.Help },
        { "what can I say", LocalCommand.Help },

        // Time, date, battery
        { "الساعة كام", LocalCommand.WhatTime },
        { "كم الساعة", LocalCommand.WhatTime },
        { "الساعه قديش", LocalCommand.WhatTime },
        { "قداش الساعة", LocalCommand.WhatTime },
        { "شحال الساعة", LocalCommand.WhatTime },
        { "كم الساعة الآن؟", LocalCommand.WhatTime },
        { "الساعة كام دلوقتي", LocalCommand.WhatTime },
        { "what time is it", LocalCommand.WhatTime },
        { "What's the time?", LocalCommand.WhatTime },
        { "ما تاريخ اليوم", LocalCommand.WhatDate },
        { "النهارده كام", LocalCommand.WhatDate },
        { "اليوم ايش", LocalCommand.WhatDate },
        { "what's the date", LocalCommand.WhatDate },
        { "البطارية كام", LocalCommand.BatteryStatus },
        { "قديش البطارية", LocalCommand.BatteryStatus },
        { "battery level", LocalCommand.BatteryStatus },

        // Volume
        { "علّي الصوت", LocalCommand.VolumeUp },
        { "على الصوت", LocalCommand.VolumeUp },
        { "ارفع الصوت", LocalCommand.VolumeUp },
        { "louder", LocalCommand.VolumeUp },
        { "وطي الصوت", LocalCommand.VolumeDown },
        { "اخفض الصوت", LocalCommand.VolumeDown },
        { "volume down", LocalCommand.VolumeDown },
        { "اكتم الصوت", LocalCommand.Mute },
        { "mute", LocalCommand.Mute },

        // Modes, OCR, settings, report, key explorer
        { "الوضع الخاص", LocalCommand.PrivateModeOn },
        { "شغل الوضع الخاص", LocalCommand.PrivateModeOn },
        { "private mode on", LocalCommand.PrivateModeOn },
        { "اقفل الوضع الخاص", LocalCommand.PrivateModeOff },
        { "turn off private mode", LocalCommand.PrivateModeOff },
        { "OCR", LocalCommand.OcrWindow },
        { "اقرأ بالتعرف الضوئي", LocalCommand.OcrWindow },
        { "الإعدادات", LocalCommand.Settings },
        { "اعدادات بصيرة", LocalCommand.Settings },
        { "settings", LocalCommand.Settings },
        { "احفظ تقرير لعائلتي", LocalCommand.SaveReport },
        { "save a report for my family", LocalCommand.SaveReport },
        { "مستكشف المفاتيح", LocalCommand.KeyExplorer },
        { "key explorer", LocalCommand.KeyExplorer },

        // Polite fillers around commands
        { "من فضلك اقرأ الشاشة", LocalCommand.ReadScreen },
        { "لو سمحت انزل تحت", LocalCommand.ScrollDown },
        { "يا بصيرة اسكتي", LocalCommand.Stop },
        { "اقرأ الشاشة من فضلك يا بصيرة", LocalCommand.ReadScreen },
        { "please stop", LocalCommand.Stop },
        { "Basira, what time is it?", LocalCommand.WhatTime },
        { "ممكن تقرا الشاشة", LocalCommand.ReadScreen },
        { "ممكن تعيد", LocalCommand.Repeat },
        { "عافاك عاود", LocalCommand.Repeat },
        { "تكفى انزل تحت", LocalCommand.ScrollDown },
        { "بالله عليك وطي الصوت", LocalCommand.VolumeDown },
        { "could you please scroll down", LocalCommand.ScrollDown },
    };

    [Theory]
    [MemberData(nameof(Positives))]
    public void MatchesCommand(string utterance, LocalCommand expected)
    {
        CommandMatch? m = CommandGrammar.Match(utterance);

        Assert.NotNull(m);
        Assert.Equal(expected, m.Command);
        Assert.Null(m.Argument);
        Assert.InRange(m.Confidence, 0.5, 1.0);
    }

    public static TheoryData<string, string> OpenAppPositives => new()
    {
        { "افتح جوجل", "جوجل" },
        { "إفتح كروم", "كروم" },
        { "افتح لي الواتساب", "الواتساب" },
        { "افتحلي يوتيوب", "يوتيوب" },
        { "شغل الحاسبة", "الحاسبه" },
        { "شغّل المفكرة", "المفكره" },
        { "فتحلي فيسبوك", "فيسبوك" },
        { "افتح مايكروسوفت وورد", "مايكروسوفت وورد" },
        { "افتح برنامج الحاسبة", "الحاسبه" },
        { "open chrome", "chrome" },
        { "Open Google Chrome", "google chrome" },
        { "launch notepad", "notepad" },
        { "start microsoft edge", "microsoft edge" },
        { "open the calculator app", "calculator" },
        { "open up spotify", "spotify" },
        { "افتح جوجل من فضلك", "جوجل" },
        { "ممكن تفتح يوتيوب", "يوتيوب" },
        { "please open whatsapp", "whatsapp" },
    };

    [Theory]
    [MemberData(nameof(OpenAppPositives))]
    public void MatchesOpenApp(string utterance, string app)
    {
        CommandMatch? m = CommandGrammar.Match(utterance);

        Assert.NotNull(m);
        Assert.Equal(LocalCommand.OpenApp, m.Command);
        Assert.Equal(app, m.Argument);
    }

    public static TheoryData<string> Negatives => new()
    {
        // Multi-step or compound requests go to the AI.
        "افتح جوجل وابحث عن قطط",
        "افتح جوجل ثم يوتيوب",
        "افتح جوجل وبعدين يوتيوب",
        "افتح جوجل ويوتيوب",
        "open google and search for cats",
        "open youtube then play music",
        "open chrome search cats",
        "افتح جوجل ابحث عن مطاعم",
        "اقرأ الشاشة وبعدين افتح جوجل",
        "افتح نافذة تانية بنفس الصفحة",
        "افتح يوتيوب على أغنية فيروز",

        // "Open" of things that are not apps.
        "افتح الملف",
        "افتح ملف السيرة الذاتية",
        "شغل أغنية فيروز",
        "شغل القرآن",
        "افتح ده",
        "open this",
        "open the file report",
        "open a new document",
        "افتح الرسالة",
        "start over",
        "افتح",
        "open",
        "افتح 123",
        "افتح جوجل كروم الجديد الآن بسرعة",

        // Questions and content requests.
        "الكتاب ده بيتكلم عن إيه",
        "مين بعتلي على واتساب",
        "ما هذا",
        "ما هو المكتوب على الشاشة",
        "what is on the screen",
        "summarize this page",
        "describe the screen",
        "اقرأ آخر رسالة من أحمد",
        "اكتب رسالة لأحمد",
        "ابحث عن مطاعم قريبة",
        "حدّث الجهاز",
        "what time is it in london",
        "كم الساعة في لندن",
        "volume up to fifty",
        "انزل تحت عند زر الإرسال",
        "scroll down to the comments",
        "stop the music",
        "تحت السرير",
        "صباح الخير",
        "شكرا",
        "thank you",
        "hello",
        "نعم",
        "yes",

        // Fillers or punctuation alone, empty, overlong.
        "",
        "   ",
        "؟؟؟",
        "من فضلك",
        "please",
        "يا بصيرة",
        "بس اقرأ",
        "اقرأ اقرأ اقرأ اقرأ اقرأ اقرأ اقرأ اقرأ اقرأ اقرأ اقرأ",
    };

    [Theory]
    [MemberData(nameof(Negatives))]
    public void ReturnsNullWhenUnsure(string utterance)
    {
        Assert.Null(CommandGrammar.Match(utterance));
    }

    [Fact]
    public void Confidence_IsHighestForExactPhrases()
    {
        Assert.Equal(1.0, CommandGrammar.Match("اقرأ الشاشة")!.Confidence);
        Assert.Equal(CommandGrammar.FillerConfidence, CommandGrammar.Match("اقرأ الشاشة من فضلك")!.Confidence);
        Assert.Equal(CommandGrammar.OpenAppConfidence, CommandGrammar.Match("افتح جوجل")!.Confidence);
        Assert.Equal(CommandGrammar.OpenAppFillerConfidence, CommandGrammar.Match("لو سمحت افتح جوجل")!.Confidence);
        Assert.Equal(CommandGrammar.AdaptedVerbConfidence, CommandGrammar.Match("ممكن تنزل تحت")!.Confidence);
        Assert.True(CommandGrammar.FillerConfidence < CommandGrammar.ExactConfidence);
    }

    [Fact]
    public void SpellingVariantsMatchTheSameCommand()
    {
        string[] variants = ["اقرأ الشاشة", "اقرا الشاشه", "إقرَأ الشّاشة!", "اقرأ الشـاشة؟", "  اقرا   الشاشة  "];

        foreach (string v in variants)
        {
            Assert.Equal(LocalCommand.ReadScreen, CommandGrammar.Match(v)?.Command);
            Assert.Equal(1.0, CommandGrammar.Match(v)!.Confidence);
        }
    }

    [Fact]
    public void PhraseTable_HasNoConflictsAfterFolding()
    {
        var seen = new Dictionary<string, LocalCommand>();
        var conflicts = new List<string>();
        foreach ((LocalCommand command, string[] phrases) in CommandPhrases.Table)
        {
            foreach (string phrase in phrases)
            {
                string key = ArabicFolding.Fold(phrase);
                if (seen.TryGetValue(key, out LocalCommand other) && other != command)
                {
                    conflicts.Add($"{phrase}: {other} vs {command}");
                }

                seen[key] = command;
            }
        }

        Assert.Empty(conflicts);
    }

    [Fact]
    public void PhraseTable_EveryEntryMatchesItsCommand()
    {
        foreach ((LocalCommand command, string[] phrases) in CommandPhrases.Table)
        {
            foreach (string phrase in phrases)
            {
                CommandMatch? m = CommandGrammar.Match(phrase);
                Assert.True(m is not null && m.Command == command, $"'{phrase}' should match {command}, got {m?.Command}");
            }
        }
    }

    [Fact]
    public void PhraseTable_CoversEveryCommandExceptOpenApp()
    {
        var covered = CommandPhrases.Table.Select(t => t.Command).ToHashSet();
        foreach (LocalCommand c in Enum.GetValues<LocalCommand>())
        {
            Assert.True(c == LocalCommand.OpenApp || covered.Contains(c), $"{c} has no phrases");
        }
    }

    [Fact]
    public void KnownLocalCommandNames_ListsEveryCommand()
    {
        Assert.Equal(Enum.GetNames<LocalCommand>(), CommandGrammar.KnownLocalCommandNames);
        Assert.Contains("ScrollDown", CommandGrammar.KnownLocalCommandNames);
        Assert.Contains("OpenApp", CommandGrammar.KnownLocalCommandNames);
    }

    [Theory]
    [InlineData("ScrollDown", LocalCommand.ScrollDown)]
    [InlineData("scrolldown", LocalCommand.ScrollDown)]
    [InlineData(" WhatTime ", LocalCommand.WhatTime)]
    public void TryParseName_ParsesCommandNames(string name, LocalCommand expected)
    {
        Assert.True(CommandGrammar.TryParseName(name, out LocalCommand c));
        Assert.Equal(expected, c);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("none")]
    [InlineData("Scroll Down")]
    public void TryParseName_RejectsUnknownNames(string? name)
    {
        Assert.False(CommandGrammar.TryParseName(name, out _));
    }

    [Fact]
    public void OverlongInputIsRejectedCheaply()
    {
        Assert.Null(CommandGrammar.Match(new string('ا', 500)));
    }
}
