namespace Mubsir.Core.Router;

internal static class CommandPhrases
{
    public static readonly (LocalCommand Command, string[] Phrases)[] Table =
    [
        (LocalCommand.ReadScreen,
        [
            "اقرأ الشاشة", "اقرا الشاشة", "إقرأ الشاشة", "اقرأ لي الشاشة", "اقرا لي الشاشة", "اقرالي الشاشة",
            "اقرألي الشاشة", "اقري الشاشة", "اقريلي الشاشة", "اقرأ ليا الشاشة", "اقرا ليا الشاشة", "قرا الشاشة",
            "قرا ليا الشاشة", "قرالي الشاشة", "اقرأ الشاشة كلها", "اقرأ كل الشاشة", "اقرأ ما على الشاشة",
            "اقرأ ما في الشاشة", "اقرأ اللي على الشاشة", "اقرا اللي ع الشاشة", "اقرا اللي عالشاشة",
            "اقرا اللي في الشاشة", "اقرالي اللي على الشاشة", "اقرأ النافذة", "اقرا النافذة", "اقرأ الصفحة",
            "اقرا الصفحة", "اقرالي الصفحة", "اقرأ لي الصفحة",
            "read the screen", "read screen", "read the whole screen", "read this screen", "read me the screen",
            "read the page", "read this page", "read the window", "read everything on the screen",
            "read what's on the screen",
        ]),
        (LocalCommand.ReadFromHere,
        [
            "اقرأ من هنا", "اقرا من هنا", "اقرالي من هنا", "اقرأ لي من هنا", "اقرا من هون", "اقرأ من هون",
            "اقرا من هني", "قرا من هنا", "كمل القراءة", "كمل قراءة", "تابع القراءة", "واصل القراءة",
            "استمر في القراءة", "كمل القراية", "كمل قراية", "كمل اقرا",
            "read from here", "continue reading", "keep reading", "start reading", "read on",
        ]),
        (LocalCommand.WhereAmI,
        [
            "أين أنا", "أنا أين", "أنا فين", "فين أنا", "أنا وين", "وين أنا", "وينني", "أنا في أي نافذة",
            "في أي نافذة أنا", "أنا في أي برنامج", "أين أنا الآن",
            "where am i", "what window is this", "which window am i in", "which app am i in",
        ]),
        (LocalCommand.ScrollDown,
        [
            "انزل", "نزل", "انزل تحت", "نزل تحت", "نزّل لتحت", "انزل لتحت", "روح تحت", "روح لتحت", "اهبط",
            "اهبط لتحت", "هبط", "تحت", "لتحت", "تحت شوية", "انزل شوية", "انزل شوي", "نزل شوية", "نزل شوي",
            "انزل لوطا", "اهبط لوطا", "هبط لوطا", "لوطا", "إلى الأسفل", "للأسفل", "مرر للأسفل", "مرر لأسفل",
            "مرر لتحت", "انزل للأسفل", "انزل لأسفل", "انزل في الصفحة", "انزل بالصفحة",
            "scroll down", "go down", "down", "move down",
        ]),
        (LocalCommand.ScrollUp,
        [
            "اطلع", "طلع", "اطلع فوق", "طلع فوق", "طلّع لفوق", "اطلع لفوق", "روح فوق", "روح لفوق", "اصعد",
            "اصعد لفوق", "اصعد للأعلى", "فوق", "لفوق", "فوق شوية", "اطلع شوية", "اطلع شوي", "طلع شوية",
            "طلع شوي", "إلى الأعلى", "للأعلى", "مرر للأعلى", "مرر لأعلى", "مرر لفوق", "اطلع للأعلى",
            "اطلع في الصفحة", "اطلع بالصفحة",
            "scroll up", "go up", "up", "move up",
        ]),
        (LocalCommand.PageDown,
        [
            "صفحة لتحت", "صفحة تحت", "انزل صفحة", "نزل صفحة", "صفحة للأسفل", "انزل صفحة كاملة",
            "page down", "one page down",
        ]),
        (LocalCommand.PageUp,
        [
            "صفحة لفوق", "صفحة فوق", "اطلع صفحة", "طلع صفحة", "صفحة للأعلى", "اطلع صفحة كاملة",
            "page up", "one page up",
        ]),
        (LocalCommand.GoToTop,
        [
            "أول الصفحة", "لأول الصفحة", "روح لأول الصفحة", "روح أول الصفحة", "اذهب إلى أول الصفحة",
            "اطلع لأول الصفحة", "فوق خالص", "اطلع لفوق خالص", "بداية الصفحة", "لبداية الصفحة",
            "اذهب إلى بداية الصفحة", "روح للبداية", "اذهب إلى البداية", "ارجع للبداية", "أعلى الصفحة",
            "لأعلى الصفحة", "اذهب إلى أعلى الصفحة",
            "go to the top", "go to top", "top of the page", "scroll to the top", "scroll to top", "back to top",
            "go to the top of the page",
        ]),
        (LocalCommand.GoToBottom,
        [
            "آخر الصفحة", "لآخر الصفحة", "روح لآخر الصفحة", "روح آخر الصفحة", "اذهب إلى آخر الصفحة",
            "انزل لآخر الصفحة", "تحت خالص", "انزل لتحت خالص", "نهاية الصفحة", "لنهاية الصفحة",
            "اذهب إلى نهاية الصفحة", "روح للنهاية", "اذهب إلى النهاية", "أسفل الصفحة", "لأسفل الصفحة",
            "اذهب إلى أسفل الصفحة",
            "go to the bottom", "go to bottom", "bottom of the page", "end of the page", "scroll to the bottom",
            "scroll to bottom", "go to the end", "go to the bottom of the page",
        ]),
        (LocalCommand.Stop,
        [
            "اسكت", "اسكتي", "سكت", "بس", "بس خلاص", "خلاص", "خلاص بس", "خلص", "كفاية", "كفى", "يكفي",
            "بكفي", "وقف", "وقّف", "توقف", "قف", "أوقف", "وقف الكلام", "أوقف الكلام", "وقف القراءة",
            "أوقف القراءة", "توقف عن القراءة", "توقف عن الكلام", "بطل كلام", "صافي", "يزي", "سكوت",
            "stop", "stop it", "quiet", "be quiet", "shut up", "enough", "silence", "stop talking", "stop reading",
        ]),
        (LocalCommand.Repeat,
        [
            "أعد", "أعدها", "عيد", "عيدها", "عيده", "تاني", "عيد تاني", "قول تاني", "قولها تاني", "قوله تاني",
            "كرر", "كررها", "أعد مرة أخرى", "أعد ما قلت", "أعد الجملة", "كرر الجملة", "عيد الجملة",
            "مرة تانية", "مرة ثانية", "مرة أخرى", "كمان مرة", "عاود", "عاودها", "عاود تاني", "ماذا قلت",
            "شو قلت", "إيش قلت", "قلت إيه", "قلت ايش", "شنو قلت", "شن قلت", "واش قلت",
            "repeat", "repeat that", "repeat it", "again", "say again", "say that again", "say it again",
            "one more time", "come again", "what did you say", "pardon",
        ]),
        (LocalCommand.PreviousSentence,
        [
            "الجملة السابقة", "الجملة اللي فاتت", "الجملة اللي قبلها", "الجملة اللي قبل", "ارجع جملة",
            "رجع جملة", "ارجع جملة لورا", "جملة لورا", "رجّع الجزء اللي فات", "ارجع للجملة السابقة",
            "ارجع للجملة اللي فاتت",
            "previous sentence", "go back a sentence", "back one sentence", "sentence before",
        ]),
        (LocalCommand.NextSentence,
        [
            "الجملة التالية", "الجملة اللي بعدها", "الجملة الجاية", "جملة لقدام", "اقفز جملة", "تخطى",
            "تخطاها", "فوتها",
            "next sentence", "skip", "skip this", "skip it", "skip sentence", "skip this sentence",
        ]),
        (LocalCommand.SpeakFaster,
        [
            "أسرع", "بسرعة", "بسرعة شوية", "أسرع شوية", "أسرع من كده", "أسرع من هيك", "تكلم أسرع",
            "تكلم بسرعة", "اتكلم أسرع", "اتكلم بسرعة", "اتكلمي بسرعة", "اقرأ أسرع", "اقرأ بسرعة",
            "زود السرعة", "ارفع السرعة", "علّي السرعة", "سرّع", "سرع شوي", "زرب", "زيد السرعة",
            "speed up", "faster", "speak faster", "talk faster", "read faster", "go faster",
        ]),
        (LocalCommand.SpeakSlower,
        [
            "أبطأ", "أبطئ", "ببطء", "بشويش", "شوية شوية", "شوي شوي", "على مهلك", "براحة", "بالراحة",
            "بشوية", "بالشوية", "على راحتك", "اقرأ أبطأ", "اقرأ ببطء", "اقرا بشويش", "اقرأ بالراحة",
            "تكلم أبطأ", "تكلم ببطء", "اتكلم بشويش", "اتكلم براحة", "هدي السرعة", "قلل السرعة",
            "خفض السرعة", "خفف السرعة", "نزل السرعة", "وطي السرعة", "نقص السرعة",
            "slower", "slow down", "speak slower", "talk slower", "read slower", "go slower", "more slowly",
        ]),
        (LocalCommand.GoBack,
        [
            "ارجع", "ارجع ورا", "ارجع لورا", "ارجع للوراء", "ارجع للخلف", "رجوع", "للخلف", "عد للخلف",
            "ارجع للصفحة السابقة", "ارجع للصفحة اللي فاتت", "ارجع صفحة", "رجعني", "رجعني ورا",
            "go back", "back", "go back a page",
        ]),
        (LocalCommand.GoForward,
        [
            "تقدم", "للأمام", "إلى الأمام", "روح لقدام", "روح قدام", "قدام", "لقدام", "تقدم للأمام",
            "اذهب للأمام",
            "go forward", "forward", "go forward a page",
        ]),
        (LocalCommand.NewTab,
        [
            "تبويب جديد", "علامة تبويب جديدة", "افتح تبويب جديد", "افتح علامة تبويب جديدة", "افتح تاب جديد",
            "تاب جديد", "افتح تابة جديدة", "تبويبة جديدة", "افتح تبويبة جديدة", "افتح تاب تاني",
            "افتح تبويب آخر",
            "new tab", "open a new tab", "open new tab", "open another tab",
        ]),
        (LocalCommand.CloseTab,
        [
            "اقفل التاب", "اقفل التاب ده", "قفل التاب", "سكر التاب", "بند التاب", "سد التاب", "أغلق التبويب",
            "أغلق علامة التبويب", "اقفل التبويب", "سكر التبويب", "أغلق التاب",
            "close tab", "close the tab", "close this tab",
        ]),
        (LocalCommand.NewWindow,
        [
            "نافذة جديدة", "افتح نافذة جديدة", "افتح نافذة تانية", "افتح نافذة ثانية", "افتح نافذة أخرى",
            "شباك جديد", "افتح شباك جديد", "افتح شباك تاني",
            "new window", "open a new window", "open new window", "open another window",
        ]),
        (LocalCommand.SwitchWindow,
        [
            "بدل النافذة", "بدل النوافذ", "غير النافذة", "النافذة التالية", "انتقل للنافذة التالية",
            "روح للنافذة التالية", "النافذة اللي بعدها", "بدل الشباك", "غير الشباك",
            "switch window", "switch windows", "next window", "change window",
        ]),
        (LocalCommand.Pause,
        [
            "توقف مؤقتًا", "وقف مؤقت", "وقف مؤقتًا", "إيقاف مؤقت", "استنى", "استنى شوية", "استنا", "تسنى",
            "انتظر", "انتظر لحظة", "لحظة", "اصبر", "اصبر شوي",
            "pause", "wait", "hold on", "wait a moment", "wait a second", "pause reading",
        ]),
        (LocalCommand.Resume,
        [
            "كمل", "كملي", "تابع", "استمر", "واصل", "استأنف",
            "resume", "continue", "go on", "carry on", "keep going",
        ]),
        (LocalCommand.Help,
        [
            "مساعدة", "المساعدة", "ساعدني", "الأوامر", "ما هي الأوامر", "إيه الأوامر", "ماذا أستطيع أن أقول",
            "ماذا يمكنني أن أقول", "ماذا أقول", "أقول إيه", "شو بقدر قول", "شو فيني قول", "إيش أقول",
            "إيش أقدر أقول", "واش نقول", "شنو نقول",
            "help", "help me", "what can i say", "what can you do", "what do i say", "commands", "list commands",
        ]),
        (LocalCommand.WhatTime,
        [
            "الساعة كام", "الساعة كم", "كم الساعة", "الساعة قديش", "قديش الساعة", "قداش الساعة",
            "الساعة قداش", "شحال الساعة", "شحال فالساعة", "شحال في الساعة", "الساعة شحال", "كم الوقت",
            "الوقت كام", "قديش الوقت", "ما الوقت", "ما هو الوقت", "ما الساعة", "الوقت", "الساعة",
            "قل لي الوقت", "قولي الساعة كام", "قول الساعة",
            "what time is it", "what's the time", "what is the time", "the time", "time", "tell me the time",
            "current time",
        ]),
        (LocalCommand.WhatDate,
        [
            "التاريخ", "ما التاريخ", "ما تاريخ اليوم", "تاريخ اليوم", "ما هو تاريخ اليوم", "النهارده كام",
            "النهاردة كام في الشهر", "اليوم كم", "اليوم كم في الشهر", "اليوم إيش", "إيش اليوم",
            "النهاردة إيه", "كم التاريخ", "إيه التاريخ", "التاريخ كام", "شو التاريخ", "شو التاريخ اليوم",
            "شو اليوم", "اليوم شو", "قديش اليوم", "قداش اليوم", "شنو التاريخ", "شن التاريخ",
            "what's the date", "what is the date", "what date is it", "today's date", "what day is it",
            "what's today", "what is today", "date", "the date",
        ]),
        (LocalCommand.BatteryStatus,
        [
            "البطارية", "كم البطارية", "البطارية كام", "البطارية كم", "نسبة البطارية", "كم نسبة البطارية",
            "الشحن", "كم الشحن", "الشحن كام", "الشحن كم", "شحن البطارية", "كم باقي في البطارية",
            "قديش البطارية", "قداش البطارية", "شحال البطارية", "حالة البطارية",
            "battery", "battery status", "battery level", "how much battery", "how much battery is left",
            "how much battery do i have", "battery percentage",
        ]),
        (LocalCommand.VolumeUp,
        [
            "علّي الصوت", "ارفع الصوت", "زود الصوت", "كبر الصوت", "الصوت أعلى", "طلع الصوت",
            "volume up", "louder", "turn it up", "turn up the volume", "turn the volume up", "increase volume",
            "increase the volume", "raise the volume",
        ]),
        (LocalCommand.VolumeDown,
        [
            "وطي الصوت", "اخفض الصوت", "خفض الصوت", "قلل الصوت", "نزل الصوت", "صغر الصوت", "هدي الصوت",
            "خفف الصوت", "نقص الصوت", "الصوت أوطى",
            "volume down", "quieter", "softer", "turn it down", "turn down the volume", "turn the volume down",
            "decrease volume", "decrease the volume", "lower the volume", "lower volume",
        ]),
        (LocalCommand.Mute,
        [
            "اكتم الصوت", "كتم الصوت", "اكتم", "اقفل الصوت", "سكت الصوت", "اطفي الصوت", "إطفاء الصوت",
            "mute", "mute the sound", "mute volume", "mute the volume", "mute sound",
        ]),
        (LocalCommand.PrivateModeOn,
        [
            "الوضع الخاص", "وضع خاص", "فعّل الوضع الخاص", "شغل الوضع الخاص", "افتح الوضع الخاص",
            "ادخل الوضع الخاص", "تشغيل الوضع الخاص", "الوضع الخاص تشغيل",
            "private mode", "private mode on", "turn on private mode", "enable private mode", "go private",
            "start private mode",
        ]),
        (LocalCommand.PrivateModeOff,
        [
            "أوقف الوضع الخاص", "اقفل الوضع الخاص", "قفل الوضع الخاص", "ألغِ الوضع الخاص", "إلغاء الوضع الخاص",
            "اطفي الوضع الخاص", "عطّل الوضع الخاص", "اخرج من الوضع الخاص", "إيقاف الوضع الخاص",
            "الوضع الخاص إيقاف", "سكر الوضع الخاص",
            "private mode off", "turn off private mode", "disable private mode", "exit private mode",
            "stop private mode", "leave private mode",
        ]),
        (LocalCommand.OcrWindow,
        [
            "اعمل OCR", "اقرأ بالتعرف الضوئي", "التعرف الضوئي", "تعرف ضوئي", "اقرأ بالتعرف على النص",
            "أو سي آر", "اعمل أو سي آر", "اقرأ الصورة بالتعرف الضوئي",
            "ocr", "do ocr", "ocr the window", "ocr this window", "read with ocr",
        ]),
        (LocalCommand.Settings,
        [
            "الإعدادات", "إعدادات", "settings",
        ]),
        (LocalCommand.SaveReport,
        [
            "احفظ تقرير", "احفظ تقريرًا", "احفظ تقرير لعائلتي", "احفظ تقريرًا لعائلتي", "احفظ تقرير للعائلة",
            "احفظ تقرير لأهلي", "احفظ تقرير للأسرة", "احفظ تقرير لأسرتي", "احفظ تقرير للعيلة", "اعمل تقرير",
            "اعمل تقرير لأهلي", "اعمل تقرير للعيلة",
            "save a report", "save report", "make a report", "create a report",
        ]),
        (LocalCommand.KeyExplorer,
        [
            "مستكشف المفاتيح", "مستكشف الأزرار", "استكشاف المفاتيح", "اكتشاف المفاتيح", "افتح مستكشف المفاتيح",
            "شغل مستكشف المفاتيح", "وضع استكشاف المفاتيح",
            "key explorer", "keyboard explorer", "explore keys", "explore the keyboard", "open key explorer",
            "start key explorer",
        ]),
    ];

