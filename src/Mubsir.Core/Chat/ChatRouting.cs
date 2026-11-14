using Mubsir.Core.Text;

namespace Mubsir.Core.Chat;

public enum ChatSite
{
    None,
    ChatGpt,
    Gemini,
}

public sealed record ChatRequest(ChatSite Site, string Question, bool AskedByName);

/// <summary>
/// Sends plain questions to the AI chat the person already pays for, in their browser, so they cost nothing on the API.
/// Anything about the screen, a file or an action stays with the brain.
/// </summary>
public static class ChatRouting
{
    private const int MinQuestion = 2;

    private static readonly (string Words, ChatSite Site)[] ByName =
    [
        ("اسأل شات جي بي تي", ChatSite.ChatGpt), ("اسال شات جي بي تي", ChatSite.ChatGpt), ("اسأل تشات جي بي تي", ChatSite.ChatGpt), ("ask chatgpt", ChatSite.ChatGpt), ("ask chat gpt", ChatSite.ChatGpt),
        ("اسأل جيميني", ChatSite.Gemini), ("اسال جيميني", ChatSite.Gemini), ("اسأل جمناي", ChatSite.Gemini), ("ask gemini", ChatSite.Gemini),
    ];

    private static readonly string[] QuestionStarts =
    [
        "يعني ايه", "يعني إيه", "ايه معنى", "إيه معنى", "ما معنى", "ما هو", "ما هي", "ايه هو", "إيه هو", "ايه هي", "إيه هي", "مين هو", "مين هي", "من هو", "من هي", "ازاي", "إزاي", "ليه", "لماذا", "كيف", "فين", "اين", "أين", "كام", "كم",
        "what does", "what is", "what are", "what's", "who is", "who was", "how do", "how does", "how many", "how much", "why", "when did", "when was", "where is", "define", "explain",
    ];

    /// <summary>These need what's on this PC, so a chat somewhere else couldn't answer them.</summary>
    private static readonly string[] AboutThisPc =
    [
        "الشاشه", "الصفحه", "الملف", "الكتاب", "الفولدر", "الجهاز", "الكمبيوتر", "البطاريه", "الصوت", "الرساله", "الرسايل", "واتساب", "الساعه", "النهارده",
        "screen", "page", "file", "book", "folder", "computer", "battery", "volume", "message", "whatsapp", "time is it", " this ", " my ", " here",
    ];

    public static ChatRequest? Pick(string? said, ChatSite preferred)
    {
        string text = (said ?? string.Empty).Trim();
        foreach ((string words, ChatSite site) in ByName)
        {
            if (text.StartsWith(words, StringComparison.OrdinalIgnoreCase))
            {
                string question = text[words.Length..].Trim(' ', '،', ',', ':', '.');
                return question.Length >= MinQuestion ? new ChatRequest(site, question, AskedByName: true) : null;
            }
        }

        return preferred != ChatSite.None && IsPlainQuestion(text) ? new ChatRequest(preferred, text, AskedByName: false) : null;
    }

    public static bool IsPlainQuestion(string? said)
    {
        string text = (said ?? string.Empty).Trim();
        if (text.Length < MinQuestion || !QuestionStarts.Any(q => text.StartsWith(q, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string padded = " " + ArabicFolding.Fold(text) + " ";
        return !AboutThisPc.Any(w => padded.Contains(ArabicFolding.Fold(w), StringComparison.OrdinalIgnoreCase) || padded.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    public static string Url(ChatSite site) => site == ChatSite.Gemini ? "https://gemini.google.com/app" : "https://chatgpt.com/";

    public static string Domain(ChatSite site) => site == ChatSite.Gemini ? "gemini.google" : "chatgpt.com";
}

/// <summary>Finds the answer in the text of the chat page.</summary>
public static class ChatAnswer
{
    private const int MinAnswer = 1;

    private static readonly string[] EndMarks =
    [
        "ChatGPT can make mistakes", "ChatGPT may", "Gemini can make mistakes", "Gemini may", "Gemini is AI", "قد يخطئ", "يمكن أن يرتكب", "Ask anything", "Ask Gemini", "Message ChatGPT", "اسأل Gemini",
    ];

    private static readonly HashSet<string> PageWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Copy", "Good response", "Bad response", "Read aloud", "Share", "More actions", "Edit message", "Regenerate", "ChatGPT said:", "You said:", "Show thinking", "Gemini said", "نسخ", "مشاركة", "Sources", "Tools",
    };

    private static readonly string[] SignInWords = ["log in", "sign in", "sign up", "تسجيل الدخول", "سجل الدخول"];

    public static string? Extract(string? pageText, string question)
    {
        ArgumentNullException.ThrowIfNull(question);
        if (string.IsNullOrEmpty(pageText))
        {
            return null;
        }

        int at = pageText.LastIndexOf(question, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        string after = pageText[(at + question.Length)..];
        foreach (string mark in EndMarks)
        {
            int end = after.IndexOf(mark, StringComparison.OrdinalIgnoreCase);
            if (end >= 0)
            {
                after = after[..end];
            }
        }

        string[] lines = after.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !PageWords.Contains(l)).ToArray();
        string answer = string.Join('\n', lines).Trim();
        return answer.Length >= MinAnswer ? answer : null;
    }

    public static bool WantsSignIn(string? pageText) =>
        pageText is not null && SignInWords.Any(w => pageText.Contains(w, StringComparison.OrdinalIgnoreCase));
}
