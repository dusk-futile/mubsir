using Mubsir.App.Native;
using Mubsir.Core.Motion;
using Mubsir.Overlay;

namespace Mubsir.App.Host;

/// <summary>
/// <c>Mubsir.exe --overlay-demo</c>: runs the notch and Hudhud on their own, over the real desktop, through
/// the same story as the HTML prototype. Nothing is listened to, sent or clicked; it only shows the visuals.
/// Ctrl stops the checklist and undoes it, or wakes Hudhud. Clicking Hudhud opens the panel. Clicking the
/// notch plays the story again. Esc quits.
/// </summary>
internal static class OverlayDemo
{
    private const int VkControl = 0x11;
    private const string Heard = "Open Windows Update and check for updates";
    private const string Said = "Settings is open. There are 3 updates ready. Shall I install them?";
    private const string SaidArabic = "فتحت الإعدادات. فيه ٣ تحديثات جاهزة، أنزّلهم؟";

    private static readonly string[] Steps = ["Open Settings", "Go to Windows Update", "Check for updates", "Read what was found"];

    public static int Run()
    {
        ConsoleIo.Attach();
        ConsoleIo.WriteLine("Mubsir overlay demo. Ctrl stops and undoes, or wakes Hudhud. Click Hudhud to open the panel, or the notch to replay. Esc quits.");
        try
        {
            using OverlayHost overlay = OverlayHost.Start();
            overlay.Failed += e => ConsoleIo.WriteLine("overlay error: " + e);
            new Script(overlay).Run();
            return 0;
        }
        catch (InvalidOperationException e)
        {
            ConsoleIo.WriteLine("overlay could not start: " + (e.InnerException ?? e).GetType().Name);
            return 1;
        }
    }

    private sealed class Script
    {
        private readonly OverlayHost _overlay;
        private bool _ctrlDown;
        private bool _escDown;
        private bool _exit;
        private volatile bool _stopPressed;
        private volatile bool _tapPressed;
        private volatile bool _cancelPressed;
        private volatile bool _replay;
        private bool _ctrlPressed;
        private long _rateFrom = Environment.TickCount64;
        private long _rateFrames;

        public Script(OverlayHost overlay)
        {
            _overlay = overlay;
            overlay.UseDemoVoice();
            overlay.NotchClicked += () => _replay = true;
            overlay.ActionChosen += action =>
            {
                ConsoleIo.WriteLine("Panel: " + action);
                if (action == CompanionAction.Talk)
                {
                    _replay = true;
                }
            };
            overlay.StopRequested += () => _stopPressed = true;
            overlay.ConfirmTapRequested += () => _tapPressed = true;
            overlay.CancelRequested += () => _cancelPressed = true;
        }

        public void Run()
        {
            Wait(1.5);
            while (!_exit)
            {
                PlayOnce();
                Rest();
            }
        }

        private void PlayOnce()
        {
            // As in the prototype, Ctrl stops whatever is happening. Only the checklist has something to undo.
            bool finished = Listening()
                && Stage(CompanionState.Thinking, null, 1.5)
                && Doing()
                && Pointing()
                && Speaking()
                && Stage(
                    CompanionState.Reading,
                    new CompanionContent
                    {
                        BookTitle = "Introducing Phonology",
                        BookPlace = "Chapter 3 · page 34 of 312",
                        BookHint = "Say “next page”, “repeat” or ask about this page",
                        BookProgress = 34.0 / 312,
                    },
                    3);
            if (finished && !_exit)
            {
                Payment();
            }

            if (!_exit)
            {
                Show(CompanionState.Idle);
            }
        }

        /// <summary>Shows a state for a while. Returns false if it was stopped or the demo is ending.</summary>
        private bool Stage(CompanionState state, CompanionContent? content, double seconds)
        {
            _overlay.Show(state, content);
            return !WaitOrStop(seconds) && !_exit;
        }

        /// <summary>Waits with nothing to do: Hudhud falls asleep on its own, and Ctrl wakes it.</summary>
        private void Rest()
        {
            _replay = false;
            while (!_exit && !_replay)
            {
                Wait(0.1);
                if (ConsumeCtrl())
                {
                    _overlay.WakeHudhud();
                }
            }
        }

        private bool Listening()
        {
            string[] words = Heard.Split(' ');
            for (int i = 1; i <= words.Length; i++)
            {
                if (!Stage(CompanionState.Listening, new CompanionContent { Transcript = string.Join(' ', words[..i]) }, 0.21))
                {
                    return false;
                }
            }

            return !WaitOrStop(1.2) && !_exit;
        }

        /// <summary>Each step spins while it runs and is ticked only once it is finished. Returns false if stopped.</summary>
        private bool Doing()
        {
            var states = new StepState[Steps.Length];
            ShowSteps(states, DoingMode.Running);
            if (WaitOrStop(0.32))
            {
                Undo(states);
                return false;
            }

            for (int i = 0; i < states.Length && !_exit; i++)
            {
                states[i] = StepState.Running;
                ShowSteps(states, DoingMode.Running);
                if (WaitOrStop(0.78))
                {
                    Undo(states);
                    return false;
                }

                states[i] = StepState.Done;
                ShowSteps(states, DoingMode.Running);
                if (WaitOrStop(0.17))
                {
                    Undo(states);
                    return false;
                }
            }

            ShowSteps(states, DoingMode.Done);
            if (WaitOrStop(1.3))
            {
                Undo(states);
                return false;
            }

            return true;
        }

