namespace Basira.Core.Text;

/// <summary>Word tables for <see cref="SymbolVerbalizer"/>. The Arabic math terms are to be reviewed by a math teacher.</summary>
internal static class SymbolTables
{
    public static readonly Bilingual Percent = new("بالمئة", "percent");
    public static readonly Bilingual And = new("و", "and");
    public static readonly Bilingual At = new("آت", "at");
    public static readonly Bilingual Hashtag = new("هاشتاج", "hashtag");
    public static readonly Bilingual NumberSign = new("رقم", "number");
    public static readonly Bilingual Squared = new("تربيع", "squared");
    public static readonly Bilingual Cubed = new("تكعيب", "cubed");
    public static readonly Bilingual Power = new("أس", "to the power of");
    public static readonly Bilingual Degrees = new("درجة", "degrees");
    public static readonly Bilingual Degree = new("درجة", "degree");
    public static readonly Bilingual Celsius = new("درجة مئوية", "degrees Celsius");
    public static readonly Bilingual CelsiusOne = new("درجة مئوية", "degree Celsius");
    public static readonly Bilingual Fahrenheit = new("درجة فهرنهايت", "degrees Fahrenheit");
    public static readonly Bilingual FahrenheitOne = new("درجة فهرنهايت", "degree Fahrenheit");
    public static readonly Bilingual DecimalPoint = new("فاصلة", "point");
    public static readonly Bilingual Minus = new("ناقص", "minus");
    public static readonly Bilingual Negative = new("سالب", "negative");
    public static readonly Bilingual Times = new("ضرب", "times");
    public static readonly Bilingual Factorial = new("مضروب", "factorial");
    public static readonly Bilingual Prime = new("شرطة", "prime");
    public static readonly Bilingual DoublePrime = new("شرطتان", "double prime");
    public static readonly Bilingual TriplePrime = new("ثلاث شرطات", "triple prime");
    public static readonly Bilingual Subscript = new("سفلي", "sub");
    public static readonly Bilingual Ratio = new("إلى", "to");
    public static readonly Bilingual Comma = new("فاصلة", "comma");
    public static readonly Bilingual Capital = new("كبير", "capital");
    public static readonly Bilingual VerticalBar = new("خط عمودي", "vertical bar");

    /// <summary>A vulgar fraction read alone ("½ كوب") or after a whole number ("2½").</summary>
    public readonly record struct FractionWords(Bilingual Alone, Bilingual AfterNumber);

    public static readonly IReadOnlyDictionary<char, FractionWords> Fractions = new Dictionary<char, FractionWords>
    {
        ['½'] = new(new("نصف", "one half"), new("ونصف", "and a half")),
        ['¼'] = new(new("رُبع", "one quarter"), new("ورُبع", "and a quarter")),
        ['¾'] = new(new("ثلاثة أرباع", "three quarters"), new("وثلاثة أرباع", "and three quarters")),
        ['⅓'] = new(new("ثُلث", "one third"), new("وثُلث", "and a third")),
        ['⅔'] = new(new("ثُلثان", "two thirds"), new("وثُلثان", "and two thirds")),
        ['⅕'] = new(new("خُمس", "one fifth"), new("وخُمس", "and a fifth")),
        ['⅛'] = new(new("ثُمن", "one eighth"), new("وثُمن", "and an eighth")),
    };

    /// <summary>Math symbols, with whether an operand (rather than an operator) ends at the symbol.</summary>
    public readonly record struct MathSymbol(Bilingual Words, bool OperandAfter);