    public static readonly string[] LeadingFillers =
[
    "من فضلك", "من فضلكم", "لو سمحت", "لو سمحتي", "لو سمحتم", "إذا سمحت", "يا مُبصِر", "مُبصِر", "ممكن",
        "يلا", "طيب", "بالله", "بالله عليك", "الله يخليك", "الله يخليكي", "الله يرضى عليك", "الله يعافيك",
        "عافاك", "تكفى", "لو تكرمت", "معلش", "بليز",
        "please", "pls", "plz", "kindly", "just", "hey", "ok", "okay", "mubsir", "hey mubsir", "ok mubsir",
        "okay mubsir", "hi mubsir", "يا mubsir", "can you", "could you", "would you", "will you",
        "can you please", "could you please",
    ];

    public static readonly string[] TrailingFillers =
[
    "من فضلك", "من فضلكم", "لو سمحت", "لو سمحتي", "لو سمحتم", "إذا سمحت", "يا مُبصِر", "مُبصِر", "يلا",
        "الله يخليك", "الله يعافيك", "عافاك", "تكفى", "لو تكرمت", "بليز", "شكرًا", "شكرًا لك", "شكرًا جزيلًا",
        "الآن", "الحين", "دلوقتي", "دلوقت", "هلق", "هلأ", "توا", "دابا", "حالًا", "شوية", "شوي",
        "please", "pls", "plz", "mubsir", "thanks", "thank you", "now", "right now", "a bit", "a little",
        "a little bit", "for me",
    ];

