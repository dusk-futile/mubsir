using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Basira.App.Abstractions;
using Basira.App.Screen.Interop;
using Basira.Core.Common;
using Basira.Core.Context;
using Basira.Core.Localization;
using Basira.Core.Logging;
using Interop.UIAutomationClient;
using Windows.Graphics.Imaging;

namespace Basira.App.Screen;

/// <summary>
/// Reads the screen with UI Automation (two MTA worker threads: one for snapshots, one for fast "where am I"
/// queries), captures the foreground window with GDI, and runs Windows OCR. Every call is time-boxed; failures are
/// logged as metadata and come back as empty or partial results, never as exceptions to the caller.
/// </summary>
internal sealed class UiaScreenReader : IScreenReader, IDisposable
{
    /// <summary>Overall budget of one UIA walk (the walk itself stops and reports a partial result).</summary>
    public static readonly TimeSpan WalkBudget = TimeSpan.FromMilliseconds(1500);

    public const int VisibleTextChars = 20_000;
    public const int DocumentTextChars = 200_000;
    public const int JpegMaxSide = 1280;
    public const float JpegQuality = 0.75f;

    private static readonly TimeSpan WalkTimeout = WalkBudget + TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan FastTimeout = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan TextTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OcrTimeout = TimeSpan.FromSeconds(10);

    private readonly UiaThread _main = new("basira-uia");
    private readonly UiaThread _fast = new("basira-uia-fast");
    private readonly ConcurrentDictionary<string, string?> _appNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly IEventLog _log;

    public UiaScreenReader(IEventLog? log)
    {
        _log = log ?? NullEventLog.Instance;
    }

    /// <summary>Printed ids of the latest snapshot -> live elements (used by the action executor).</summary>
    public ElementRegistry Registry { get; } = new();

    public OcrService Ocr { get; } = new();

    public ForegroundInfo GetForeground()
    {
        try
        {
            nint hwnd = ScreenNative.GetForegroundWindow();
            if (hwnd == 0)
            {
                return new ForegroundInfo(0, 0, null, null, null, false, false);
            }

            ScreenNative.GetWindowThreadProcessId(hwnd, out uint pid);
            var path = ScreenNative.ProcessImagePath(pid);
            var processName = path is null ? null : Path.GetFileName(path);
            return new ForegroundInfo(
                hwnd,
                (int)pid,
                processName,
                ScreenNative.WindowText(hwnd),
                ScreenNative.ClassName(hwnd),
                ScreenNative.IsProcessElevated(pid),
                UiaClassifier.IsBrowser(processName));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Fail(LogKind.Error, "fg." + MetaLog.Code(e));
            return new ForegroundInfo(0, 0, null, null, null, false, false);
        }
    }