    /// <summary>Single- and multi-character math tokens. Minus, prime, factorial, power and subscript are handled in code.</summary>
    public static readonly IReadOnlyDictionary<string, MathSymbol> MathSymbols = new Dictionary<string, MathSymbol>
    {
        ["+"] = new(new("زائد", "plus"), false),
        ["＋"] = new(new("زائد", "plus"), false),
        ["×"] = new(new("ضرب", "times"), false),
        ["*"] = new(new("ضرب", "times"), false),
        ["·"] = new(new("ضرب", "times"), false),
        ["⋅"] = new(new("ضرب", "times"), false),
        ["∙"] = new(new("ضرب", "times"), false),
        ["✕"] = new(new("ضرب", "times"), false),
        ["÷"] = new(new("على", "divided by"), false),
        ["/"] = new(new("على", "divided by"), false),
        ["∕"] = new(new("على", "divided by"), false),
        ["⁄"] = new(new("على", "divided by"), false),
        ["="] = new(new("يساوي", "equals"), false),
        ["=="] = new(new("يساوي", "equals"), false),
        ["≠"] = new(new("لا يساوي", "not equal to"), false),
        ["!="] = new(new("لا يساوي", "not equal to"), false),
        ["<"] = new(new("أصغر من", "less than"), false),
        [">"] = new(new("أكبر من", "greater than"), false),
        ["≤"] = new(new("أصغر من أو يساوي", "less than or equal to"), false),
        ["<="] = new(new("أصغر من أو يساوي", "less than or equal to"), false),
        ["≦"] = new(new("أصغر من أو يساوي", "less than or equal to"), false),
        ["≥"] = new(new("أكبر من أو يساوي", "greater than or equal to"), false),
        [">="] = new(new("أكبر من أو يساوي", "greater than or equal to"), false),
        ["≧"] = new(new("أكبر من أو يساوي", "greater than or equal to"), false),
        ["≈"] = new(new("يساوي تقريبا", "approximately equal to"), false),
        ["≃"] = new(new("يساوي تقريبا", "approximately equal to"), false),
        ["≡"] = new(new("يطابق", "identical to"), false),
        ["∝"] = new(new("يتناسب مع", "proportional to"), false),
        [":="] = new(new("يعرف بأنه", "is defined as"), false),
        ["±"] = new(new("زائد أو ناقص", "plus or minus"), false),
        ["+-"] = new(new("زائد أو ناقص", "plus or minus"), false),
        ["+/-"] = new(new("زائد أو ناقص", "plus or minus"), false),
        ["∓"] = new(new("ناقص أو زائد", "minus or plus"), false),
        ["√"] = new(new("الجذر التربيعي لـ", "square root of"), false),
        ["∛"] = new(new("الجذر التكعيبي لـ", "cube root of"), false),
        ["∜"] = new(new("الجذر الرابع لـ", "fourth root of"), false),
        ["∑"] = new(new("مجموع", "sum"), false),
        ["∏"] = new(new("جداء", "product"), false),
        ["∫"] = new(new("تكامل", "integral"), false),
        ["∬"] = new(new("تكامل مزدوج", "double integral"), false),
        ["∮"] = new(new("تكامل مغلق", "contour integral"), false),
        ["∂"] = new(new("تفاضل جزئي", "partial"), false),
        ["∇"] = new(new("نابلا", "nabla"), false),
        ["∞"] = new(new("ما لا نهاية", "infinity"), true),
        ["∅"] = new(new("المجموعة الخالية", "the empty set"), true),
        ["∈"] = new(new("ينتمي إلى", "element of"), false),
        ["∉"] = new(new("لا ينتمي إلى", "not an element of"), false),
        ["⊂"] = new(new("مجموعة جزئية من", "subset of"), false),
        ["⊆"] = new(new("مجموعة جزئية من أو تساوي", "subset of or equal to"), false),
        ["⊃"] = new(new("تحوي", "superset of"), false),
        ["∪"] = new(new("اتحاد", "union"), false),
        ["∩"] = new(new("تقاطع", "intersection"), false),
        ["∀"] = new(new("لكل", "for all"), false),
        ["∃"] = new(new("يوجد", "there exists"), false),
        ["∴"] = new(new("إذن", "therefore"), false),
        ["∵"] = new(new("لأن", "because"), false),
        ["→"] = new(new("يؤول إلى", "goes to"), false),
        ["->"] = new(new("يؤول إلى", "goes to"), false),
        ["⇒"] = new(new("يؤدي إلى", "implies"), false),
        ["=>"] = new(new("يؤدي إلى", "implies"), false),
        ["⇔"] = new(new("إذا وفقط إذا", "if and only if"), false),
        ["<=>"] = new(new("إذا وفقط إذا", "if and only if"), false),
        ["∠"] = new(new("زاوية", "angle"), false),
        ["⊥"] = new(new("عمودي على", "perpendicular to"), false),
        ["∥"] = new(new("يوازي", "parallel to"), false),
        ["°"] = new(new("درجة", "degrees"), true),
        ["%"] = new(new("بالمئة", "percent"), true),
        ["٪"] = new(new("بالمئة", "percent"), true),
        ["‰"] = new(new("في الألف", "per mille"), true),
        ["("] = new(new("فتح قوس", "open bracket"), false),
        [")"] = new(new("إغلاق قوس", "close bracket"), true),
        ["["] = new(new("فتح قوس مربع", "open square bracket"), false),
        ["]"] = new(new("إغلاق قوس مربع", "close square bracket"), true),
        ["{"] = new(new("فتح قوس معقوف", "open curly bracket"), false),
        ["}"] = new(new("إغلاق قوس معقوف", "close curly bracket"), true),
        [","] = new(Comma, false),
        ["،"] = new(Comma, false),
    };

