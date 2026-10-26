using System.Collections.Frozen;

namespace Mubsir.Core.Brain;

public static class AppRecipes
{
    public const string WhatsApp = "WhatsApp";
    public const string Gmail = "Gmail";
    public const string Outlook = "Outlook";
    public const string YouTube = "YouTube";
    public const string Word = "Microsoft Word";
    public const string FileExplorer = "File Explorer";
    public const string Browser = "Chrome and Edge";

    public static IReadOnlyList<string> Order { get; } = [Browser, WhatsApp, Gmail, Outlook, YouTube, Word, FileExplorer];

    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Browser] =
            "Google Chrome and Microsoft Edge: Ctrl+L or Alt+D focuses the address bar. Ctrl+T new tab, Ctrl+N new window, "
            + "Ctrl+W closes the tab, Ctrl+Tab next tab, Ctrl+Shift+T reopens a closed tab, Alt+Left back, Alt+Right forward, "
            + "F5 reload, Ctrl+F find on page. To open the same page in a new window: Alt+D, then Shift+Enter; in a new tab: Alt+D, then Alt+Enter. "
            + "With NVDA in browse mode single letters are NVDA navigation keys, so prefer invoke and set_text over typing letters. "
            + "Downloads (Ctrl+J) may be read but never opened.",

        [WhatsApp] =
            "WhatsApp Desktop and WhatsApp Web: the chat list is on the left, the open chat on the right with the message box at the bottom. "
            + "Search chats with the search box at the top of the chat list (Ctrl+F in the Desktop app). Unread chats show an unread count in their name; "
            + "the Unread filter above the list shows only those. Open a chat by invoking it, then read the last messages with read_screen. "
            + "A voice note has a Play button inside the message; invoke it to play. To reply, set_text in the message box, then invoke the Send button "
            + "or press Enter; the app asks the user before sending. New chat: Ctrl+N in the Desktop app.",

        [Gmail] =
            "Gmail in the browser: the search box is named \"Search mail\". The inbox is a list of rows; invoke a row to open the email. "
            + "Compose with the Compose button; fields are To, Subject and Message Body; send with the Send button or Ctrl+Enter (the app asks first). "
            + "Reply with the Reply button under the email. Single-letter shortcuts (c compose, r reply, e archive) work only if enabled in settings, so prefer buttons.",

        [Outlook] =
            "Outlook on the web and the Outlook app: New mail button (or N), Reply (R), Reply all (Shift+R), Forward (Shift+F), "
            + "search with Alt+Q. In a new message, fill To, Subject and the message body, then the Send button or Ctrl+Enter sends (the app asks first). "
            + "Folders such as Inbox, Sent Items and Deleted Items are in the left pane.",

        [YouTube] =
            "YouTube: search with browse_search using engine youtube, or set_text in the box named \"Search\" then press Enter. Invoke a video title to open it. "
            + "Player keys k (play or pause), j and l (back and forward 10 seconds), m (mute), f (full screen) and Shift+N (next video) work only when "
            + "focus is on the player and NVDA is in focus mode, not in browse mode; the media tool works in any mode and is preferred.",

        [Word] =
            "Microsoft Word: read the document with read_document or read_screen with scope document. Ctrl+Home goes to the start, Ctrl+End to the end, "
            + "Ctrl+F opens the navigation pane for finding text, Ctrl+S saves, Ctrl+O opens a file, Ctrl+Z undoes. "
            + "The status bar shows the page number and word count.",

        [FileExplorer] =
            "File Explorer: prefer the file tools (list_folder, find_files, move_file, copy_file, rename_file, recycle) over the window. "
            + "In the window, Alt+D or Ctrl+L focuses the address bar, Alt+Up goes to the parent folder, Alt+Left goes back, F2 renames, "
            + "Ctrl+Shift+N makes a new folder. Never open program or script files (.exe, .msi, .bat, .ps1, .lnk and similar).",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly (string Alias, string Key)[] Aliases =
    [
        ("whatsapp", WhatsApp), ("واتساب", WhatsApp), ("واتس", WhatsApp), ("الواتس", WhatsApp), ("الواتساب", WhatsApp),
        ("gmail", Gmail), ("جيميل", Gmail), ("google mail", Gmail),
        ("outlook", Outlook), ("hotmail", Outlook), ("olk", Outlook), ("أوتلوك", Outlook), ("اوتلوك", Outlook),
        ("youtube", YouTube), ("يوتيوب", YouTube), ("اليوتيوب", YouTube),
        ("word", Word), ("winword", Word), ("وورد", Word), ("الوورد", Word),
        ("explorer", FileExplorer), ("مستكشف الملفات", FileExplorer), ("الملفات", FileExplorer),
        ("chrome", Browser), ("edge", Browser), ("msedge", Browser), ("browser", Browser), ("كروم", Browser),
        ("ايدج", Browser), ("إيدج", Browser), ("المتصفح", Browser), ("متصفح", Browser),
    ];

    public static IReadOnlyList<string> Match(IEnumerable<string> appNames)
    {
        ArgumentNullException.ThrowIfNull(appNames);
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in appNames)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (All.ContainsKey(raw.Trim()))
            {
                wanted.Add(raw.Trim());
            }

            var words = Words(raw);
            foreach (var (alias, key) in Aliases)
            {
                if (words.Contains(Words(alias), StringComparison.Ordinal))
                {
                    wanted.Add(key);
                }
            }
        }

        return Order.Where(wanted.Contains).ToList();
    }

    private static string Words(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 2).Append(' ');
        foreach (var c in s.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (sb[^1] != ' ')
            {
                sb.Append(' ');
            }
        }

        if (sb[^1] != ' ')
        {
            sb.Append(' ');
        }

        return sb.ToString();
    }
}
