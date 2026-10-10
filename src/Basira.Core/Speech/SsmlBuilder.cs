using System.Text;
using Basira.Core.Common;
using Basira.Core.Text;

namespace Basira.Core.Speech;

/// <summary>
/// Which SSML element marks a language change. Which one NVDA maps to a voice switch is verified on Windows,
/// so both are supported.
/// </summary>
public enum LangTagStyle
{
    /// <summary><c>&lt;voice xml:lang="ar"&gt;</c></summary>
    VoiceElement,

    /// <summary><c>&lt;lang xml:lang="ar"&gt;</c></summary>
    LangElement,
}

/// <summary>Builds the SSML for one sentence, sent to NVDA's speakSsml.</summary>
public static class SsmlBuilder
{
    /// <summary>
    /// Builds <c>&lt;speak&gt;&lt;mark name="s12"/&gt;&lt;voice xml:lang="ar"&gt;…&lt;/voice&gt;…&lt;/speak&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Consecutive runs of one language share an element; spelled-out runs become
    /// <c>&lt;say-as interpret-as="characters"&gt;</c> inside it. Whitespace collapses to single spaces, the ends are
    /// trimmed, and characters that are not valid in XML are dropped. No XML declaration is written.
    /// </remarks>
    public static string Build(IReadOnlyList<TextRun> runs, string? markName, LangTagStyle style = LangTagStyle.VoiceElement)
    {
        ArgumentNullException.ThrowIfNull(runs);
        string element = style == LangTagStyle.LangElement ? "lang" : "voice";
        var parts = Clean(runs);

        var sb = new StringBuilder(64);
        sb.Append("<speak>");
        if (!string.IsNullOrEmpty(markName))
        {
            sb.Append("<mark name=\"");
            AppendEscaped(sb, markName, attribute: true);
            sb.Append("\"/>");
        }

        int i = 0;
        while (i < parts.Count)
        {
            Lang lang = parts[i].Lang;
            int end = i;
            bool hasContent = false;
            while (end < parts.Count && parts[end].Lang == lang)
            {
                hasContent |= !string.IsNullOrWhiteSpace(parts[end].Text);
                end++;
            }

            if (!hasContent)
            {
                sb.Append(' '); // a lone space between two languages needs no voice switch
                i = end;
                continue;
            }

            sb.Append('<').Append(element).Append(" xml:lang=\"").Append(lang.Tag()).Append("\">");
            for (int k = i; k < end; k++)
            {
                if (parts[k].SpellOut && !string.IsNullOrWhiteSpace(parts[k].Text))
                {
                    sb.Append("<say-as interpret-as=\"characters\">");
                    AppendEscaped(sb, parts[k].Text, attribute: false);
                    sb.Append("</say-as>");
                }
                else
                {
                    AppendEscaped(sb, parts[k].Text, attribute: false);
                }
            }

            sb.Append("</").Append(element).Append('>');
            i = end;
        }

        sb.Append("</speak>");
        return sb.ToString();
    }

    /// <summary>Collapses whitespace across run boundaries, trims the ends and drops invalid XML characters.</summary>
    private static List<TextRun> Clean(IReadOnlyList<TextRun> runs)
    {
        var parts = new List<TextRun>(runs.Count);
        bool lastWasSpace = true; // trims leading whitespace
        foreach (var run in runs)
        {
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            var sb = new StringBuilder(run.Text.Length);
            string text = run.Text;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                    {
                        sb.Append(' ');
                        lastWasSpace = true;
                    }

                    continue;
                }

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    sb.Append(c).Append(text[i + 1]);
                    i++;
                    lastWasSpace = false;
                    continue;
                }

                if (IsValidXmlChar(c))
                {
                    sb.Append(c);
                    lastWasSpace = false;
                }
            }

            if (sb.Length > 0)
            {
                parts.Add(run with { Text = sb.ToString() });
            }
        }

        // Trim trailing whitespace.
        while (parts.Count > 0)
        {
            string last = parts[^1].Text.TrimEnd();
            if (last.Length > 0)
            {
                parts[^1] = parts[^1] with { Text = last };
                break;
            }

            parts.RemoveAt(parts.Count - 1);
        }

        return parts;
    }

    private static bool IsValidXmlChar(char c) =>
        c is '\t' or '\n' or '\r' or (>= ' ' and <= '\uD7FF') or (>= '\uE000' and <= '\uFFFD');

    private static void AppendEscaped(StringBuilder sb, string text, bool attribute)
    {
        foreach (char c in text)
        {
            switch (c)
            {
                case '&':
                    sb.Append("&amp;");
                    break;
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                case '"' when attribute:
                    sb.Append("&quot;");
                    break;
                case '\'' when attribute:
                    sb.Append("&apos;");
                    break;
                default:
                    if (!attribute || IsValidXmlChar(c) || char.IsSurrogate(c))
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }
    }
}