    /// <summary>Longest multi-character math token, for greedy matching.</summary>
    public const int MaxMathSymbolLength = 3;

    /// <summary>Greek letters common in school math and science.</summary>
    public static readonly IReadOnlyDictionary<char, Bilingual> GreekLetters = new Dictionary<char, Bilingual>
    {
        ['α'] = new("ألفا", "alpha"),
        ['β'] = new("بيتا", "beta"),
        ['γ'] = new("غاما", "gamma"),
        ['δ'] = new("دلتا", "delta"),
        ['Δ'] = new("دلتا", "delta"),
        ['ε'] = new("إبسيلون", "epsilon"),
        ['ζ'] = new("زيتا", "zeta"),
        ['η'] = new("إيتا", "eta"),
        ['θ'] = new("ثيتا", "theta"),
        ['λ'] = new("لامدا", "lambda"),
        ['μ'] = new("ميو", "mu"),
        ['ν'] = new("نيو", "nu"),
        ['ξ'] = new("كساي", "xi"),
        ['π'] = new("باي", "pi"),
        ['ρ'] = new("رو", "rho"),
        ['σ'] = new("سيغما", "sigma"),
        ['Σ'] = new("مجموع", "sum"),
        ['τ'] = new("تاو", "tau"),
        ['φ'] = new("فاي", "phi"),
        ['ϕ'] = new("فاي", "phi"),
        ['χ'] = new("كاي", "chi"),
        ['ψ'] = new("بساي", "psi"),
        ['ω'] = new("أوميغا", "omega"),
        ['Ω'] = new("أوميغا", "omega"),
    };

    /// <summary>Function names read as words in math mode.</summary>
    public static readonly IReadOnlyDictionary<string, Bilingual> MathFunctions = new Dictionary<string, Bilingual>(StringComparer.OrdinalIgnoreCase)
    {
        ["sin"] = new("جا", "sine"),
        ["cos"] = new("جتا", "cosine"),
        ["tan"] = new("ظا", "tangent"),
        ["cot"] = new("ظتا", "cotangent"),
        ["sec"] = new("قا", "secant"),
        ["csc"] = new("قتا", "cosecant"),
        ["log"] = new("لوغاريتم", "log"),
        ["ln"] = new("اللوغاريتم الطبيعي", "natural log"),
        ["exp"] = new("الدالة الأسية", "exponential"),
        ["lim"] = new("نهاية", "limit"),
        ["max"] = new("القيمة العظمى", "max"),
        ["min"] = new("القيمة الصغرى", "min"),
        ["sqrt"] = new("الجذر التربيعي لـ", "square root of"),
        ["det"] = new("محدد", "determinant"),
        ["mod"] = new("باقي القسمة على", "mod"),
    };

    public static readonly IReadOnlyDictionary<char, char> Superscripts = new Dictionary<char, char>
    {
        ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4',
        ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9',
        ['⁺'] = '+', ['⁻'] = '-', ['⁼'] = '=', ['⁽'] = '(', ['⁾'] = ')',
        ['ⁿ'] = 'n', ['ⁱ'] = 'i', ['ˣ'] = 'x', ['ʸ'] = 'y',
    };

