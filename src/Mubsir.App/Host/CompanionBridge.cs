using Mubsir.Core.Logging;
using Mubsir.Core.Motion;
using Mubsir.Overlay;

namespace Mubsir.App.Host;

/// <summary>
/// Tells the notch what the voice agent is doing, for anyone looking at the screen. It starts in the background and
/// can fail or be turned off with no effect on anything else: every call is safe to make whether it's running or not.
/// </summary>
internal sealed class CompanionBridge(SpeechService speech, SettingsProvider settings, IEventLog log) : IDisposable
{
    private const int MaxSteps = 6;

    private readonly object _gate = new();
    private readonly List<string> _steps = [];
    private OverlayHost? _overlay;
    private bool _busy;

    public void Start()
    {
        speech.SpeakingChanged += OnSpeaking;
        _ = Task.Run(() =>
        {
            try
            {
                OverlayHost overlay = OverlayHost.Start(settings.Settings.ShowHudhud);
                overlay.Failed += e => log.Failure("overlay", e);
                _overlay = overlay;
            }
            catch (InvalidOperationException e)
            {
                log.Failure("overlay_start", e.InnerException ?? e);
            }
        });
    }

    public void Listening()
    {
        _busy = true;
        _overlay?.Show(CompanionState.Listening);
    }

    public void Thinking()
    {
        _busy = true;
        _overlay?.Show(CompanionState.Thinking);
    }

    public void BeginTask()
    {
        lock (_gate)
        {
            _steps.Clear();
        }
    }

    /// <summary>Each step the agent takes becomes a line. The last one is running and the earlier ones are done.</summary>
    public void Step(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (_gate)
        {
            _steps.Add(text);
            if (_steps.Count > MaxSteps)
            {
                _steps.RemoveAt(0);
            }

            _overlay?.Show(CompanionState.Doing, new CompanionContent { Steps = Lines(StepState.Running), Mode = DoingMode.Running });
        }
    }

    public void TaskEnded()
    {
        _busy = false;
        if (!speech.IsSpeaking)
        {
            _overlay?.Show(CompanionState.Idle);
        }
    }

    public void PointAt(int x, int y, string label)
    {
        _overlay?.Show(CompanionState.Pointing, new CompanionContent { PointText = label });
        _overlay?.PointAt(x, y, label);
    }

    public void Dispose()
    {
        speech.SpeakingChanged -= OnSpeaking;
        _overlay?.Dispose();
    }

    private CompanionStep[] Lines(StepState last) =>
        _steps.Select((s, i) => new CompanionStep(s, i == _steps.Count - 1 ? last : StepState.Done)).ToArray();

    private void OnSpeaking(bool speaking)
    {
        if (speaking)
        {
            _overlay?.Show(CompanionState.Speaking);
        }
        else if (!_busy)
        {
            _overlay?.Show(CompanionState.Idle);
        }
    }
}