        /// <summary>Ctrl: undo what was finished, last step first, then say it is all back.</summary>
        private void Undo(StepState[] states)
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] == StepState.Running)
                {
                    states[i] = StepState.Undone;
                }
            }

            ShowSteps(states, DoingMode.Undoing);
            Wait(0.22);
            for (int i = states.Length - 1; i >= 0 && !_exit; i--)
            {
                if (states[i] != StepState.Done)
                {
                    continue;
                }

                states[i] = StepState.Undoing;
                ShowSteps(states, DoingMode.Undoing);
                Wait(0.44);
                states[i] = StepState.Undone;
                ShowSteps(states, DoingMode.Undoing);
                Wait(0.14);
            }

            ShowSteps(states, DoingMode.Stopped);
            Wait(0.5);
            Show(CompanionState.Undone);
            Wait(2.2);
            Show(CompanionState.Idle);
        }

        private void ShowSteps(StepState[] states, DoingMode mode)
        {
            var steps = new CompanionStep[states.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = new CompanionStep(Steps[i], states[i]);
            }

            _overlay.Show(CompanionState.Doing, new CompanionContent { Steps = steps, Mode = mode });
        }

        /// <summary>The pointer flies out of the notch to a spot on the screen and names it.</summary>
        private bool Pointing()
        {
            _overlay.PointAt(
                User32.GetSystemMetrics(Win32.SM_CXSCREEN) * 0.34,
                User32.GetSystemMetrics(Win32.SM_CYSCREEN) * 0.46,
                "Install all");
            return Stage(CompanionState.Pointing, new CompanionContent { PointText = "It's the Install all button" }, 2.6);
        }

        private bool Speaking()
        {
            int words = Said.Split(' ').Length;
            for (int i = 0; i <= words; i++)
            {
                if (!Stage(CompanionState.Speaking, new CompanionContent { Caption = Said, CaptionWordsSpoken = i }, 0.19))
                {
                    return false;
                }
            }

            return Stage(CompanionState.Speaking, new CompanionContent { Caption = Said, CaptionArabic = SaidArabic }, 2.6);
        }

        /// <summary>Four presses approve it. They happen on their own after a moment, or press Ctrl yourself.</summary>
        private void Payment()
        {
            double[] autoTaps = [1.4, 1.75, 2.05, 2.35];
            long start = Environment.TickCount64;
            int taps = 0;
            ShowPayment(taps);
            _tapPressed = false;
            _cancelPressed = false;
            while (taps < 4 && !_exit)
            {
                Wait(0.03);
                if (_cancelPressed)
                {
                    Show(CompanionState.Idle);
                    return;
                }

                double elapsed = (Environment.TickCount64 - start) / 1000.0;
                bool tapped = ConsumeCtrl() || _tapPressed || elapsed >= autoTaps[taps];
                _tapPressed = false;
                if (tapped)
                {
                    taps++;
                    ShowPayment(taps);
                    _overlay.NudgeNotch();
                }
            }

            Wait(0.3);
            _overlay.Show(CompanionState.Paid, new CompanionContent { PaidText = "Paid EGP 250 · Electricity" });
            Wait(3.2);
        }

        private void ShowPayment(int taps) => _overlay.Show(CompanionState.Confirm, new CompanionContent
        {
            Amount = "EGP 250.00",
            Site = "pay.example.com",
            Facts = "Electricity bill, October · Visa ending 4417",
            Taps = taps,
        });

        private void Show(CompanionState state) => _overlay.Show(state);

        private bool WaitOrStop(double seconds)
        {
            long end = Environment.TickCount64 + (long)(seconds * 1000);
            while (Environment.TickCount64 < end && !_exit)
            {
                Poll();
                if (ConsumeCtrl() || _stopPressed)
                {
                    _stopPressed = false;
                    return true;
                }

                Thread.Sleep(15);
            }

            return false;
        }

        private void Wait(double seconds)
        {
            long end = Environment.TickCount64 + (long)(seconds * 1000);
            while (Environment.TickCount64 < end && !_exit)
            {
                Poll();
                Thread.Sleep(15);
            }
        }

        private bool ConsumeCtrl()
        {
            bool pressed = _ctrlPressed;
            _ctrlPressed = false;
            return pressed;
        }

        private void Poll()
        {
            // The frame rate, for checking the plan's performance budget on a real PC.
            long now = Environment.TickCount64;
            if (now - _rateFrom >= 5000)
            {
                long frames = _overlay.FramesDrawn;
                ConsoleIo.WriteLine(Math.Round((frames - _rateFrames) * 1000.0 / (now - _rateFrom)) + " frames a second");
                _rateFrom = now;
                _rateFrames = frames;
            }

            bool ctrl = (User32.GetAsyncKeyState(VkControl) & 0x8000) != 0;
            if (ctrl && !_ctrlDown)
            {
                _ctrlPressed = true;
            }

            _ctrlDown = ctrl;
            bool esc = (User32.GetAsyncKeyState(Win32.VK_ESCAPE) & 0x8000) != 0;
            if (esc && !_escDown)
            {
                _exit = true;
            }

            _escDown = esc;
        }
    }
}