    public static readonly IReadOnlyDictionary<char, char> Subscripts = new Dictionary<char, char>
    {
        ['₀'] = '0', ['₁'] = '1', ['₂'] = '2', ['₃'] = '3', ['₄'] = '4',
        ['₅'] = '5', ['₆'] = '6', ['₇'] = '7', ['₈'] = '8', ['₉'] = '9',
        ['₊'] = '+', ['₋'] = '-', ['₌'] = '=', ['₍'] = '(', ['₎'] = ')',
        ['ₐ'] = 'a', ['ₑ'] = 'e', ['ₒ'] = 'o', ['ₓ'] = 'x', ['ₕ'] = 'h', ['ₖ'] = 'k', ['ₗ'] = 'l',
        ['ₘ'] = 'm', ['ₙ'] = 'n', ['ₚ'] = 'p', ['ₛ'] = 's', ['ₜ'] = 't', ['ᵢ'] = 'i', ['ⱼ'] = 'j',
    };

    /// <summary>Digit names 0–9.</summary>
    public static readonly IReadOnlyList<Bilingual> DigitNames =
    [
        new("صفر", "zero"), new("واحد", "one"), new("اثنان", "two"), new("ثلاثة", "three"), new("أربعة", "four"),
        new("خمسة", "five"), new("ستة", "six"), new("سبعة", "seven"), new("ثمانية", "eight"), new("تسعة", "nine"),
    ];

    /// <summary>Arabic letter names for spelling.</summary>
    public static readonly IReadOnlyDictionary<char, Bilingual> ArabicLetterNames = new Dictionary<char, Bilingual>
    {
        ['ا'] = new("ألف", "alef"),
        ['ب'] = new("باء", "beh"),
        ['ت'] = new("تاء", "teh"),
        ['ث'] = new("ثاء", "theh"),
        ['ج'] = new("جيم", "jeem"),
        ['ح'] = new("حاء", "hah"),
        ['خ'] = new("خاء", "khah"),
        ['د'] = new("دال", "dal"),
        ['ذ'] = new("ذال", "thal"),
        ['ر'] = new("راء", "reh"),
        ['ز'] = new("زاي", "zain"),
        ['س'] = new("سين", "seen"),
        ['ش'] = new("شين", "sheen"),
        ['ص'] = new("صاد", "sad"),
        ['ض'] = new("ضاد", "dad"),
        ['ط'] = new("طاء", "tah"),
        ['ظ'] = new("ظاء", "zah"),
        ['ع'] = new("عين", "ain"),
        ['غ'] = new("غين", "ghain"),
        ['ف'] = new("فاء", "feh"),
        ['ق'] = new("قاف", "qaf"),
        ['ك'] = new("كاف", "kaf"),
        ['ل'] = new("لام", "lam"),
        ['م'] = new("ميم", "meem"),
        ['ن'] = new("نون", "noon"),
        ['ه'] = new("هاء", "heh"),
        ['و'] = new("واو", "waw"),
        ['ي'] = new("ياء", "yeh"),
        ['ة'] = new("تاء مربوطة", "teh marbuta"),
        ['ى'] = new("ألف مقصورة", "alef maksura"),
        ['ء'] = new("همزة", "hamza"),
        ['أ'] = new("ألف همزة فوق", "alef with hamza above"),
        ['إ'] = new("ألف همزة تحت", "alef with hamza below"),
        ['آ'] = new("ألف مد", "alef with madda"),
        ['ؤ'] = new("واو همزة", "waw with hamza"),
        ['ئ'] = new("ياء همزة", "yeh with hamza"),
        ['ٱ'] = new("ألف وصل", "alef wasla"),
        ['ـ'] = new("تطويل", "tatweel"),
        ['پ'] = new("باء بثلاث نقاط", "peh"),
        ['چ'] = new("جيم بثلاث نقاط", "tcheh"),
        ['ژ'] = new("زاي بثلاث نقاط", "jeh"),
        ['ڤ'] = new("فاء بثلاث نقاط", "veh"),
        ['گ'] = new("كاف فارسية", "gaf"),
        ['ک'] = new("كاف فارسية", "keheh"),
        ['ی'] = new("ياء فارسية", "farsi yeh"),
    };

