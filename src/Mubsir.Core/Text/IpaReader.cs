using System.Text;
using Mubsir.Core.Common;

namespace Mubsir.Core.Text;

/// <summary>
/// Says phonetic writing such as /ʃɪp/ sound by sound. A fixed list, so it never guesses and costs nothing.
/// Only a word holding a real phonetic symbol changes, so ordinary text is left alone.
/// </summary>
public static class IpaReader
{
    private static readonly Dictionary<char, Bilingual> Sounds = new()
    {
        ['ə'] = new("شوا", "schwa"),
        ['ɚ'] = new("شوا مع راء", "r-coloured schwa"),
        ['æ'] = new("فتحة أمامية", "a as in cat"),
        ['ɑ'] = new("ألف خلفية", "a as in father"),
        ['ɒ'] = new("واو مفتوحة قصيرة", "o as in hot"),
        ['ɔ'] = new("واو مفتوحة", "aw as in law"),
        ['ʊ'] = new("ضمة قصيرة", "oo as in book"),
        ['ʌ'] = new("فتحة قصيرة", "u as in cup"),
        ['ɪ'] = new("كسرة قصيرة", "i as in sit"),
        ['ɛ'] = new("إمالة مفتوحة", "e as in bed"),
        ['ɜ'] = new("صائت وسطي", "er as in bird"),
        ['ɝ'] = new("صائت وسطي مع راء", "er with r colour"),
        ['ʃ'] = new("صوت الشين", "the sh sound"),
        ['ʒ'] = new("صوت الجيم الشامية", "the zh sound"),
        ['θ'] = new("صوت الثاء", "th as in think"),
        ['ð'] = new("صوت الذال", "th as in this"),
        ['ŋ'] = new("نون خلفية", "the ng sound"),
        ['ɹ'] = new("راء إنجليزية", "the English r"),
        ['ɾ'] = new("راء خفيفة", "a tapped r"),
        ['ʁ'] = new("راء لهوية", "a uvular r"),
        ['ʔ'] = new("همزة", "glottal stop"),
        ['ħ'] = new("صوت الحاء", "the Arabic haa"),
        ['ʕ'] = new("صوت العين", "the Arabic ayn"),
        ['ɣ'] = new("صوت الغين", "the Arabic ghayn"),
        ['χ'] = new("صوت الخاء", "the Arabic khaa"),
        ['ʈ'] = new("تاء ارتدادية", "retroflex t"),
        ['ɖ'] = new("دال ارتدادية", "retroflex d"),
        ['ɳ'] = new("نون ارتدادية", "retroflex n"),
        ['ɭ'] = new("لام ارتدادية", "retroflex l"),
        ['ɲ'] = new("نون حنكية", "palatal n"),
        ['ʎ'] = new("لام حنكية", "palatal l"),
        ['ɸ'] = new("فاء شفوية", "bilabial f"),
        ['β'] = new("باء احتكاكية", "bilabial v"),
        ['ç'] = new("ياء مهموسة", "palatal h"),
        ['ʝ'] = new("ياء احتكاكية", "voiced palatal fricative"),
        ['ʋ'] = new("واو أسنانية", "labiodental approximant"),
        ['ɰ'] = new("غين تقاربية", "velar approximant"),
        ['ʂ'] = new("شين ارتدادية", "retroflex sh"),
        ['ʐ'] = new("زاي ارتدادية", "retroflex zh"),
        ['ɕ'] = new("شين حنكية", "palatal sh"),
        ['ʑ'] = new("زاي حنكية", "palatal zh"),
        ['ˈ'] = new("نبر رئيسي", "main stress"),
        ['ˌ'] = new("نبر ثانوي", "second stress"),
        ['ː'] = new("مد", "long"),
    };

    /// <summary>Greek letters also mean maths, so these alone don't make a word phonetic.</summary>
    private static readonly HashSet<char> AlsoGreek = ['θ', 'β', 'χ'];

    public static string Read(string text, Lang lang)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Any(IsMark))
        {
            return text;
        }

        string[] words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Any(IsMark))
            {
                words[i] = Word(words[i], lang);
            }
        }

        return string.Join(' ', words);
    }

    private static bool IsMark(char c) => Sounds.ContainsKey(c) && !AlsoGreek.Contains(c);

    private static string Word(string word, Lang lang)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        foreach (char c in word)
        {
            if (Sounds.TryGetValue(c, out Bilingual sound))
            {
                parts.Add(sound.For(lang));
            }
            else if (char.IsLetter(c))
            {
                parts.Add(c.ToString());
            }
        }

        sb.Append(string.Join(lang == Lang.Arabic ? "، " : ", ", parts));
        if (char.IsPunctuation(word[^1]) && word[^1] is '.' or ',' or '،' or '؟' or '?')
        {
            sb.Append(word[^1]);
        }

        return sb.ToString();
    }
}
