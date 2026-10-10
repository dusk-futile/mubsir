using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Basira.Core.Common;

namespace Basira.Core.Text;

/// <summary>
/// Replaces emoji with short spoken names in Arabic or English ("(وجه يبكي من الضحك، ثلاث مرات)").
/// </summary>
/// <remarks>
/// Variation selectors and skin tones are ignored, repeats are collapsed into one name plus a count,
/// keycaps (1\uFE0F\u20E3) become their digit, and unknown emoji are removed silently.
/// </remarks>
public static class EmojiNames
{
    private const char Zwj = '\u200D';
    private const char Vs16 = '\uFE0F';
    private const char Vs15 = '\uFE0E';

    private static readonly Bilingual GenericFlag = new("علم", "flag");

    /// <summary>Emoji names keyed by the emoji without variation selectors or skin tones.</summary>
    internal static readonly IReadOnlyDictionary<string, Bilingual> Names = new Dictionary<string, Bilingual>
    {
        // Faces
        ["😀"] = new("وجه مبتسم", "grinning face"),
        ["😃"] = new("وجه مبتسم بعينين واسعتين", "grinning face with big eyes"),
        ["😄"] = new("وجه ضاحك", "grinning face with smiling eyes"),
        ["😁"] = new("وجه مبتهج", "beaming face"),
        ["😆"] = new("وجه يضحك بعينين مغمضتين", "grinning squinting face"),
        ["😅"] = new("وجه يضحك ويتصبب عرقا", "grinning face with sweat"),
        ["🤣"] = new("يتدحرج من الضحك", "rolling on the floor laughing"),
        ["😂"] = new("وجه يبكي من الضحك", "face with tears of joy"),
        ["🙂"] = new("وجه مبتسم قليلا", "slightly smiling face"),
        ["🙃"] = new("وجه مقلوب", "upside-down face"),
        ["😉"] = new("وجه يغمز", "winking face"),
        ["😊"] = new("وجه مبتسم بخجل", "smiling face with smiling eyes"),
        ["☺"] = new("وجه مبتسم", "smiling face"),
        ["😇"] = new("وجه مبتسم بهالة", "smiling face with halo"),
        ["🥰"] = new("وجه مبتسم بقلوب", "smiling face with hearts"),
        ["😍"] = new("وجه بعينين على شكل قلب", "smiling face with heart eyes"),
        ["🤩"] = new("وجه منبهر", "star-struck"),
        ["😘"] = new("وجه يرسل قبلة", "face blowing a kiss"),
        ["😋"] = new("وجه يتلذذ بالطعام", "face savoring food"),
        ["😛"] = new("وجه يخرج لسانه", "face with tongue"),
        ["😜"] = new("وجه يغمز ويخرج لسانه", "winking face with tongue"),
        ["🤪"] = new("وجه مجنون", "zany face"),
        ["🤗"] = new("وجه يعانق", "hugging face"),
        ["🤭"] = new("وجه يضع يده على فمه", "face with hand over mouth"),
        ["🤫"] = new("وجه يطلب الصمت", "shushing face"),
        ["🤔"] = new("وجه يفكر", "thinking face"),
        ["😐"] = new("وجه محايد", "neutral face"),
        ["😑"] = new("وجه بلا تعبير", "expressionless face"),
        ["😏"] = new("وجه بابتسامة ساخرة", "smirking face"),
        ["😒"] = new("وجه غير مسرور", "unamused face"),
        ["🙄"] = new("وجه يدير عينيه", "face with rolling eyes"),
        ["😬"] = new("وجه متوتر", "grimacing face"),
        ["😌"] = new("وجه مرتاح", "relieved face"),
        ["😔"] = new("وجه حزين", "pensive face"),
        ["😴"] = new("وجه نائم", "sleeping face"),
        ["😷"] = new("وجه بكمامة طبية", "face with medical mask"),
        ["🤒"] = new("وجه مريض", "face with thermometer"),
        ["🤢"] = new("وجه يشعر بالغثيان", "nauseated face"),
        ["🥵"] = new("وجه يشعر بالحر", "hot face"),
        ["🥶"] = new("وجه يشعر بالبرد", "cold face"),
        ["🤯"] = new("رأس ينفجر", "exploding head"),
        ["🥳"] = new("وجه يحتفل", "partying face"),
        ["😎"] = new("وجه بنظارة شمسية", "smiling face with sunglasses"),
        ["😕"] = new("وجه محتار", "confused face"),
        ["😟"] = new("وجه قلق", "worried face"),
        ["🙁"] = new("وجه عابس قليلا", "slightly frowning face"),
        ["☹"] = new("وجه عابس", "frowning face"),
        ["😮"] = new("وجه مندهش", "face with open mouth"),
        ["😲"] = new("وجه مذهول", "astonished face"),
        ["😳"] = new("وجه محرج", "flushed face"),
        ["🥺"] = new("وجه يتوسل", "pleading face"),
        ["😢"] = new("وجه يبكي", "crying face"),
        ["😭"] = new("وجه يبكي بشدة", "loudly crying face"),
        ["😱"] = new("وجه يصرخ من الخوف", "face screaming in fear"),
        ["😞"] = new("وجه خائب الأمل", "disappointed face"),
        ["😩"] = new("وجه متعب", "weary face"),
        ["🥱"] = new("وجه يتثاءب", "yawning face"),
        ["😤"] = new("وجه ينفخ من الغضب", "face with steam from nose"),
        ["😠"] = new("وجه غاضب", "angry face"),
        ["😡"] = new("وجه غاضب جدا", "enraged face"),
        ["🤬"] = new("وجه يشتم", "face with symbols on mouth"),
        ["🤦"] = new("يضع يده على وجهه", "facepalm"),
        ["🤷"] = new("يهز كتفيه", "shrug"),
        ["😈"] = new("وجه شيطان مبتسم", "smiling face with horns"),
        ["💀"] = new("جمجمة", "skull"),
        ["👻"] = new("شبح", "ghost"),
        ["🤖"] = new("روبوت", "robot"),
        ["🙈"] = new("قرد يغطي عينيه", "see-no-evil monkey"),
        ["🙉"] = new("قرد يغطي أذنيه", "hear-no-evil monkey"),
        ["🙊"] = new("قرد يغطي فمه", "speak-no-evil monkey"),

        // Hearts
        ["❤"] = new("قلب أحمر", "red heart"),
        ["♥"] = new("قلب", "heart"),
        ["🧡"] = new("قلب برتقالي", "orange heart"),
        ["💛"] = new("قلب أصفر", "yellow heart"),
        ["💚"] = new("قلب أخضر", "green heart"),
        ["💙"] = new("قلب أزرق", "blue heart"),
        ["💜"] = new("قلب بنفسجي", "purple heart"),
        ["🖤"] = new("قلب أسود", "black heart"),
        ["🤍"] = new("قلب أبيض", "white heart"),
        ["🤎"] = new("قلب بني", "brown heart"),
        ["💔"] = new("قلب مكسور", "broken heart"),
        ["💕"] = new("قلبان", "two hearts"),
        ["💞"] = new("قلوب تدور", "revolving hearts"),
        ["💓"] = new("قلب ينبض", "beating heart"),
        ["💗"] = new("قلب ينمو", "growing heart"),
        ["💖"] = new("قلب لامع", "sparkling heart"),
        ["💘"] = new("قلب يخترقه سهم", "heart with arrow"),
        ["💝"] = new("قلب بشريطة", "heart with ribbon"),
        ["❣"] = new("علامة تعجب على شكل قلب", "heart exclamation"),
        ["❤\u200D🔥"] = new("قلب مشتعل", "heart on fire"),
        ["❤\u200D🩹"] = new("قلب مضمد", "mending heart"),
        ["💋"] = new("أثر قبلة", "kiss mark"),

        // Hands and body
        ["👍"] = new("إبهام للأعلى", "thumbs up"),
        ["👎"] = new("إبهام للأسفل", "thumbs down"),
        ["👌"] = new("علامة موافق", "OK hand"),
        ["✌"] = new("علامة النصر", "victory hand"),
        ["🤞"] = new("أصابع متقاطعة", "crossed fingers"),
        ["👊"] = new("قبضة يد", "oncoming fist"),
        ["✊"] = new("قبضة مرفوعة", "raised fist"),
        ["👏"] = new("تصفيق", "clapping hands"),
        ["🙌"] = new("يدان مرفوعتان", "raising hands"),
        ["🤲"] = new("كفان مرفوعتان", "palms up together"),
        ["🤝"] = new("مصافحة", "handshake"),
        ["🙏"] = new("يدان مضمومتان", "folded hands"),
        ["👋"] = new("يد تلوح", "waving hand"),
        ["✋"] = new("يد مرفوعة", "raised hand"),
        ["🖐"] = new("يد مفتوحة", "hand with fingers splayed"),
        ["☝"] = new("سبابة للأعلى", "index pointing up"),
        ["👆"] = new("إصبع يشير للأعلى", "pointing up"),
        ["👇"] = new("إصبع يشير للأسفل", "pointing down"),
        ["👈"] = new("إصبع يشير لليسار", "pointing left"),
        ["👉"] = new("إصبع يشير لليمين", "pointing right"),
        ["💪"] = new("عضلة مفتولة", "flexed biceps"),
        ["✍"] = new("يد تكتب", "writing hand"),
        ["\u1FAF6"] = new("يدان على شكل قلب", "heart hands"),
        ["👀"] = new("عينان", "eyes"),

        // People
        ["👶"] = new("طفل رضيع", "baby"),
        ["👨"] = new("رجل", "man"),
        ["👩"] = new("امرأة", "woman"),
        ["👪"] = new("عائلة", "family"),
        ["👨\u200D👩\u200D👦"] = new("عائلة", "family"),
        ["👨\u200D👩\u200D👧"] = new("عائلة", "family"),
        ["👨\u200D👩\u200D👧\u200D👦"] = new("عائلة", "family"),

        // Marks and signs
        ["✅"] = new("علامة صح", "check mark"),
        ["✔"] = new("علامة صح", "check mark"),
        ["✓"] = new("علامة صح", "check mark"),
        ["☑"] = new("مربع عليه علامة صح", "checked box"),
        ["❌"] = new("علامة خطأ", "cross mark"),
        ["❎"] = new("علامة خطأ", "cross mark"),
        ["⚠"] = new("تحذير", "warning"),
        ["❗"] = new("علامة تعجب", "exclamation mark"),
        ["❓"] = new("علامة استفهام", "question mark"),
        ["‼"] = new("علامتا تعجب", "double exclamation mark"),
        ["⛔"] = new("ممنوع الدخول", "no entry"),
        ["🚫"] = new("ممنوع", "prohibited"),
        ["🔥"] = new("نار", "fire"),
        ["✨"] = new("بريق", "sparkles"),
        ["⭐"] = new("نجمة", "star"),
        ["🌟"] = new("نجمة لامعة", "glowing star"),
        ["★"] = new("نجمة ممتلئة", "filled star"),
        ["☆"] = new("نجمة فارغة", "empty star"),
        ["💯"] = new("مئة نقطة", "hundred points"),
        ["🔴"] = new("دائرة حمراء", "red circle"),
        ["🟢"] = new("دائرة خضراء", "green circle"),
        ["⬆"] = new("سهم للأعلى", "up arrow"),
        ["⬇"] = new("سهم للأسفل", "down arrow"),
        ["➡"] = new("سهم لليمين", "right arrow"),
        ["⬅"] = new("سهم لليسار", "left arrow"),
        ["🆕"] = new("جديد", "new"),
        ["🔗"] = new("رابط", "link"),

        // Celebrations
        ["🎉"] = new("احتفال", "party popper"),
        ["🎊"] = new("قصاصات احتفال", "confetti ball"),
        ["🎁"] = new("هدية", "gift"),
        ["🎂"] = new("كعكة عيد ميلاد", "birthday cake"),
        ["🎈"] = new("بالون", "balloon"),
        ["🏆"] = new("كأس", "trophy"),
        ["🎓"] = new("قبعة التخرج", "graduation cap"),
        ["👑"] = new("تاج", "crown"),
        ["💎"] = new("جوهرة", "gem stone"),

        // Devices and objects
        ["📞"] = new("سماعة هاتف", "telephone receiver"),
        ["☎"] = new("هاتف", "telephone"),
        ["📱"] = new("هاتف محمول", "mobile phone"),
        ["💻"] = new("حاسوب محمول", "laptop"),
        ["🖥"] = new("حاسوب مكتبي", "desktop computer"),
        ["⌨"] = new("لوحة مفاتيح", "keyboard"),
        ["📷"] = new("كاميرا", "camera"),
        ["📸"] = new("كاميرا بفلاش", "camera with flash"),
        ["📹"] = new("كاميرا فيديو", "video camera"),
        ["🎤"] = new("ميكروفون", "microphone"),
        ["🎧"] = new("سماعات رأس", "headphones"),
        ["🔊"] = new("صوت مرتفع", "speaker high volume"),
        ["🔇"] = new("صوت مكتوم", "muted speaker"),
        ["📢"] = new("مكبر صوت", "loudspeaker"),
        ["🔔"] = new("جرس", "bell"),
        ["📧"] = new("بريد إلكتروني", "e-mail"),
        ["✉"] = new("ظرف", "envelope"),
        ["💬"] = new("فقاعة كلام", "speech balloon"),
        ["📌"] = new("دبوس", "pushpin"),
        ["📍"] = new("علامة موقع", "location pin"),
        ["📎"] = new("مشبك ورق", "paperclip"),
        ["📄"] = new("صفحة", "page"),
        ["📁"] = new("مجلد", "folder"),
        ["📅"] = new("تقويم", "calendar"),
        ["📚"] = new("كتب", "books"),
        ["📖"] = new("كتاب مفتوح", "open book"),
        ["✏"] = new("قلم رصاص", "pencil"),
        ["⏰"] = new("منبه", "alarm clock"),
        ["⌛"] = new("ساعة رملية", "hourglass"),
        ["⏳"] = new("ساعة رملية", "hourglass"),
        ["🔒"] = new("قفل مغلق", "locked"),
        ["🔓"] = new("قفل مفتوح", "unlocked"),
        ["🔑"] = new("مفتاح", "key"),
        ["💡"] = new("مصباح", "light bulb"),
        ["💰"] = new("كيس نقود", "money bag"),
        ["💵"] = new("نقود ورقية", "banknote"),
        ["💳"] = new("بطاقة ائتمان", "credit card"),
        ["🛒"] = new("عربة تسوق", "shopping cart"),
        ["🏠"] = new("منزل", "house"),
        ["🏥"] = new("مستشفى", "hospital"),
        ["💊"] = new("حبة دواء", "pill"),
        ["🚗"] = new("سيارة", "car"),
        ["✈"] = new("طائرة", "airplane"),
        ["🎵"] = new("نغمة موسيقية", "musical note"),
        ["🎶"] = new("نغمات موسيقية", "musical notes"),
        ["⚽"] = new("كرة قدم", "soccer ball"),

        // Faith and nature
        ["🕋"] = new("الكعبة", "Kaaba"),
        ["🕌"] = new("مسجد", "mosque"),
        ["☪"] = new("هلال ونجمة", "star and crescent"),
        ["📿"] = new("مسبحة", "prayer beads"),
        ["🌙"] = new("هلال", "crescent moon"),
        ["☀"] = new("شمس", "sun"),
        ["🌧"] = new("سحابة ممطرة", "cloud with rain"),
        ["🌍"] = new("الكرة الأرضية", "globe"),
        ["🌹"] = new("وردة", "rose"),
        ["🌷"] = new("زهرة توليب", "tulip"),
        ["🌸"] = new("زهرة كرز", "cherry blossom"),
        ["💐"] = new("باقة ورد", "bouquet"),
        ["🌴"] = new("نخلة", "palm tree"),
        ["☕"] = new("مشروب ساخن", "hot beverage"),
        ["🏴"] = new("علم", "flag"),
        ["🏳"] = new("علم أبيض", "white flag"),
    };

