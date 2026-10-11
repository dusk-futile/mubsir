using System.Diagnostics;
using Basira.App.Abstractions;
using Basira.App.Screen;
using Basira.App.Screen.Documents;
using Basira.App.Screen.Interop;
using Basira.Core.Agent;
using Basira.Core.Brain;
using Basira.Core.Common;
using Basira.Core.Context;
using Basira.Core.Logging;
using Basira.Core.Policy;
using Basira.Core.Speech;
using Interop.UIAutomationClient;

namespace Basira.App.Actions;

/// <summary>What <see cref="WindowsToolExecutor.PrepareAsync"/> learned that the later run needs.</summary>
internal sealed class ExecutorState
{
    public required TaskContext Task { get; init; }

    /// <summary>Foreground window when the step was prepared; input actions abort if it changed.</summary>
    public nint Hwnd { get; init; }

    /// <summary>The live element for the model's id (or for scrolling), captured at prepare time.</summary>
    public IUIAutomationElement? Element { get; set; }

    public string? TargetRuntimeId { get; set; }

    /// <summary>The model gave an id that is not in the latest snapshot of this window.</summary>
    public bool TargetMissing { get; set; }

    /// <summary>The target is the focused element (typing and keys), re-checked before input.</summary>
    public bool FocusTarget { get; set; }
}

/// <summary>
/// Runs agent tools on Windows: UI Automation patterns first, SendInput as the fallback, shell and file APIs for the
/// rest. Before any input it re-checks that the foreground window and the target are unchanged. Failures come back as
/// error tool results (codes and plain sentences, never exception text); logs carry tool names and codes only.
/// </summary>
internal sealed partial class WindowsToolExecutor : IToolExecutor
{
    private static readonly TimeSpan UiaActionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FastTimeout = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(400);

    private readonly UiaScreenReader _screen;
    private readonly DocumentReader _documents;
    private readonly ReadingPositions _positions;
    private readonly IBrain? _brain;
    private readonly ISpeechService? _speech;
    private readonly ISettingsProvider? _settings;
    private readonly IMemoryStore? _memory;
    private readonly IUserPrompter? _prompter;
    private readonly IUsageSink? _usage;
    private readonly IEventLog _log;
    private readonly PolicyEngine _policy = new();
    private readonly AppLauncher _apps = new();
    private readonly object _windowsGate = new();
    private List<WindowEntry> _lastWindows = [];

    public WindowsToolExecutor(
        UiaScreenReader screen,
        DocumentReader documents,
        ReadingPositions positions,
        IBrain? brain,
        ISpeechService? speech,
        ISettingsProvider? settings,
        IMemoryStore? memory,
        IUserPrompter? prompter,
        IUsageSink? usage,
        IEventLog? log)
    {
        _screen = screen;
        _documents = documents;
        _positions = positions;
        _brain = brain;
        _speech = speech;
        _settings = settings;
        _memory = memory;
        _prompter = prompter;
        _usage = usage;
        _log = log ?? NullEventLog.Instance;
    }

    public async Task<PreparedAction> PrepareAsync(ToolCall call, TaskContext task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(task);

        var tool = (call.Name ?? "").Trim().ToLowerInvariant();
        var args = ToolArgs.Parse(call.Arguments);
        var fg = _screen.GetForeground();
        var snapshot = _screen.Registry.Current;
        var state = new ExecutorState { Task = task, Hwnd = fg.Hwnd };
        bool sameWindow = snapshot is not null && snapshot.Hwnd == fg.Hwnd && fg.Hwnd != 0;
        UiNode? target = null;

        if (tool is ToolNames.Invoke or ToolNames.SetText or ToolNames.Focus or ToolNames.Scroll && ToolArgs.Int(args, "id") is { } id)
        {
            if (_screen.Registry.TryResolve(id, out var node, out var element, out var snap) && node is not null)
            {
                target = node;
                state.TargetRuntimeId = node.RuntimeId;
                state.Element = element;
                state.TargetMissing = element is null || snap is null || snap.Hwnd != fg.Hwnd;
            }
            else
            {
                state.TargetMissing = true;
            }
        }
        else if (tool is ToolNames.TypeText or ToolNames.PressKeys)
        {
            target = await FocusedNodeAsync(fg, ct).ConfigureAwait(false);
            state.TargetRuntimeId = target?.RuntimeId;
            state.FocusTarget = true;
        }
        else if (tool == ToolNames.Scroll && sameWindow)
        {
            // No id: scroll the main document of the window when the snapshot has one.
            var doc = UiFacts.Flatten(snapshot!.Screen.Root).FirstOrDefault(n => n.Role == "document");
            state.Element = _screen.Registry.ElementOf(doc);
        }

        var screen = sameWindow ? snapshot!.Screen : LightContext(fg);
        if (state.FocusTarget && target is not null)
        {
            screen = screen with { Focused = target };
        }

        MarkSensitive(task, screen);

        string? keyChord = null;
        if (tool == ToolNames.PressKeys)
        {
            var chords = KeyChordParser.Normalize(ToolArgs.Str(args, "keys"));
            keyChord = chords.Count > 0 ? string.Join(' ', chords) : ToolArgs.Str(args, "keys");
        }

        string? url = tool switch
        {
            ToolNames.OpenUrl => ToolArgs.Str(args, "url"),
            ToolNames.Invoke when target is { Role: "link" } && Uri.TryCreate(target.Value, UriKind.Absolute, out _) => target.Value,
            _ => null,
        };

        if (tool is ToolNames.Invoke or ToolNames.PressKeys or ToolNames.SetText)
        {
            UiFacts.AddFacts(args, target, screen);
        }

        var (paths, destination) = ResolvePaths(tool, args);
        var action = new AgentAction
        {
            Tool = tool,
            Args = args,
            Target = target,
            Screen = screen,
            KeyChord = keyChord,
            FilePaths = paths,
            DestinationPath = destination,
            Url = url,
        };
        return new PreparedAction(call, action, state);
    }

