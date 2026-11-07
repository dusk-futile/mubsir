using Mubsir.Core.Logging;
using Mubsir.Core.Motion;
using Mubsir.Overlay;

namespace Mubsir.App.Host;

/// <summary>
/// Tells the notch what the voice agent is doing, for anyone looking at the screen. It starts in the background and
/// can fail or be turned off with no effect on anything else: every call is safe to make whether it's running or not.
/// </summary>
internal sealed class CompanionBridge(SpeechService speech, WaveInRecorder recorder, IEventLog log) : IDisposable
{
    private const int MaxSteps = 6;

    private readonly object _gate = new();
    private readonly List<string> _steps = [];
    private OverlayHost? _overlay;
    private bool _busy;
    private bool _disposed;

    public void Start()
    {
        speech.SpeakingChanged += OnSpeaking;
        speech.History.Spoken += OnSentence;
        recorder.Level += OnMic;
        _ = Task.Run(() =>
        {
            try
            {
                OverlayHost overlay = OverlayHost.Start();
                overlay.Failed += e => log.Failure("overlay", e);
                lock (_gate)
                {
                    if (_disposed)
                    {
                        overlay.Dispose();
                        return;
                    }

                    _overlay = overlay;
                }
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

        // The wave follows the real mic while listening. Speech has no level to read, so it uses the demo's motion.
        _overlay?.UseDemoVoice(false);
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

    public void Dispose()
    {
        speech.SpeakingChanged -= OnSpeaking;
        speech.History.Spoken -= OnSentence;
        recorder.Level -= OnMic;
        lock (_gate)
        {
            _disposed = true;
            _overlay?.Dispose();
        }
    }

    private CompanionStep[] Lines(StepState last) =>
        _steps.Select((s, i) => new CompanionStep(s, i == _steps.Count - 1 ? last : StepState.Done)).ToArray();

    private void OnMic(double level) => _overlay?.SetVoiceLevel(level);

    /// <summary>What's being said, for anyone looking at the screen.</summary>
    private void OnSentence(int index)
    {
        if (speech.IsSpeaking)
        {
            _overlay?.Show(CompanionState.Speaking, Caption(speech.History.Current?.Text));
        }
    }

    private static CompanionContent? Caption(string? text) => string.IsNullOrWhiteSpace(text)
        ? null
        : text.Any(c => c is >= '\u0600' and <= '\u06FF')
            ? new CompanionContent { Caption = string.Empty, CaptionArabic = text }
            : new CompanionContent { Caption = text };

    private void OnSpeaking(bool speaking)
    {
        if (speaking)
        {
            _overlay?.UseDemoVoice();
            _overlay?.Show(CompanionState.Speaking, Caption(speech.History.Current?.Text));
        }
        else
        {
            _overlay?.Show(_busy ? CompanionState.Thinking : CompanionState.Idle);
        }
    }
}