    public static readonly string[] OpenVerbs =
[
    "افتح", "افتحلي", "افتحي", "افتحيلي", "فتح", "فتحلي", "شغل", "شغللي", "open", "launch", "start",
    ];

    public static readonly string[] OpenVerbParticles = ["لي", "ليا", "لنا", "up"];

    public static readonly string[] AppNameNoise = ["the", "a", "an", "my", "برنامج", "تطبيق", "app", "application", "program"];

    public static readonly string[] ArgumentStopWords =
[
    "و", "ثم", "بعدين", "وبعدين", "بعدها", "بعد", "عشان", "علشان", "لكي", "كي", "حتى", "في", "على", "عن",
        "من", "إلى", "مع", "اللي", "الذي", "التي", "إذا", "لو", "أو", "لي", "ليا", "and", "then", "to", "in",
        "on", "with", "for", "from", "at", "of", "or", "after", "if", "so", "about", "into", "by",
    ];

    public static readonly string[] ArgumentVerbs =
[
    "ابحث", "دور", "اكتب", "اقرأ", "افتح", "شغل", "روح", "ادخل", "اضغط", "ابعت", "ارسل", "اعمل", "شوف",
        "اعرض", "كلم", "اتصل", "اقفل", "سكر", "search", "type", "write", "play", "send", "find", "read", "go",
        "click", "press", "call", "show", "look", "check", "open", "launch", "start", "run", "close",
    ];