    /// <summary>Country names for regional-indicator flags, keyed by ISO 3166 code.</summary>
    internal static readonly IReadOnlyDictionary<string, Bilingual> Countries = new Dictionary<string, Bilingual>
    {
        ["EG"] = new("مصر", "Egypt"),
        ["SA"] = new("السعودية", "Saudi Arabia"),
        ["LY"] = new("ليبيا", "Libya"),
        ["AE"] = new("الإمارات", "the United Arab Emirates"),
        ["MA"] = new("المغرب", "Morocco"),
        ["JO"] = new("الأردن", "Jordan"),
        ["PS"] = new("فلسطين", "Palestine"),
        ["DZ"] = new("الجزائر", "Algeria"),
        ["TN"] = new("تونس", "Tunisia"),
        ["SD"] = new("السودان", "Sudan"),
        ["IQ"] = new("العراق", "Iraq"),
        ["SY"] = new("سوريا", "Syria"),
        ["LB"] = new("لبنان", "Lebanon"),
        ["KW"] = new("الكويت", "Kuwait"),
        ["QA"] = new("قطر", "Qatar"),
        ["BH"] = new("البحرين", "Bahrain"),
        ["OM"] = new("سلطنة عمان", "Oman"),
        ["YE"] = new("اليمن", "Yemen"),
        ["MR"] = new("موريتانيا", "Mauritania"),
        ["US"] = new("الولايات المتحدة", "the United States"),
        ["GB"] = new("المملكة المتحدة", "the United Kingdom"),
        ["TR"] = new("تركيا", "Turkey"),
        ["FR"] = new("فرنسا", "France"),
        ["DE"] = new("ألمانيا", "Germany"),
        ["IT"] = new("إيطاليا", "Italy"),
        ["CA"] = new("كندا", "Canada"),
    };