    public async Task<(ScreenContext Screen, BuiltContext Built)> CaptureContextAsync(ReadScope scope, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var fg = GetForeground();
        var appName = FriendlyAppName(fg);
        long generation = Registry.NextGeneration();

        WalkResult? result = null;
        if (fg.Hwnd != 0)
        {
            try
            {
                result = await _main.RunAsync(
                    uia => WalkAndRead(uia, fg, scope, ct),
                    WalkTimeout,
                    ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.Fail(LogKind.Error, "uia.snapshot." + MetaLog.Code(e), latencyMs: clock.ElapsedMilliseconds);
            }
        }

        var walk = result?.Walk;
        var screen = new ScreenContext
        {
            AppName = appName,
            ProcessName = fg.ProcessName,
            WindowTitle = fg.WindowTitle,
            WindowClass = fg.WindowClass,
            Url = walk?.Url,
            Domain = walk?.Domain,
            IsBrowser = fg.IsBrowser,
            Focused = walk?.Focused,
            Root = walk?.Root,
            VisibleText = result?.Text,
            Selection = result?.Selection,
            IsElevated = fg.IsElevated,
            Truncated = walk is null || walk.Truncated,
        };

        var built = ContextBuilder.Build(screen);
        Registry.Replace(new ScreenSnapshot(
            generation,
            fg.Hwnd,
            screen,
            built,
            walk?.Elements ?? new Dictionary<int, IUIAutomationElement>(),
            Environment.TickCount64));
        _log.Metric("uia.snapshot_ms", clock.ElapsedMilliseconds);
        _log.Metric("uia.snapshot_nodes", walk?.Elements.Count ?? 0);
        return (screen, built);
    }

    public async Task<string?> DescribeFocusAsync(Lang lang, CancellationToken ct)
    {
        var fg = GetForeground();
        var app = FriendlyAppName(fg) ?? (lang == Lang.Arabic ? "نافذة غير معروفة" : "an unknown window");
        UiNode? focused = null;
        if (fg.Hwnd != 0)
        {
            try
            {
                focused = await _fast.RunAsync(
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
                _log.Fail(LogKind.Error, "uia.focus." + MetaLog.Code(e));
            }
        }

        var sb = new StringBuilder();
        var title = ScreenText.Short(fg.WindowTitle, 120);
        if (title is null || string.Equals(title, app, StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(lang == Lang.Arabic ? $"أنت في {app}." : $"You are in {app}.");
        }
        else
        {
            sb.Append(Strings.Get(StringKey.WhereAmI, lang, app, title));
        }

        if (focused is not null)
        {
            sb.Append(' ').Append(lang == Lang.Arabic ? "التركيز على " : "Focus: ").Append(ScreenText.Describe(focused, lang)).Append('.');
        }

        if (fg.IsElevated)
        {
            sb.Append(' ').Append(Strings.Get(StringKey.AdminWindow, lang));
        }

        return sb.ToString();
    }

    public async Task<string?> ReadTextAsync(ReadScope scope, CancellationToken ct)
    {
        var fg = GetForeground();
        if (fg.Hwnd == 0)
        {
            return null;
        }

        try
        {
            if (scope == ReadScope.Focus)
            {
                return await _fast.RunAsync(uia => FocusedText(uia), FastTimeout, ct).ConfigureAwait(false);
            }

            return await _main.RunAsync(
                uia =>
                {
                    var walk = UiaSnapshotter.Walk(uia, fg.Hwnd, fg.IsBrowser, scope, WalkBudget, ct);
                    string? text = null;
                    if (walk.TextElement is not null)
                    {
                        text = scope == ReadScope.Document
                            ? DocumentText(walk.TextElement, DocumentTextChars)
                            : VisibleText(walk.TextElement, VisibleTextChars);
                    }

                    return string.IsNullOrWhiteSpace(text) ? NullIfBlank(ScreenText.OnScreenText(walk.Root, VisibleTextChars)) : text;
                },
                TextTimeout,
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Fail(LogKind.Error, "uia.text." + MetaLog.Code(e));
            return null;
        }
    }

    public async Task<byte[]?> CaptureForegroundJpegAsync(CancellationToken ct)
    {
        try
        {
            var image = await CaptureForegroundAsync(ct).ConfigureAwait(false);
            if (image is null)
            {
                return null;
            }

            var jpeg = await ImageCodec.EncodeJpegAsync(image, JpegMaxSide, JpegQuality, ct)
                .WaitAsync(CaptureTimeout, ct)
                .ConfigureAwait(false);
            return jpeg.Length == 0 ? null : jpeg;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Fail(LogKind.Error, "capture." + MetaLog.Code(e));
            return null;
        }
    }

    public async Task<string?> OcrForegroundAsync(CancellationToken ct)
    {
        try
        {
            var image = await CaptureForegroundAsync(ct).ConfigureAwait(false);
            if (image is null)
            {
                return null;
            }

            using var bitmap = await ImageCodec.ToOcrBitmapAsync(image, OcrService.MaxImageDimension, ct)
                .WaitAsync(CaptureTimeout, ct)
                .ConfigureAwait(false);
            return await OcrBitmapAsync(bitmap, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Fail(LogKind.Error, "ocr." + MetaLog.Code(e));
            return null;
        }
    }

    /// <summary>OCR of an already decoded bitmap (also used for scanned PDF pages).</summary>
    public async Task<string?> OcrBitmapAsync(SoftwareBitmap bitmap, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var text = await Ocr.RecognizeAsync(bitmap, ct).WaitAsync(OcrTimeout, ct).ConfigureAwait(false);
        _log.Metric("ocr_ms", clock.ElapsedMilliseconds);
        return text;
    }

    /// <summary>Runs work on the snapshot UIA thread (the action executor resolves and drives elements here).</summary>
    public Task<T> RunUiaAsync<T>(Func<IUIAutomation, T> work, TimeSpan timeout, CancellationToken ct) =>
        _main.RunAsync(work, timeout, ct);

    /// <summary>Runs a quick query on the fast UIA thread (focus checks).</summary>
    public Task<T> RunFastAsync<T>(Func<IUIAutomation, T> work, TimeSpan timeout, CancellationToken ct) =>
        _fast.RunAsync(work, timeout, ct);

    /// <summary>The app's friendly name: the executable's FileDescription ("Google Chrome"), else its base name.</summary>
    public string? FriendlyAppName(ForegroundInfo fg)
    {
        if (fg.ProcessName is null)
        {
            return ScreenText.Short(fg.WindowTitle, 60);
        }

        // Store apps live inside ApplicationFrameHost; their window title is the app's name.
        if (string.Equals(fg.ProcessName, "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            return ScreenText.Short(fg.WindowTitle, 60) ?? "Windows";
        }

        string? path = null;
        try
        {
            path = ScreenNative.ProcessImagePath((uint)fg.ProcessId);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
        }

        var key = path ?? fg.ProcessName;
        return _appNames.GetOrAdd(key, static (k, name) => Describe(k) ?? UiaClassifier.ProcessBaseName(name), fg.ProcessName);

        static string? Describe(string path)
        {
            try
            {
                if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                {
                    return null;
                }

                var info = FileVersionInfo.GetVersionInfo(path);
                return ScreenText.Short(info.FileDescription, 60) ?? ScreenText.Short(info.ProductName, 60);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public void Dispose()
    {
        _main.Dispose();
        _fast.Dispose();
        Registry.Clear();
    }

    internal static string? VisibleText(IUIAutomationElement element, int maxChars)
    {
        if (element.GetCurrentPattern(UiaIds.TextPattern) is not IUIAutomationTextPattern text)
        {
            return null;
        }

        var ranges = text.GetVisibleRanges();
        if (ranges is null)
        {
            return null;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < ranges.Length && sb.Length < maxChars; i++)
        {
            var chunk = ranges.GetElement(i)?.GetText(maxChars - sb.Length);
            if (!string.IsNullOrEmpty(chunk))
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(chunk);
            }
        }

        return NullIfBlank(sb.ToString());
    }

    internal static string? DocumentText(IUIAutomationElement element, int maxChars)
    {
        if (element.GetCurrentPattern(UiaIds.TextPattern) is not IUIAutomationTextPattern text)
        {
            return null;
        }

        return NullIfBlank(text.DocumentRange?.GetText(maxChars));
    }

    internal static string? SelectionText(IUIAutomationElement element, int maxChars)
    {
        if (element.GetCurrentPattern(UiaIds.TextPattern) is not IUIAutomationTextPattern text)
        {
            return null;
        }

        var selection = text.GetSelection();
        if (selection is null || selection.Length == 0)
        {
            return null;
        }

        return NullIfBlank(selection.GetElement(0)?.GetText(maxChars));
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static WalkResult WalkAndRead(IUIAutomation uia, ForegroundInfo fg, ReadScope scope, CancellationToken ct)
    {
        var walk = UiaSnapshotter.Walk(uia, fg.Hwnd, fg.IsBrowser, scope, WalkBudget, ct);
        string? text = null;
        string? selection = null;
        if (walk.TextElement is { } te && scope != ReadScope.Focus)
        {
            try
            {
                text = scope == ReadScope.Document ? DocumentText(te, VisibleTextChars) : VisibleText(te, VisibleTextChars);
                selection = SelectionText(te, 2000);
            }
            catch (COMException)
            {
                // Some providers expose TextPattern but fail its calls; the element list is still useful.
            }
            catch (InvalidCastException)
            {
            }
        }

        return new WalkResult(walk, text, selection);
    }

    private static string? FocusedText(IUIAutomation uia)
    {
        var focused = uia.GetFocusedElement();
        if (focused is null)
        {
            return null;
        }

        var name = NullIfBlank(focused.CurrentName);
        if (focused.CurrentIsPassword != 0)
        {
            return name; // the label only, never what was typed
        }

        if (focused.GetCurrentPattern(UiaIds.ValuePattern) is IUIAutomationValuePattern value && NullIfBlank(value.CurrentValue) is { } v)
        {
            return name is null || v.Contains(name, StringComparison.Ordinal) ? v : name + ": " + v;
        }

        try
        {
            if (DocumentText(focused, VisibleTextChars) is { } t)
            {
                return t;
            }
        }
        catch (COMException)
        {
        }

        return name;
    }

    private async Task<CapturedImage?> CaptureForegroundAsync(CancellationToken ct)
    {
        nint hwnd = ScreenNative.GetForegroundWindow();
        if (hwnd == 0)
        {
            return null;
        }

        return await Task.Run(() => ScreenCapture.CaptureWindow(hwnd), ct).WaitAsync(CaptureTimeout, ct).ConfigureAwait(false);
    }

    private sealed record WalkResult(UiaWalk Walk, string? Text, string? Selection);
}
