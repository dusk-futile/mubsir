using System.Collections.Frozen;
using System.Text;
using Basira.Core.Common;
using Basira.Core.Context;

namespace Basira.Core.Policy;

/// <summary>Tier 1 runs with narration, tier 2 needs one key from the user, tier 3 is never done by the agent.</summary>
public enum ActionTier
{
    Allow,
    Confirm,
    Block,
}

/// <summary>
/// A tool call after the Windows layer resolved it against the screen. Every field the policy reads is ground truth
/// gathered locally (UIA, file system), not text from the model, except <see cref="Args"/>, which echo the call.
/// </summary>
public sealed record AgentAction
{
    /// <summary>Tool name from <see cref="Brain.ToolCatalog"/>, e.g. "invoke".</summary>
    public string Tool { get; init; } = "";

    /// <summary>
    /// Tool arguments as strings. The executor also adds UIA facts under "recipient", "body" and "amount"
    /// when it can read them from the page (these feed the confirmation sentence).
    /// </summary>
    public IReadOnlyDictionary<string, string?> Args { get; init; } = FrozenDictionary<string, string?>.Empty;

    /// <summary>The element the action affects: the id's node for invoke/set_text/focus, the focused element for keys and typing.</summary>
    public UiNode? Target { get; init; }

    /// <summary>The foreground window at the time of the action.</summary>
    public ScreenContext? Screen { get; init; }

    /// <summary>Normalized chord such as "enter", "ctrl+enter", "alt+s" or "win+r"; several chords may be space-separated.</summary>
    public string? KeyChord { get; init; }

    /// <summary>Absolute source paths, resolved by the executor (library names like "Desktop" already expanded).</summary>
    public IReadOnlyList<string> FilePaths { get; init; } = [];

    /// <summary>Absolute destination path for move, copy and rename, resolved by the executor.</summary>
    public string? DestinationPath { get; init; }

    /// <summary>The URL to open, or the href of the link being invoked when known.</summary>
    public string? Url { get; init; }

    // Metadata only: arguments are content and must not end up in logs through ToString().
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Tool = ").Append(Tool)
            .Append(", Args = ").Append(Args.Count)
            .Append(", FilePaths = ").Append(FilePaths.Count);
        return true;
    }
}

/// <summary>State of one spoken task, shared by the agent loop and the policy.</summary>
public sealed class TaskContext
{
    private static readonly IReadOnlySet<string> NoDomains = FrozenSet<string>.Empty;

    private volatile bool _untrustedContentSeen;
    private volatile bool _sensitiveMode;

    /// <summary>The user's own request (transcript or typed text). The only trusted instruction.</summary>
    public string UserUtterance { get; init; } = "";

    public Lang Lang { get; init; } = Lang.Arabic;

    /// <summary>Set once the agent has read a web page, email, message or document during this task.</summary>
    public bool UntrustedContentSeen
    {
        get => _untrustedContentSeen;
        set => _untrustedContentSeen = value;
    }

    /// <summary>Bank mode: no typing, no screenshots, no cloud. Set by the Windows layer or by detection.</summary>
    public bool SensitiveMode
    {
        get => _sensitiveMode;
        set => _sensitiveMode = value;
    }

    /// <summary>Domains the user named in the request, e.g. "youtube.com".</summary>
    public IReadOnlySet<string> UserNamedDomains { get; init; } = NoDomains;

    /// <summary>Absolute folders the file tools may touch (the user's Desktop, Documents, Downloads, Pictures, Videos, Music).</summary>
    public IReadOnlyList<string> LibraryRoots { get; init; } = [];

    /// <summary>The user's editable list of bank, payment, wallet and government domains.</summary>
    public IReadOnlyList<string> BankDomains { get; init; } = [];

    /// <summary>Private mode: no cloud calls at all.</summary>
    public bool PrivateMode { get; init; }
}

/// <summary>What a tier-2 action will do. Confirmation sentences are built from these facts only.</summary>
public enum ConfirmKind
{
    Send,
    Pay,
    Buy,
    Transfer,
    Delete,
    Submit,
    SignOut,
    Restart,
    Install,
    Post,
    Accept,
    Generic,
}

/// <summary>Ground-truth facts for the confirmation sentence (UIA and file system, never the model's words).</summary>
public sealed record ConfirmationFacts
{
    public ConfirmKind Kind { get; init; } = ConfirmKind.Generic;

    public string? ButtonName { get; init; }

    public string? Recipient { get; init; }

    public string? BodyPreview { get; init; }

    public string? Amount { get; init; }

    public string? Site { get; init; }

    public int FileCount { get; init; }

    public IReadOnlyList<string> FileNames { get; init; } = [];

    /// <summary>The action came from untrusted content rather than the user's request.</summary>
    public bool SuggestedByPage { get; init; }

    /// <summary>The tool behind a generic confirmation ("remember", "open_url", "press_keys"...), so the sentence says what happens.</summary>
    public string? ToolName { get; init; }

    /// <summary>The key chord for press_keys confirmations, e.g. "enter".</summary>
    public string? KeyChord { get; init; }
}

/// <summary>The policy's verdict on one action.</summary>
/// <param name="Tier">Allow, Confirm or Block.</param>
/// <param name="ReasonCode">Stable code from <see cref="ReasonCodes"/>; safe to log and to return to the model.</param>
/// <param name="Facts">Facts for the confirmation sentence (Confirm, and Block with a bank handoff).</param>
/// <param name="BankHandoff">Bank mode: focus the real button and let the user press it himself.</param>
public sealed record PolicyDecision(ActionTier Tier, string ReasonCode, ConfirmationFacts? Facts = null, bool BankHandoff = false);

/// <summary>Reason codes returned in <see cref="PolicyDecision.ReasonCode"/>.</summary>
public static class ReasonCodes
{
    public const string Allow = "allow";
    public const string AllowNoOverwrite = "allow.no_overwrite";
    public const string AllowUserNamedUpdate = "allow.user_named_update";

    public const string UnknownTool = "blocked.unknown_tool";
    public const string Elevated = "blocked.elevated";
    public const string Shell = "blocked.shell";
    public const string RunDialog = "blocked.run_dialog";
    public const string Keys = "blocked.keys";
    public const string UrlScheme = "blocked.url_scheme";
    public const string Executable = "blocked.executable";
    public const string Path = "blocked.path";
    public const string Settings = "blocked.settings";
    public const string AccountSecurity = "blocked.account_security";
    public const string PasswordField = "blocked.password_field";
    public const string SensitiveText = "blocked.sensitive_text";
    public const string PrivateNoScreenshot = "private.no_screenshot";

    public const string BankNoTyping = "bank.no_typing";
    public const string BankNoScreenshot = "bank.no_screenshot";
    public const string BankNoSubmit = "bank.no_submit";
    public const string BankHandoff = "bank.handoff";

    public const string ConfirmVerb = "confirm.verb";
    public const string ConfirmEnterInField = "confirm.enter_in_field";
    public const string ConfirmEnterUnknownTarget = "confirm.enter_unknown_target";
    public const string ConfirmDeleteKey = "confirm.delete_key";
    public const string ConfirmRecycle = "confirm.recycle";
    public const string ConfirmRestart = "confirm.restart";
    public const string ConfirmMailto = "confirm.mailto";
    public const string ConfirmUntrustedClick = "confirm.untrusted_click";
    public const string ConfirmUntrustedUrl = "confirm.untrusted_url";
    public const string ConfirmUntrustedRemember = "confirm.untrusted_remember";
    public const string ConfirmRemember = "confirm.remember";
}