    public async Task<ToolResult> ExecuteAsync(PreparedAction action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        var clock = Stopwatch.StartNew();
        var tool = action.Action.Tool;
        var state = action.ExecutorState as ExecutorState
            ?? new ExecutorState { Task = new TaskContext(), Hwnd = ScreenNative.GetForegroundWindow() };
        string content;
        bool isError;
        try
        {
            (content, isError) = await RunToolAsync(tool, action.Action, state, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            (content, isError) = ("error: the step took too long and was stopped; call read_screen to see what happened", true);
        }
        catch (UiaUnavailableException)
        {
            (content, isError) = ("error: UI Automation is not available on this PC", true);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Fail(LogKind.ToolCall, "exec." + MetaLog.Code(e), tool, clock.ElapsedMilliseconds);
            return new ToolResult(action.Call.Id, "error: " + MetaLog.Code(e), IsError: true);
        }

        if (isError)
        {
            _log.Fail(LogKind.ToolCall, "tool_error", tool, clock.ElapsedMilliseconds);
        }
        else
        {
            _log.Ok(LogKind.ToolCall, tool, clock.ElapsedMilliseconds, action.Action.Target?.Role);
        }

        return new ToolResult(action.Call.Id, content, isError);
    }

    public async Task<ToolResult> HandOffAsync(PreparedAction action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        var state = action.ExecutorState as ExecutorState;
        var a = action.Action;
        var lang = state?.Task.Lang ?? Lang.Arabic;
        bool focused = false;
        if (state?.Element is { } element && !state.TargetMissing)
        {
            try
            {
                focused = await _screen.RunUiaAsync(
                    _ => UiaActions.IsAlive(element, state.TargetRuntimeId) && UiaActions.SetFocus(element),
                    UiaActionTimeout,
                    ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.Fail(LogKind.ToolCall, "handoff." + MetaLog.Code(e), a.Tool);
            }
        }

        var sentence = HandOffText.Build(
            ScreenText.Short(a.Target?.Name, 60),
            Arg(a, "amount"),
            Arg(a, "recipient"),
            UiFacts.IsPayLike(a.Target) || Arg(a, "amount") is not null,
            focused,
            lang);
        try
        {
            _speech?.Speak(sentence, UtteranceKind.System, lang, interrupt: true);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Fail(LogKind.Speech, "handoff_speak." + MetaLog.Code(e), a.Tool);
        }

        _log.Ok(LogKind.Policy, "bank_handoff");
        return new ToolResult(action.Call.Id, "handed to the user");
    }

    public string Narration(PreparedAction action, Lang lang)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ActionNarration.For(action.Action, lang);
    }

    private Task<(string Content, bool IsError)> RunToolAsync(string tool, AgentAction a, ExecutorState s, CancellationToken ct) => tool switch
    {
        ToolNames.ReadScreen => ReadScreenAsync(a, s, ct),
        ToolNames.Invoke => InvokeAsync(a, s, ct),
        ToolNames.SetText => SetTextAsync(a, s, ct),
        ToolNames.TypeText => TypeTextAsync(a, s, ct),
        ToolNames.PressKeys => PressKeysAsync(a, s, ct),
        ToolNames.Scroll => ScrollAsync(a, s, ct),
        ToolNames.Focus => FocusAsync(a, s, ct),
        ToolNames.ListWindows => ListWindowsAsync(ct),
        ToolNames.SwitchTo => SwitchToAsync(a, ct),
        ToolNames.OpenApp => OpenAppAsync(a, ct),
        ToolNames.OpenUrl => OpenUrlAsync(a, ct),
        ToolNames.BrowseSearch => BrowseSearchAsync(a, ct),
        ToolNames.DescribeScreen => DescribeScreenAsync(a, s, ct),
        ToolNames.OcrWindow => OcrWindowAsync(s, ct),
        ToolNames.ReadDocument => ReadDocumentAsync(a, s, ct),
        ToolNames.Media => MediaAsync(a, s, ct),
        ToolNames.ListFolder => Task.Run(() => ListFolder(a), ct),
        ToolNames.FindFiles => Task.Run(() => FindFiles(a, s, ct), ct),
        ToolNames.MoveFile => Task.Run(() => MoveOrCopy(a, move: true), ct),
        ToolNames.CopyFile => Task.Run(() => MoveOrCopy(a, move: false), ct),
        ToolNames.RenameFile => Task.Run(() => Rename(a), ct),
        ToolNames.Recycle => Task.Run(() => Recycle(a), ct),
        ToolNames.SystemStatus => Task.Run(() => SystemStatus(a), ct),
        ToolNames.SetVolume => Task.Run(() => SetVolume(a), ct),
        ToolNames.OpenSettings => OpenSettingsAsync(a, ct),
        ToolNames.WindowsUpdateStatus => WindowsUpdateAsync(ct),
        ToolNames.RestartPc => Task.Run(Restart, ct),
        ToolNames.Remember => Task.FromResult(Remember(a)),
        ToolNames.AskUser => AskUserAsync(a, ct),
        _ => Task.FromResult(Error("unknown tool")),
    };

    private static (string, bool) Ok(string content) => (content, false);

    private static (string, bool) Error(string message) => ("error: " + message, true);

    private static string? Arg(AgentAction a, string key) => ToolArgs.Str(a.Args, key);

    /// <summary>Bank mode starts as soon as the window looks like a bank or card page.</summary>
    private void MarkSensitive(TaskContext task, ScreenContext? screen)
    {
        if (screen is null || task.SensitiveMode)
        {
            return;
        }

        try
        {
            if (SensitiveDetector.Evaluate(screen, task.BankDomains).Sensitive)
            {
                task.SensitiveMode = true;
                _log.Ok(LogKind.Policy, "bank_mode_detected");
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Fail(LogKind.Policy, "sensitive." + MetaLog.Code(e));
        }
    }

    private ScreenContext LightContext(ForegroundInfo fg) => new()
    {
        AppName = _screen.FriendlyAppName(fg),
        ProcessName = fg.ProcessName,
        WindowTitle = fg.WindowTitle,
        WindowClass = fg.WindowClass,
        IsBrowser = fg.IsBrowser,
        IsElevated = fg.IsElevated,
        Truncated = true,
    };

    private async Task<UiNode?> FocusedNodeAsync(ForegroundInfo fg, CancellationToken ct)
    {
        if (fg.Hwnd == 0)
        {
            return null;
        }

        try
        {
            return await _screen.RunFastAsync(
                uia => UiaSnapshotter.Describe(uia, uia.GetFocusedElement(), fg.IsBrowser),
                FastTimeout,
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Fail(LogKind.Error, "focus_node." + MetaLog.Code(e));
            return null;
        }
    }

    /// <summary>Resolves library names and relative library paths for the file tools (no file is touched).</summary>
    private static (IReadOnlyList<string> Paths, string? Destination) ResolvePaths(string tool, IReadOnlyDictionary<string, string?> args)
    {
        static string Resolve(string raw) => FileOps.ResolveUserPath(raw) ?? raw;

        switch (tool)
        {
            case ToolNames.ListFolder:
            case ToolNames.ReadDocument:
                return ToolArgs.Str(args, "path") is { } p ? ([Resolve(p)], null) : ([], null);
            case ToolNames.FindFiles:
                return ToolArgs.Str(args, "folder") is { } f ? ([Resolve(f)], null) : ([], null);
            case ToolNames.MoveFile:
            case ToolNames.CopyFile:
                if (ToolArgs.Str(args, "from") is not { } from)
                {
                    return ([], null);
                }

                var src = Resolve(from);
                return ([src], FileOps.Destination(src, ToolArgs.Str(args, "to"), SafeIsDirectory) ?? ToolArgs.Str(args, "to"));
            case ToolNames.RenameFile:
                if (ToolArgs.Str(args, "path") is not { } path)
                {
                    return ([], null);
                }

                var source = Resolve(path);
                var newName = ToolArgs.Str(args, "new_name");
                string? dest = null;
                if (newName is not null && newName.IndexOfAny(['\\', '/', ':']) < 0 && Path.GetDirectoryName(source.TrimEnd('\\', '/')) is { } dir)
                {
                    dest = Path.Combine(dir, newName);
                }

                return ([source], dest);
            case ToolNames.Recycle:
                return (ToolArgs.List(args, "paths").Select(Resolve).ToList(), null);
            default:
                return ([], null);
        }
    }

    private static bool SafeIsDirectory(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