    /// <summary>Arabic diacritics (tashkeel) for spelling.</summary>
    public static readonly IReadOnlyDictionary<char, Bilingual> TashkeelNames = new Dictionary<char, Bilingual>
    {
        ['\u064B'] = new("تنوين فتح", "fathatan"),
        ['\u064C'] = new("تنوين ضم", "dammatan"),
        ['\u064D'] = new("تنوين كسر", "kasratan"),
        ['\u064E'] = new("فتحة", "fatha"),
        ['\u064F'] = new("ضمة", "damma"),
        ['\u0650'] = new("كسرة", "kasra"),
        ['\u0651'] = new("شدة", "shadda"),
        ['\u0652'] = new("سكون", "sukun"),
        ['\u0653'] = new("مدة", "maddah"),
        ['\u0654'] = new("همزة فوق", "hamza above"),
        ['\u0655'] = new("همزة تحت", "hamza below"),
        ['\u0670'] = new("ألف خنجرية", "dagger alef"),
    };

    /// <summary>Punctuation and symbol names for spelling.</summary>
    public static readonly IReadOnlyDictionary<char, Bilingual> SpellSymbols = new Dictionary<char, Bilingual>
    {
        [' '] = new("مسافة", "space"),
        ['\u00A0'] = new("مسافة", "space"),
        ['\t'] = new("مسافة جدولة", "tab"),
        ['\n'] = new("سطر جديد", "new line"),
        ['.'] = new("نقطة", "dot"),
        [','] = new("فاصلة", "comma"),
        ['،'] = new("فاصلة", "Arabic comma"),
        [';'] = new("فاصلة منقوطة", "semicolon"),
        ['؛'] = new("فاصلة منقوطة", "Arabic semicolon"),
        [':'] = new("نقطتان", "colon"),
        ['?'] = new("علامة استفهام", "question mark"),
        ['؟'] = new("علامة استفهام", "Arabic question mark"),
        ['!'] = new("علامة تعجب", "exclamation mark"),
        ['-'] = new("شرطة", "dash"),
        ['–'] = new("شرطة", "en dash"),
        ['—'] = new("شرطة طويلة", "em dash"),
        ['_'] = new("شرطة سفلية", "underscore"),
        ['@'] = new("علامة آت", "at sign"),
        ['#'] = new("علامة الشباك", "hash"),
        ['$'] = new("علامة الدولار", "dollar sign"),
        ['%'] = new("علامة النسبة المئوية", "percent sign"),
        ['٪'] = new("علامة النسبة المئوية", "percent sign"),
        ['&'] = new("علامة و", "ampersand"),
        ['*'] = new("نجمة", "star"),
        ['+'] = new("زائد", "plus"),
        ['='] = new("يساوي", "equals"),
        ['/'] = new("شرطة مائلة", "slash"),
        ['\\'] = new("شرطة مائلة عكسية", "backslash"),
        ['('] = new("فتح قوس", "open bracket"),
        [')'] = new("إغلاق قوس", "close bracket"),
        ['['] = new("فتح قوس مربع", "open square bracket"),
        [']'] = new("إغلاق قوس مربع", "close square bracket"),
        ['{'] = new("فتح قوس معقوف", "open curly bracket"),
        ['}'] = new("إغلاق قوس معقوف", "close curly bracket"),
        ['<'] = new("أصغر من", "less than"),
        ['>'] = new("أكبر من", "greater than"),
        ['\''] = new("فاصلة علوية", "apostrophe"),
        ['"'] = new("علامة تنصيص", "quote"),
        ['«'] = new("علامة تنصيص", "left quote"),
        ['»'] = new("علامة تنصيص", "right quote"),
        ['`'] = new("علامة نبر", "backtick"),
        ['~'] = new("تيلدا", "tilde"),
        ['^'] = new("علامة الأس", "caret"),
        ['|'] = new("خط عمودي", "vertical bar"),
        ['€'] = new("يورو", "euro"),
        ['£'] = new("جنيه إسترليني", "pound"),
        ['¥'] = new("ين", "yen"),
        ['°'] = new("درجة", "degree"),
        ['×'] = new("ضرب", "times"),
        ['÷'] = new("قسمة", "divided by"),
        ['…'] = new("ثلاث نقاط", "ellipsis"),
        ['•'] = new("نقطة تعداد", "bullet"),
        ['©'] = new("حقوق النشر", "copyright"),
        ['®'] = new("علامة مسجلة", "registered"),
        ['™'] = new("علامة تجارية", "trademark"),
        ['§'] = new("فقرة", "section"),
        ['٫'] = new("فاصلة عشرية", "decimal separator"),
        ['٬'] = new("فاصل الآلاف", "thousands separator"),
    };
}
