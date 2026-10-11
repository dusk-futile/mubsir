using System.Collections.Frozen;
using System.Text;
using Mubsir.Core.Common;
using Mubsir.Core.Context;

namespace Mubsir.Core.Policy;

public enum ActionTier
{
    Allow,
    Confirm,
    Block,
}

public sealed record AgentAction
{
    public string Tool { get; init; } = "";

    public IReadOnlyDictionary<string, string?> Args { get; init; } = FrozenDictionary<string, string?>.Empty;

    public UiNode? Target { get; init; }

    public ScreenContext? Screen { get; init; }

    public string? KeyChord { get; init; }

    public IReadOnlyList<string> FilePaths { get; init; } = [];

    public string? DestinationPath { get; init; }

    public string? Url { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Tool = ").Append(Tool)
            .Append(", Args = ").Append(Args.Count)
            .Append(", FilePaths = ").Append(FilePaths.Count);
        return true;
    }
}

public sealed class TaskContext
{
    private static readonly IReadOnlySet<string> NoDomains = FrozenSet<string>.Empty;

    private volatile bool _untrustedContentSeen;
    private volatile bool _sensitiveMode;

    public string UserUtterance { get; init; } = "";

    public Lang Lang { get; init; } = Lang.Arabic;

    public bool UntrustedContentSeen
    {
        get => _untrustedContentSeen;
        set => _untrustedContentSeen = value;
    }

    public bool SensitiveMode
    {
        get => _sensitiveMode;
        set => _sensitiveMode = value;
    }

    public IReadOnlySet<string> UserNamedDomains { get; init; } = NoDomains;

    public IReadOnlyList<string> LibraryRoots { get; init; } = [];

    public IReadOnlyList<string> BankDomains { get; init; } = [];

    public bool PrivateMode { get; init; }
}

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

    public bool SuggestedByPage { get; init; }

    public string? ToolName { get; init; }

    public string? KeyChord { get; init; }
}

public sealed record PolicyDecision(ActionTier Tier, string ReasonCode, ConfirmationFacts? Facts = null, bool BankHandoff = false);

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
