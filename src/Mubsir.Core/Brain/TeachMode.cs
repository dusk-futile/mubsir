using Mubsir.Core.Common;
using Mubsir.Core.Text;

namespace Mubsir.Core.Brain;

/// <summary>
/// Learning: Mubsir shows the way and the learner does it. It reads the screen and points, and it can't click,
/// type or change anything. The rule is enforced by the policy, not just asked of the AI.
/// </summary>
public static class TeachMode
{
    private static readonly string[] Starts =
    [
        "علمني", "علميني", "عايز اتعلم", "عايزه اتعلم", "نفسي اتعلم", "وريني ازاي", "وريني إزاي", "اشرحلي ازاي", "ازاي اعمل",
        "teach me", "show me how", "i want to learn", "help me learn", "how do i",
    ];

    private static readonly string[] Ends = ["خلاص كفايه تعليم", "بطل تعليم", "اعملها انت", "نفذها انت", "stop teaching", "do it for me", "you do it"];

    /// <summary>Tools that only look, point or talk.</summary>
    public static IReadOnlySet<string> AllowedTools { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ToolNames.ReadScreen, ToolNames.DescribeScreen, ToolNames.OcrWindow, ToolNames.ListWindows, ToolNames.SystemStatus,
        ToolNames.PointAt, ToolNames.AskUser, ToolNames.Remember, ToolNames.ReadDocument, ToolNames.FindBook,
    };

    public static IReadOnlyList<ToolSchema> Tools { get; } = ToolCatalog.All.Where(t => AllowedTools.Contains(t.Name)).ToArray();

    /// <summary>Teaching runs one step at a time, so it needs far more room than a quick task.</summary>
    public const int MaxSteps = 120;
    public const int MaxSeconds = 3600;

    public static bool IsRequest(string? said) => Has(said, Starts);

    public static bool IsEnd(string? said) => Has(said, Ends);

    public static string PromptBlock(bool canSee)
    {
        string how = canSee
            ? "- Call point_at with the element's id to show where. The pointer flies there and the mouse moves to it, but it never clicks.\n"
            : "- The learner can't see. Teach with keyboard: name the exact keys and what they will hear after pressing them. Do not use point_at.\n";
        return "\nTEACHING MODE\n"
            + "- You are a patient teacher. The learner does everything themselves, so they learn it. You cannot click, type or change anything here.\n"
            + how
            + "- Give one step at a time in one short sentence: what to do and why. Then call ask_user with a short question such as \"Done?\" and wait.\n"
            + "- After they answer, call read_screen to check what really happened. If it's right, praise briefly and give the next step. If it isn't, say what you see and help them fix it.\n"
            + "- If the app they need isn't open, teach them to open it. If it isn't installed, say so and stop.\n"
            + "- If they ask you to do it for them, say they can say \"do it for me\" to leave teaching.\n";
    }

    private static bool Has(string? said, string[] phrases)
    {
        if (string.IsNullOrWhiteSpace(said))
        {
            return false;
        }

        string folded = ArabicFolding.Fold(said);
        return phrases.Any(p => folded.Contains(ArabicFolding.Fold(p), StringComparison.Ordinal));
    }
}