    public static readonly string[] NonAppFirstWords =
[
    "ملف", "الملف", "ملفات", "الملفات", "مجلد", "المجلد", "فولدر", "الفولدر", "صورة", "الصورة", "صور",
        "الصور", "رسالة", "الرسالة", "رسائل", "الرسائل", "ايميل", "الايميل", "إيميل", "الإيميل", "البريد",
        "نافذة", "النافذة", "شباك", "الشباك", "تاب", "التاب", "تبويب", "التبويب", "صفحة", "الصفحة", "رابط",
        "الرابط", "لينك", "اللينك", "موقع", "الموقع", "أغنية", "الأغنية", "أغاني", "الأغاني", "فيديو",
        "الفيديو", "موسيقى", "الموسيقى", "قرآن", "القرآن", "سورة", "السورة", "راديو", "الراديو", "إذاعة",
        "الإذاعة", "فيلم", "الفيلم", "مسلسل", "المسلسل", "حلقة", "الحلقة", "بودكاست", "جهاز", "الجهاز",
        "كمبيوتر", "الكمبيوتر", "الصوت", "النت", "الإنترنت", "الواي", "واي", "البلوتوث", "بلوتوث", "الوضع",
        "وضع", "مستند", "المستند", "وثيقة", "الوثيقة", "كتاب", "الكتاب", "رقم", "كل", "الكل",
        "هنا", "هذا", "هذه", "ده", "دي", "دا", "هاد", "هادا", "هيدا", "هيدي", "هاي", "هذي", "اياه",
        "it", "this", "that", "these", "those", "them", "here", "there", "file", "files", "folder", "folders",
        "window", "windows", "tab", "page", "link", "website", "site", "song", "music", "video", "movie",
        "podcast", "radio", "episode", "playlist", "email", "mail", "message", "messages", "picture", "photo",
        "image", "document", "new", "another", "reading", "over", "again", "everything", "all",

        "ماذا", "ما", "ايه", "إيه", "شو", "ايش", "إيش", "شنو", "شن", "واش", "أي", "اي", "مين", "من", "وين", "فين",
        "كيف", "ازاي", "إزاي", "ليش", "ليه", "لماذا", "what", "which", "who", "where", "how", "why",
    ];

    public static readonly string[] WawAppWords = ["وورد", "ورد", "واتساب", "واتس", "ويندوز", "ويكيبيديا", "ويب", "وان", "ون", "وي", "وايبر"];
}
