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
    private readonly List<CompanionStep> _steps = [];
    private OverlayHost? _overlay;
    private bool _busy;
    private bool _disposed;

    /// <summary>The page being read aloud and how many there are, or null when no book is being read.</summary>
    public Func<(int Page, int Pages)?>? Book { get; set; }

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

    /// <summary>Each step the agent takes becomes a line, spinning while it runs.</summary>
    public void Step(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (_gate)
        {
            _steps.Add(new CompanionStep(text, StepState.Running));
            if (_steps.Count > MaxSteps)
            {
                _steps.RemoveAt(0);
            }

            ShowSteps();
        }
    }

    /// <summary>The running step finished: ticked and reworded ("Opened Settings") when it worked, crossed out when it didn't.</summary>
    public void StepFinished(bool ok, string? finished)
    {
        lock (_gate)
        {
            int last = _steps.Count - 1;
            if (last < 0 || _steps[last].State != StepState.Running)
            {
                return;
            }

            _steps[last] = _steps[last] with
            {
                State = ok ? StepState.Done : StepState.Undone,
                Text = string.IsNullOrWhiteSpace(finished) ? _steps[last].Text : finished,
            };
            ShowSteps();
        }
    }

    private void ShowSteps() =>
        _overlay?.Show(CompanionState.Doing, new CompanionContent { Steps = _steps.ToArray(), Mode = DoingMode.Running });

    public void TaskEnded()
    {
        _busy = false;
        lock (_gate)
        {
            // A step that was handed off (reading aloud, for one) never reports back: the task ending means it ran.
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].State == StepState.Running)
                {
                    _steps[i] = _steps[i] with { State = StepState.Done };
                }
            }
        }

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

    private void OnMic(double level) => _overlay?.SetVoiceLevel(level);

    /// <summary>What's being said, for anyone looking at the screen. While a book is read, where in the book.</summary>
    private void OnSentence(int index)
    {
        if (speech.IsSpeaking)
        {
            ShowVoice();
        }
    }

    private void ShowVoice()
    {
        if (Book?.Invoke() is { } book)
        {
            _overlay?.Show(CompanionState.Reading, new CompanionContent
            {
                BookPlace = $"Page {book.Page} of {book.Pages}",
                BookProgress = book.Pages == 0 ? 0 : (double)book.Page / book.Pages,
            });
            return;
        }

        _overlay?.Show(CompanionState.Speaking, Caption(speech.History.Current?.Text));
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
            ShowVoice();
        }
        else
        {
            _overlay?.Show(_busy ? CompanionState.Thinking : CompanionState.Idle);
        }
    }
}