    private static readonly string[] ArabicCounts = ["", "", "", "ثلاث", "أربع", "خمس", "ست", "سبع", "ثماني", "تسع", "عشر"];

    /// <summary>One emoji found in text.</summary>
    /// <param name="Length">UTF-16 length of the whole sequence.</param>
    /// <param name="Key">Identity used to collapse repeats (modifiers stripped).</param>
    /// <param name="Name">Spoken name, or null when unknown.</param>
    /// <param name="Keep">Text to keep instead of a name (keycap digit, or a plain symbol shown as emoji).</param>
    internal readonly record struct EmojiCluster(int Length, string Key, Bilingual? Name, string? Keep);

    /// <summary>Replaces emoji with parenthesized names in <paramref name="lang"/>.</summary>
    public static string Replace(string text, Lang lang) => Replace(text, lang, parenthesize: true);

    /// <summary>Replaces emoji with names; <paramref name="parenthesize"/> false gives bare names (for math reading).</summary>
    public static string Replace(string text, Lang lang, bool parenthesize)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!MayContainEmoji(text))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 32);
        int n = text.Length;
        int i = 0;
        while (i < n)
        {
            if (!TryReadCluster(text, i, out var cluster))
            {
                sb.Append(text[i]);
                i++;
                continue;
            }

            int end = i + cluster.Length;
            if (cluster.Name is { } name)
            {
                int count = 1;
                while (true)
                {
                    int j = end;
                    while (j < n && text[j] == ' ')
                    {
                        j++;
                    }

                    if (j < n && TryReadCluster(text, j, out var next) && next.Key == cluster.Key)
                    {
                        count++;
                        end = j + next.Length;
                    }
                    else
                    {
                        break;
                    }
                }

                AppendSpoken(sb, Phrase(name, count, lang, parenthesize), text, end);
            }
            else if (cluster.Keep is { } keep)
            {
                sb.Append(keep);
            }
            else
            {
                end = RemoveCluster(sb, text, end);
            }

            i = end;
        }

        return sb.ToString();
    }

    /// <summary>Removes all emoji (keycaps keep their digit), for when emoji should not be spoken.</summary>
    public static string Remove(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!MayContainEmoji(text))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (!TryReadCluster(text, i, out var cluster))
            {
                sb.Append(text[i]);
                i++;
                continue;
            }

            int end = i + cluster.Length;
            if (cluster.Keep is { } keep)
            {
                sb.Append(keep);
            }
            else
            {
                end = RemoveCluster(sb, text, end);
            }

            i = end;
        }

        return sb.ToString();
    }

    /// <summary>Looks up the name of one emoji (modifiers allowed).</summary>
    public static bool TryGetName(string emoji, Lang lang, [NotNullWhen(true)] out string? name)
    {
        ArgumentNullException.ThrowIfNull(emoji);
        name = null;
        if (emoji.Length == 0 || !TryReadCluster(emoji, 0, out var cluster) || cluster.Length != emoji.Length || cluster.Name is not { } n)
        {
            return false;
        }

        name = n.For(lang);
        return true;
    }

    /// <summary>Number of named emoji (excluding flags).</summary>
    public static int Count => Names.Count;

    private static bool MayContainEmoji(string text) => text.AsSpan().IndexOfAnyInRange('\u2000', '\uFFFF') >= 0;

    private static string Phrase(Bilingual name, int count, Lang lang, bool parenthesize)
    {
        string core = name.For(lang);
        if (count > 1)
        {
            core += lang == Lang.Arabic ? "، " + ArabicTimes(count) : ", " + EnglishTimes(count);
        }

        return parenthesize ? "(" + core + ")" : core;
    }

    private static string ArabicTimes(int count) => count switch
    {
        2 => "مرتين",
        <= 10 => ArabicCounts[count] + " مرات",
        _ => count.ToString(CultureInfo.InvariantCulture) + " مرة",
    };

    private static string EnglishTimes(int count) =>
        count == 2 ? "twice" : count.ToString(CultureInfo.InvariantCulture) + " times";

    private static void AppendSpoken(StringBuilder sb, string phrase, string text, int end)
    {
        if (sb.Length > 0 && !char.IsWhiteSpace(sb[^1]) && sb[^1] is not ('(' or '[' or '"' or '«'))
        {
            sb.Append(' ');
        }

        sb.Append(phrase);
        if (end < text.Length && !char.IsWhiteSpace(text[end])
            && text[end] is not ('.' or ',' or '!' or '?' or '؟' or '،' or '؛' or ';' or ':' or ')' or ']' or '"' or '»'))
        {
            sb.Append(' ');
        }
    }

    /// <summary>Drops an emoji without leaving doubled spaces or gluing the neighbouring words together.</summary>
    private static int RemoveCluster(StringBuilder sb, string text, int end)
    {
        if (sb.Length == 0 || char.IsWhiteSpace(sb[^1]))
        {
            while (end < text.Length && text[end] == ' ')
            {
                end++;
            }

            if (end >= text.Length)
            {
                while (sb.Length > 0 && sb[^1] == ' ')
                {
                    sb.Length--;
                }
            }
        }
        else if (end < text.Length && char.IsLetterOrDigit(text[end]))
        {
            sb.Append(' ');
        }

        return end;
    }

    /// <summary>Reads one emoji sequence at <paramref name="index"/>; false when the character there is not an emoji.</summary>
    internal static bool TryReadCluster(string text, int index, out EmojiCluster cluster)
    {
        cluster = default;
        if (!Rune.TryGetRuneAt(text, index, out Rune first))
        {
            return false;
        }

        int cp = first.Value;
        int pos = index + first.Utf16SequenceLength;

        if (cp is (>= '0' and <= '9') or '#' or '*')
        {
            int p = pos;
            if (p < text.Length && text[p] == Vs16)
            {
                p++;
            }

            if (p < text.Length && text[p] == '\u20E3')
            {
                string digit = ((char)cp).ToString();
                cluster = new EmojiCluster(p + 1 - index, digit, null, digit);
                return true;
            }

            return false;
        }

        if (cp is Vs16 or Vs15 || IsSkinTone(cp))
        {
            cluster = new EmojiCluster(pos - index, "", null, null);
            return true;
        }

        if (IsRegionalIndicator(cp))
        {
            if (Rune.TryGetRuneAt(text, pos, out Rune second) && IsRegionalIndicator(second.Value))
            {
                string code = string.Concat((char)('A' + cp - 0x1F1E6), (char)('A' + second.Value - 0x1F1E6));
                Bilingual flag = Countries.TryGetValue(code, out var country)
                    ? new Bilingual("علم " + country.Ar, "flag of " + country.En)
                    : GenericFlag;
                cluster = new EmojiCluster(pos + second.Utf16SequenceLength - index, "flag:" + code, flag, null);
                return true;
            }

            cluster = new EmojiCluster(pos - index, "", null, null);
            return true;
        }

        bool pictograph = IsPictograph(cp);
        if (!pictograph && !IsSymbolCandidate(cp))
        {
            return false;
        }

        var key = new StringBuilder(first.ToString());
        bool emojiPresentation = false;
        pos = SkipModifiers(text, pos, ref emojiPresentation);
        while (pos + 1 < text.Length && text[pos] == Zwj
            && Rune.TryGetRuneAt(text, pos + 1, out Rune next)
            && (IsPictograph(next.Value) || IsSymbolCandidate(next.Value)))
        {
            key.Append(Zwj).Append(next.ToString());
            pos = SkipModifiers(text, pos + 1 + next.Utf16SequenceLength, ref emojiPresentation);
        }

        string k = key.ToString();
        bool known = Names.TryGetValue(k, out var name);
        if (!known && k.Contains(Zwj))
        {
            known = Names.TryGetValue(first.ToString(), out name);
        }

        if (!pictograph && !known && !emojiPresentation)
        {
            return false; // a plain symbol such as © or → that is not shown as emoji
        }

        int length = pos - index;
        cluster = known
            ? new EmojiCluster(length, k, name, null)
            : new EmojiCluster(length, k, null, pictograph ? null : first.ToString());
        return true;
    }

    private static int SkipModifiers(string text, int pos, ref bool emojiPresentation)
    {
        while (pos < text.Length)
        {
            char c = text[pos];
            if (c is Vs16 or Vs15)
            {
                emojiPresentation |= c == Vs16;
                pos++;
                continue;
            }

            if (Rune.TryGetRuneAt(text, pos, out Rune r) && (IsSkinTone(r.Value) || r.Value is >= 0xE0020 and <= 0xE007F))
            {
                pos += r.Utf16SequenceLength;
                continue;
            }

            break;
        }

        return pos;
    }

    private static bool IsSkinTone(int cp) => cp is >= 0x1F3FB and <= 0x1F3FF;

    private static bool IsRegionalIndicator(int cp) => cp is >= 0x1F1E6 and <= 0x1F1FF;

    /// <summary>Supplementary-plane pictographs, always emoji.</summary>
    private static bool IsPictograph(int cp) =>
        cp is >= 0x1F000 and <= 0x1FAFF
        && cp is not (>= 0x1F700 and <= 0x1F7DF)
        && cp is not (>= 0x1F800 and <= 0x1F8FF)
        && !IsRegionalIndicator(cp)
        && !IsSkinTone(cp);

    /// <summary>Basic-plane symbols that are emoji only when named here or followed by U+FE0F.</summary>
    private static bool IsSymbolCandidate(int cp) =>
        cp is 0x00A9 or 0x00AE or 0x203C or 0x2049 or 0x2122 or 0x2139 or 0x24C2
            or 0x2934 or 0x2935 or 0x3030 or 0x303D or 0x3297 or 0x3299
            or (>= 0x2190 and <= 0x21FF)
            or (>= 0x2300 and <= 0x23FF)
            or (>= 0x25A0 and <= 0x27BF)
            or (>= 0x2B00 and <= 0x2BFF);
}
