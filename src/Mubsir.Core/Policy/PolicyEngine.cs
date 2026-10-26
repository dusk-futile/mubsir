using Mubsir.Core.Brain;
using Mubsir.Core.Context;

namespace Mubsir.Core.Policy;

public sealed class PolicyEngine
{
    private static readonly ConfirmKind[] HandoffKinds = [ConfirmKind.Pay, ConfirmKind.Transfer, ConfirmKind.Buy, ConfirmKind.Submit];

    public PolicyDecision Evaluate(AgentAction action, TaskContext task)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(task);

        var tool = (action.Tool ?? "").Trim().ToLowerInvariant();
        if (!ToolNames.IsKnown(tool))
        {
            return Block(ReasonCodes.UnknownTool);
        }

        var chords = tool == ToolNames.PressKeys ? KeyChords.Parse(action.KeyChord ?? Arg(action, "keys")) : [];
        return CheckBlock(tool, action, task, chords)
            ?? CheckBank(tool, action, task, chords)
            ?? CheckUpdateExemption(tool, action, task)
            ?? CheckConfirm(tool, action, task, chords)
            ?? new PolicyDecision(ActionTier.Allow, tool is ToolNames.MoveFile or ToolNames.CopyFile or ToolNames.RenameFile
                ? ReasonCodes.AllowNoOverwrite
                : ReasonCodes.Allow);
    }

    public bool AllowCloud(TaskContext task, ScreenContext? screen)
    {
        ArgumentNullException.ThrowIfNull(task);
        return !task.PrivateMode && !IsBankMode(task, screen, target: null);
    }

    public bool AllowScreenshot(TaskContext task, ScreenContext? screen) => AllowCloud(task, screen);

    public static bool MarksUntrusted(string tool, ScreenContext? screen)
    {
        var t = (tool ?? "").Trim().ToLowerInvariant();
        return t switch
        {
            ToolNames.ReadDocument => true,
            ToolNames.ReadScreen or ToolNames.OcrWindow or ToolNames.DescribeScreen => PolicyRules.IsUntrustedSource(screen),
            _ => false,
        };
    }

    internal static bool IsBankMode(TaskContext task, ScreenContext? screen, UiNode? target) =>
        task.SensitiveMode
        || (screen is not null && SensitiveDetector.Evaluate(screen, task.BankDomains).Sensitive)
        || (target is not null && SensitiveDetector.IsSensitiveField(target));

    private static PolicyDecision? CheckBlock(string tool, AgentAction a, TaskContext task, List<string> chords)
    {
        var screen = a.Screen;
        bool input = tool is ToolNames.Invoke or ToolNames.SetText or ToolNames.TypeText or ToolNames.PressKeys;
        bool typing = tool is ToolNames.SetText or ToolNames.TypeText;

        if (input && screen is { IsElevated: true })
        {
            return Block(ReasonCodes.Elevated);
        }

        if (input && PolicyRules.ShellReason(screen) is { } shell)
        {
            return Block(shell);
        }

        if ((typing || (tool == ToolNames.PressKeys && !chords.All(KeyChords.IsNavigation))) && PolicyRules.IsRunLike(screen))
        {
            return Block(ReasonCodes.RunDialog);
        }

        if (tool == ToolNames.PressKeys && CheckKeys(a, chords) is { } keys)
        {
            return keys;
        }

        if (tool == ToolNames.PressKeys && chords.Any(IsEnterChord) && (a.Target is null || !IsEditable(a.Target))
            && CheckInvokeTarget(a) is { } opened)
        {
            return opened;
        }

        var specific = tool switch
        {
            ToolNames.OpenUrl => CheckOpenUrl(a),
            ToolNames.Invoke => CheckInvokeTarget(a),
            ToolNames.OpenApp => CheckOpenApp(a),
            ToolNames.OpenSettings => PolicyRules.IsBlockedSettingsPage(Arg(a, "page")) ? Block(ReasonCodes.Settings) : null,
            ToolNames.SetText or ToolNames.TypeText => CheckTyping(a, task),
            _ when ToolNames.FileTools.Contains(tool) => CheckFiles(tool, a, task),
            _ => null,
        };
        if (specific is not null)
        {
            return specific;
        }

        if (input && PolicyRules.IsAccountSecurityPage(screen) && !(tool == ToolNames.PressKeys && chords.All(KeyChords.IsNavigation)))
        {
            return Block(ReasonCodes.AccountSecurity);
        }

        if (tool == ToolNames.DescribeScreen && task.PrivateMode)
        {
            return Block(ReasonCodes.PrivateNoScreenshot);
        }

        return null;
    }

    private static PolicyDecision? CheckKeys(AgentAction a, List<string> chords)
    {
        var target = a.Target;
        bool editable = target is not null && IsEditable(target);
        foreach (var chord in chords)
        {
            if (KeyChords.Dangerous.Contains(chord))
            {
                return Block(ReasonCodes.Keys);
            }

            if (chord == "shift+delete" && !editable)
            {
                return Block(ReasonCodes.Keys);
            }

            if (chord == "ctrl+shift+enter" && target is { IsSearchBox: true } or { IsAddressBar: true })
            {
                return Block(ReasonCodes.Keys);
            }

            if (chord == "alt+f4" && PolicyRules.IsDesktop(a.Screen))
            {
                return Block(ReasonCodes.Keys);
            }
        }

        return null;
    }

    private static PolicyDecision? CheckOpenUrl(AgentAction a)
    {
        var raw = a.Url ?? Arg(a, "url");
        if (!PolicyRules.TryParseUrl(raw, allowSchemeless: true, out var url))
        {
            return Block(ReasonCodes.UrlScheme);
        }

        if (url.Scheme == "mailto")
        {
            return null;
        }

        if (url.Scheme is not ("http" or "https") || url.Host.Length == 0)
        {
            return Block(ReasonCodes.UrlScheme);
        }

        return PolicyRules.HasBlockedExtension(url.Path) ? Block(ReasonCodes.Executable) : null;
    }

    private static PolicyDecision? CheckInvokeTarget(AgentAction a)
    {
        var t = a.Target;
        bool browser = a.Screen?.IsBrowser == true;

        if (a.FilePaths.Any(PolicyRules.HasBlockedExtension))
        {
            return Block(ReasonCodes.Executable);
        }

        var href = a.Url ?? (t is { Role: "link" } ? t.Value : null);
        if (PolicyRules.TryParseUrl(href, allowSchemeless: false, out var url))
        {
            switch (url.Scheme)
            {
                case "http" or "https":
                    if (PolicyRules.HasBlockedExtension(url.Path))
                    {
                        return Block(ReasonCodes.Executable);
                    }

                    break;
                case "file":
                    if (url.Host.Length > 0 || url.Path.StartsWith(@"\\", StringComparison.Ordinal) || PolicyRules.HasBlockedExtension(url.Path))
                    {
                        return Block(url.Host.Length > 0 ? ReasonCodes.UrlScheme : ReasonCodes.Executable);
                    }

                    break;
                case "javascript" or "mailto" or "about":
                    break;
                default:
                    return Block(ReasonCodes.UrlScheme);
            }
        }
        else if (PolicyRules.LooseScheme(href) is { } scheme)
        {
            if (scheme is not ("http" or "https" or "javascript" or "mailto" or "about"))
            {
                return Block(ReasonCodes.UrlScheme);
            }

            if (scheme is "http" or "https" && PolicyRules.HasBlockedExtension(href!.Split('?', '#')[0]))
            {
                return Block(ReasonCodes.Executable);
            }
        }

        if (t is null)
        {
            return null;
        }

        bool valueIsUrl = t.Role == "link";
        if (PolicyRules.LooksLikeExecutableName(t.Name, browser)
            || (!valueIsUrl && PolicyRules.LooksLikeExecutableName(t.Value, browser)))
        {
            return Block(ReasonCodes.Executable);
        }

        return null;
    }

    private static PolicyDecision? CheckOpenApp(AgentAction a)
    {
        var name = Arg(a, "name")?.Trim().Trim('"').Trim();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        bool pathLike = name.IndexOfAny(['\\', '/', ':', '%']) >= 0 || name.StartsWith('.');
        if (pathLike)
        {
            return Block(PolicyRules.HasBlockedExtension(name) ? ReasonCodes.Executable : ReasonCodes.Path);
        }

        return PolicyRules.HasBlockedExtension(name) ? Block(ReasonCodes.Executable) : null;
    }

    private static PolicyDecision? CheckTyping(AgentAction a, TaskContext task)
    {
        var target = a.Target;
        if (target is { IsPassword: true })
        {
            return Block(ReasonCodes.PasswordField);
        }

        var text = Arg(a, "text");
        if (Redactor.ContainsCardOrIban(text))
        {
            return Block(ReasonCodes.SensitiveText);
        }

        if (target is { IsAddressBar: true } && !string.IsNullOrWhiteSpace(text))
        {
            if (PolicyRules.IsExplorer(a.Screen))
            {
                var path = PolicyRules.NormalizePath(text);
                return path is not null && PolicyRules.IsPathAllowed(path, task.LibraryRoots) ? null : Block(ReasonCodes.RunDialog);
            }

            if (LooksLikeAddress(text) && PolicyRules.TryParseUrl(text.Trim(), allowSchemeless: true, out var url))
            {
                if (url.Scheme is not ("http" or "https"))
                {
                    return Block(ReasonCodes.UrlScheme);
                }

                if (PolicyRules.HasBlockedExtension(url.Path))
                {
                    return Block(ReasonCodes.Executable);
                }
            }
        }

        return null;
    }

    private static PolicyDecision? CheckFiles(string tool, AgentAction a, TaskContext task)
    {
        var sources = new List<string>(a.FilePaths.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (sources.Count == 0)
        {
            foreach (var key in new[] { "path", "from", "folder" })
            {
                if (Arg(a, key) is { Length: > 0 } v)
                {
                    sources.Add(v);
                }
            }

            if (tool == ToolNames.Recycle && Arg(a, "paths") is { Length: > 0 } list)
            {
                sources.AddRange(list.Split(['\n', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        string? destination = a.DestinationPath;
        if (destination is null && tool is ToolNames.MoveFile or ToolNames.CopyFile)
        {
            destination = Arg(a, "to");
        }

        foreach (var p in destination is null ? sources : sources.Append(destination))
        {
            var n = PolicyRules.NormalizePath(p);
            if (n is null || !PolicyRules.IsPathAllowed(n, task.LibraryRoots))
            {
                return Block(ReasonCodes.Path);
            }
        }

        if (tool == ToolNames.RenameFile && Arg(a, "new_name") is { } newName)
        {
            var nn = newName.Trim();
            if (nn.Length == 0 || nn is "." or ".." || nn.IndexOfAny(['\\', '/', ':']) >= 0)
            {
                return Block(ReasonCodes.Path);
            }

            if (PolicyRules.HasBlockedExtension(nn))
            {
                return Block(ReasonCodes.Executable);
            }
        }

        if (tool is ToolNames.MoveFile or ToolNames.CopyFile or ToolNames.RenameFile && PolicyRules.HasBlockedExtension(destination))
        {
            return Block(ReasonCodes.Executable);
        }

        return null;
    }

    private static PolicyDecision? CheckBank(string tool, AgentAction a, TaskContext task, List<string> chords)
    {
        if (!IsBankMode(task, a.Screen, a.Target))
        {
            return null;
        }

        switch (tool)
        {
            case ToolNames.SetText or ToolNames.TypeText:
                return Block(ReasonCodes.BankNoTyping);

            case ToolNames.DescribeScreen:
                return Block(ReasonCodes.BankNoScreenshot);

            case ToolNames.Invoke:
                return Handoff(a);

            case ToolNames.PressKeys:
                if (chords.Any(KeyChords.IsTyping))
                {
                    return Block(ReasonCodes.BankNoTyping);
                }

                var target = a.Target;
                if (target is not null && !IsEditable(target) && chords.Any(KeyChords.IsActivate))
                {
                    return Handoff(a);
                }

                if (chords.Any(KeyChords.IsSubmit) && (target is null || (IsEditable(target) && !target.IsSearchBox)))
                {
                    return Block(ReasonCodes.BankNoSubmit);
                }

                break;
        }

        return null;
    }

    private static PolicyDecision? Handoff(AgentAction a)
    {
        if (a.Target is not { } t || ClassifyTarget(t) is not { } kind || Array.IndexOf(HandoffKinds, kind) < 0)
        {
            return null;
        }

        var facts = Facts(a, kind, t.Name ?? t.AutomationId, suggested: false);
        return new PolicyDecision(ActionTier.Block, ReasonCodes.BankHandoff, facts, BankHandoff: true);
    }

    private static PolicyDecision? CheckUpdateExemption(string tool, AgentAction a, TaskContext task)
    {
        if (!PolicyRules.UserAskedForUpdate(task.UserUtterance))
        {
            return null;
        }

        if (tool == ToolNames.OpenSettings && PolicyRules.IsWindowsUpdatePage(Arg(a, "page")))
        {
            return new PolicyDecision(ActionTier.Allow, ReasonCodes.AllowUserNamedUpdate);
        }

        if (tool == ToolNames.Invoke && a.Target is { } t && PolicyRules.IsWindowsUpdateScreen(a.Screen)
            && ClassifyTarget(t) is null or ConfirmKind.Install && PolicyRules.IsUpdateButton(t.Name))
        {
            return new PolicyDecision(ActionTier.Allow, ReasonCodes.AllowUserNamedUpdate);
        }

        return null;
    }

    private static PolicyDecision? CheckConfirm(string tool, AgentAction a, TaskContext task, List<string> chords)
    {
        switch (tool)
        {
            case ToolNames.Invoke:
                return ConfirmClick(a, task, chord: null);

            case ToolNames.PressKeys:
                return ConfirmKeys(a, task, chords);

            case ToolNames.Recycle:
                {
                    var files = a.FilePaths.Count > 0
                        ? a.FilePaths
                        : (IReadOnlyList<string>)(Arg(a, "paths") ?? Arg(a, "path") ?? "")
                            .Split(['\n', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var facts = new ConfirmationFacts
                    {
                        Kind = ConfirmKind.Delete,
                        FileCount = files.Count,
                        FileNames = files.Select(FileName).ToList(),
                        ToolName = tool,
                    };
                    return Confirm(ReasonCodes.ConfirmRecycle, facts);
                }

            case ToolNames.RestartPc:
                return Confirm(ReasonCodes.ConfirmRestart, new ConfirmationFacts { Kind = ConfirmKind.Restart, ToolName = tool });

            case ToolNames.OpenUrl:
                {
                    if (!PolicyRules.TryParseUrl(a.Url ?? Arg(a, "url"), allowSchemeless: true, out var url))
                    {
                        return null;
                    }

                    if (url.Scheme == "mailto")
                    {
                        return Confirm(ReasonCodes.ConfirmMailto, new ConfirmationFacts
                        {
                            Kind = ConfirmKind.Send,
                            Recipient = url.MailAddress,
                            ToolName = tool,
                            SuggestedByPage = task.UntrustedContentSeen,
                        });
                    }

                    if (task.UntrustedContentSeen && !PolicyRules.IsUserNamedDomain(url.Host, task))
                    {
                        return Confirm(ReasonCodes.ConfirmUntrustedUrl, new ConfirmationFacts
                        {
                            Kind = ConfirmKind.Generic,
                            Site = url.Host,
                            ToolName = tool,
                            SuggestedByPage = true,
                        });
                    }

                    return null;
                }

            case ToolNames.SetText or ToolNames.TypeText:
                return ConfirmAddressBarUrl(a, task);

            case ToolNames.Remember:
                {
                    var fact = Arg(a, "fact");
                    if (task.UntrustedContentSeen)
                    {
                        return Confirm(ReasonCodes.ConfirmUntrustedRemember, new ConfirmationFacts
                        {
                            Kind = ConfirmKind.Generic,
                            BodyPreview = fact,
                            ToolName = tool,
                            SuggestedByPage = true,
                        });
                    }

                    return PolicyRules.FactFromUtterance(fact, task.UserUtterance)
                        ? null
                        : Confirm(ReasonCodes.ConfirmRemember, new ConfirmationFacts { Kind = ConfirmKind.Generic, BodyPreview = fact, ToolName = tool });
                }
        }

        return null;
    }

    private static PolicyDecision? ConfirmClick(AgentAction a, TaskContext task, string? chord)
    {
        var t = a.Target;
        var screen = a.Screen;
        var site = screen is null ? null : ContextBuilder.SiteOf(screen);
        if (t is null)
        {
            return task.UntrustedContentSeen && screen?.IsBrowser == true
                ? Confirm(ReasonCodes.ConfirmUntrustedClick, new ConfirmationFacts { Kind = ConfirmKind.Generic, Site = site, SuggestedByPage = true, ToolName = a.Tool, KeyChord = chord })
                : null;
        }

        bool named = PolicyRules.NamedInUtterance(t.Name, task.UserUtterance);
        var kind = ClassifyTarget(t);
        if (kind is null && PolicyRules.IsShutdownDialog(screen) && !IsCancelOrHelp(t.Name))
        {
            kind = ConfirmKind.Restart;
        }

        if (kind is { } k)
        {
            bool suggested = task.UntrustedContentSeen && !named && !VerbTable.UtteranceKinds(task.UserUtterance).Contains(k);
            return Confirm(ReasonCodes.ConfirmVerb, Facts(a, k, t.Name ?? t.AutomationId, suggested) with { KeyChord = chord });
        }

        var href = a.Url ?? (t.Role == "link" ? t.Value : null);
        if (PolicyRules.TryParseUrl(href, allowSchemeless: false, out var url) && url.Scheme == "mailto")
        {
            return Confirm(ReasonCodes.ConfirmMailto, new ConfirmationFacts
            {
                Kind = ConfirmKind.Send,
                Recipient = url.MailAddress,
                ToolName = ToolNames.OpenUrl,
                SuggestedByPage = task.UntrustedContentSeen && !named,
            });
        }

        if (task.UntrustedContentSeen && !named && (screen?.IsBrowser == true || t.Role == "link"))
        {
            return Confirm(ReasonCodes.ConfirmUntrustedClick, new ConfirmationFacts
            {
                Kind = ConfirmKind.Generic,
                ButtonName = t.Name,
                Site = site,
                SuggestedByPage = true,
                ToolName = chord is null ? ToolNames.Invoke : ToolNames.PressKeys,
                KeyChord = chord,
            });
        }

        return null;
    }

    private static PolicyDecision? ConfirmKeys(AgentAction a, TaskContext task, List<string> chords)
    {
        var t = a.Target;
        var site = a.Screen is null ? null : ContextBuilder.SiteOf(a.Screen);

        if (t is not null && !IsEditable(t) && chords.FirstOrDefault(KeyChords.IsActivate) is { } activate)
        {
            if (ConfirmClick(a, task, activate) is { } click)
            {
                return click;
            }
        }

        bool deleteKey = chords.Contains("delete") || (PolicyRules.IsExplorer(a.Screen) && chords.Contains("ctrl+d"));
        if (deleteKey && (t is null || !IsEditable(t)))
        {
            return Confirm(ReasonCodes.ConfirmDeleteKey, new ConfirmationFacts
            {
                Kind = ConfirmKind.Delete,
                ButtonName = t?.Name,
                Site = site,
                ToolName = ToolNames.PressKeys,
                KeyChord = "delete",
            });
        }

        var submit = chords.FirstOrDefault(KeyChords.IsSubmit);
        if (submit is null)
        {
            return null;
        }

        if (t is null)
        {
            return Confirm(ReasonCodes.ConfirmEnterUnknownTarget, new ConfirmationFacts
            {
                Kind = ConfirmKind.Generic,
                Site = site,
                ToolName = ToolNames.PressKeys,
                KeyChord = submit,
                SuggestedByPage = task.UntrustedContentSeen,
            });
        }

        if (IsEditable(t) && !t.IsSearchBox && !t.IsAddressBar)
        {
            bool messaging = submit != "enter" || PolicyRules.IsMessagingContext(a.Screen)
                || Arg(a, "recipient") is not null || Arg(a, "body") is not null;
            var kind = messaging ? ConfirmKind.Send : ConfirmKind.Generic;
            bool suggested = task.UntrustedContentSeen && !VerbTable.UtteranceKinds(task.UserUtterance).Contains(kind)
                && kind != ConfirmKind.Generic;
            return Confirm(ReasonCodes.ConfirmEnterInField, Facts(a, kind, t.Name, suggested) with
            {
                ToolName = ToolNames.PressKeys,
                KeyChord = submit,
            });
        }

        return null;
    }

    private static PolicyDecision? ConfirmAddressBarUrl(AgentAction a, TaskContext task)
    {
        if (!task.UntrustedContentSeen || a.Target is not { IsAddressBar: true } || PolicyRules.IsExplorer(a.Screen))
        {
            return null;
        }

        var text = Arg(a, "text");
        if (!LooksLikeAddress(text) || !PolicyRules.TryParseUrl(text!.Trim(), allowSchemeless: true, out var url)
            || PolicyRules.IsUserNamedDomain(url.Host, task))
        {
            return null;
        }

        return Confirm(ReasonCodes.ConfirmUntrustedUrl, new ConfirmationFacts
        {
            Kind = ConfirmKind.Generic,
            Site = url.Host,
            ToolName = ToolNames.OpenUrl,
            SuggestedByPage = true,
        });
    }

    private static ConfirmKind? ClassifyTarget(UiNode t)
    {
        var byName = VerbTable.Classify(t.Name);
        if (byName is not null)
        {
            return byName;
        }

        var byId = VerbTable.Classify(t.AutomationId);
        return byId is ConfirmKind.Submit or ConfirmKind.Accept or ConfirmKind.SignOut && VerbTable.IsHarmlessName(t.Name)
            ? null
            : byId;
    }

    private static ConfirmationFacts Facts(AgentAction a, ConfirmKind kind, string? button, bool suggested) => new()
    {
        Kind = kind,
        ButtonName = button,
        Site = a.Screen is null ? null : ContextBuilder.SiteOf(a.Screen),
        Recipient = Arg(a, "recipient"),
        BodyPreview = Arg(a, "body"),
        Amount = Arg(a, "amount"),
        SuggestedByPage = suggested,
        ToolName = a.Tool,
    };

    private static bool IsEditable(UiNode node) => node.IsEditable || node.Role == "edit";

    private static bool IsEnterChord(string chord) => chord == "enter" || chord.EndsWith("+enter", StringComparison.Ordinal);

    private static bool IsCancelOrHelp(string? name) =>
        PolicyText.Tokens(name).Any(t => t is "cancel" or "help" or "الغاء" or "تعليمات" or "مساعده");

    private static bool LooksLikeAddress(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.Trim();
        return !t.Any(char.IsWhiteSpace) && (t.Contains("://", StringComparison.Ordinal) || t.Contains('.') || t.Contains(':'));
    }

    private static string? Arg(AgentAction a, string key) =>
        a.Args.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static string FileName(string path)
    {
        var p = path.TrimEnd('\\', '/');
        int cut = p.LastIndexOfAny(['\\', '/']);
        return cut >= 0 ? p[(cut + 1)..] : p;
    }

    private static PolicyDecision Block(string reason) => new(ActionTier.Block, reason);

    private static PolicyDecision Confirm(string reason, ConfirmationFacts facts) => new(ActionTier.Confirm, reason, facts);
}
