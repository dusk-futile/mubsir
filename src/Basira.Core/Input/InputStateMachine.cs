using System.Globalization;
using System.Text;
using Basira.Core.Common;

namespace Basira.Core.Input;

/// <summary>
/// The keyboard state machine. The Windows low-level hook calls <see cref="OnKey"/> for every key
/// event and must get an answer at once: whether to swallow the key (hide it from apps and NVDA)
/// and which actions to run. Pure logic: no I/O, no timers of its own; <see cref="OnTick"/> is
/// called about every 50 ms for time-based transitions. Thread-safe.
/// </summary>
/// <remarks>
/// Precedence: key explorer &gt; confirmation pending &gt; agent running &gt; command layer &gt; talk/tap.
/// A physical Ctrl key-down always emits <see cref="StopSpeech"/> and is never swallowed, except in
/// the key explorer, which swallows every key so the user can explore safely.
/// A key-up is swallowed exactly when its key-down was swallowed.
/// </remarks>
public sealed class InputStateMachine
{
    /// <summary>A "repeat" key-down arriving this long after the previous event of that key means its key-up was lost.</summary>
    private const long AutoRepeatStaleMs = 2000;

    private static readonly InputAction[] NoActions = [];
    private static readonly StopSpeech StopSpeechAction = new();
    private static readonly TalkPreroll TalkPrerollAction = new();
    private static readonly TalkStarted TalkStartedAction = new();
    private static readonly TalkEnded TalkEndedAction = new();
    private static readonly TalkCancelled TalkCancelledAction = new();
    private static readonly LayerOpened LayerOpenedAction = new();
    private static readonly LayerClosed LayerClosedAction = new();
    private static readonly TaskStop TaskStopAction = new();
    private static readonly TaskPause TaskPauseAction = new();
    private static readonly TaskResume TaskResumeAction = new();
    private static readonly ConfirmAccepted ConfirmAcceptedAction = new();
    private static readonly ConfirmRejected ConfirmRejectedAction = new();
    private static readonly ConfirmRepeat ConfirmRepeatAction = new();
    private static readonly PausedNotice PausedNoticeAction = new();
    private static readonly ResumeFromPaused ResumeFromPausedAction = new();
    private static readonly KeyExplorerExited KeyExplorerExitedAction = new();
    private static readonly LayerKey[] LayerKeyActions = CreateLayerKeyActions();

    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly Dictionary<int, PressedKey> _pressed = new();
    private readonly HashSet<int> _swallowed = new();
    private readonly List<InputAction> _scratch = new(4);
    private readonly List<InputAction> _pending = new(2);
    private InputSettings _settings;

    private TalkPhase _talkPhase;
    private int _talkId;
    private long _talkDownAt;
    private bool _talkTapEligible;
    private bool _talkIsLayerKey;
    private bool _talkLayerCandidate;

    // Press of a layer key that is not also the talk key.
    private bool _layerPressActive;
    private int _layerPressId;
    private long _layerPressDownAt;
    private bool _layerPressCandidate;
    private bool _layerPressCancelled;

    private long? _lastTapUpAt;
    private bool _layerOpen;
    private long _layerActivityAt;

    private bool _agentRunning;
    private bool _taskPaused;
    private bool _confirmPending;
    private long _promptFinishedAt;
    private bool _reading;
    private bool _basiraPaused;
    private long? _pausedNoticeAt;
    private bool _keyExplorer;

    public InputStateMachine(InputSettings settings, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);
        settings.Validate();
        _settings = settings;
        _clock = clock;
    }

    private enum TalkPhase
    {
        Idle,

        /// <summary>Talk key down, capture pre-rolling, hold not yet confirmed.</summary>
        Pressed,

        /// <summary>Hold confirmed; release sends the audio.</summary>
        Talking,

        /// <summary>The press was consumed or cancelled; its release does nothing.</summary>
        Ignored,
    }

    public InputSettings Settings
    {
        get { lock (_gate) { return _settings; } }
    }

    public bool LayerOpen
    {
        get { lock (_gate) { return _layerOpen; } }
    }

    public bool IsTalking
    {
        get { lock (_gate) { return _talkPhase == TalkPhase.Talking; } }
    }

    public bool AgentRunning
    {
        get { lock (_gate) { return _agentRunning; } }
    }

    public bool TaskPaused
    {
        get { lock (_gate) { return _agentRunning && _taskPaused; } }
    }

    public bool ConfirmationPending
    {
        get { lock (_gate) { return _confirmPending; } }
    }

    public bool BasiraPaused
    {
        get { lock (_gate) { return _basiraPaused; } }
    }

    public bool KeyExplorer
    {
        get { lock (_gate) { return _keyExplorer; } }
    }

    private bool IsNormalMode => !_keyExplorer && !_confirmPending && !_agentRunning && !_basiraPaused && !_layerOpen;

    /// <summary>Decides synchronously what to do with one key event.</summary>
    public KeyDecision OnKey(KeyEvent e)
    {
        if (e.IsInjected)
        {
            // Basira's own SendInput (and any other injected input) never drives the machine.
            return new KeyDecision(false, NoActions);
        }

        lock (_gate)
        {
            long now = _clock.NowMs;
            List<InputAction> actions = _scratch;
            actions.Clear();
            FlushPending(actions);
            CheckTimers(now, actions);

            int id = PhysicalId(e);
            bool swallow;
            if (!e.IsDown)
            {
                swallow = HandleUp(id, now, actions);
            }
            else if (_pressed.TryGetValue(id, out PressedKey held) && now - held.LastSeenMs <= AutoRepeatStaleMs)
            {
                // Auto-repeat of a held key: never re-triggers; mirrors the first key-down's swallow.
                _pressed[id] = held with { LastSeenMs = now };
                swallow = _swallowed.Contains(id);
            }
            else
            {
                bool modifier = VirtualKeys.IsModifier(e.VirtualKey);
                _pressed[id] = new PressedKey(modifier, now);
                swallow = HandleDown(e, id, modifier, now, actions);
                if (swallow)
                {
                    _swallowed.Add(id);
                }
                else
                {
                    _swallowed.Remove(id);
                }
            }

            return new KeyDecision(swallow, actions.Count == 0 ? NoActions : actions.ToArray());
        }
    }

    /// <summary>Time-based transitions (hold confirmed, layer timeout). Call about every 50 ms.</summary>
    public IReadOnlyList<InputAction> OnTick()
    {
        lock (_gate)
        {
            List<InputAction> actions = _scratch;
            actions.Clear();
            FlushPending(actions);
            CheckTimers(_clock.NowMs, actions);
            return actions.Count == 0 ? NoActions : actions.ToArray();
        }
    }

    /// <summary>An agent task started or ended. Starting one closes the command layer.</summary>
    public void SetAgentRunning(bool running)
    {
        lock (_gate)
        {
            if (running && !_agentRunning)
            {
                CloseLayerQuietly();
                _lastTapUpAt = null;
            }

            _agentRunning = running;
            if (!running)
            {
                _taskPaused = false;
            }
        }
    }

    public void SetTaskPaused(bool paused)
    {
        lock (_gate)
        {
            _taskPaused = paused;
        }
    }

    /// <summary>
    /// A confirmation prompt is waiting for Enter or Escape. Enter is accepted only from
    /// <paramref name="promptFinishedAtMs"/> + <see cref="InputSettings.ConfirmIgnoreMs"/> on (clock time).
    /// </summary>
    public void SetConfirmationPending(bool pending, long promptFinishedAtMs)
    {
        lock (_gate)
        {
            if (pending)
            {
                CloseLayerQuietly();
                _lastTapUpAt = null;
            }

            _confirmPending = pending;
            _promptFinishedAt = promptFinishedAtMs;
        }
    }

    /// <summary>Basira is reading (say-all or an answer): the layer then stays open for navigation keys.</summary>
    public void SetReading(bool reading)
    {
        lock (_gate)
        {
            _reading = reading;
        }
    }

    /// <summary>Basira paused by the user: the talk key only says how to resume.</summary>
    public void SetBasiraPaused(bool paused)
    {
        lock (_gate)
        {
            if (paused)
            {
                CloseLayerQuietly();
                CancelTalkQuietly();
                _lastTapUpAt = null;
                _layerPressActive = false;
            }
            else
            {
                _pausedNoticeAt = null;
            }

            _basiraPaused = paused;
        }
    }

    public void SetKeyExplorer(bool on)
    {
        lock (_gate)
        {
            if (on)
            {
                CloseLayerQuietly();
                CancelTalkQuietly();
                _lastTapUpAt = null;
                _layerPressActive = false;
            }

            _keyExplorer = on;
        }
    }

    public void UpdateSettings(InputSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);
        s.Validate();
        lock (_gate)
        {
            if (s.TalkKey != _settings.TalkKey)
            {
                CancelTalkQuietly();
            }

            if (s.LayerKey != _settings.LayerKey)
            {
                _layerPressActive = false;
                _lastTapUpAt = null;
            }

            _settings = s;
        }
    }

    /// <summary>
    /// Forget which keys are held (e.g. after the hook was re-installed or the session was locked,
    /// when key-ups may have been lost). Cancels a talk in progress.
    /// </summary>
    public void ResetKeyState()
    {
        lock (_gate)
        {
            CancelTalkQuietly();
            _talkPhase = TalkPhase.Idle;
            _layerPressActive = false;
            _lastTapUpAt = null;
            _pressed.Clear();
            _swallowed.Clear();
        }
    }

    /// <summary>One-line state dump for traces.</summary>
    public override string ToString()
    {
        lock (_gate)
        {
            var sb = new StringBuilder(160);
            var inv = CultureInfo.InvariantCulture;
            sb.Append("talk=").Append(_talkPhase);
            if (_talkPhase != TalkPhase.Idle)
            {
                sb.Append('@').Append(_talkDownAt.ToString(inv));
            }

            sb.Append(" layer=").Append(_layerOpen ? "open@" + _layerActivityAt.ToString(inv) : "closed");
            if (_lastTapUpAt is long tap)
            {
                sb.Append(" lastTap=").Append(tap.ToString(inv));
            }

            sb.Append(" agent=").Append(_agentRunning ? (_taskPaused ? "paused" : "running") : "off");
            sb.Append(" confirm=").Append(_confirmPending ? "pending@" + _promptFinishedAt.ToString(inv) : "off");
            sb.Append(" reading=").Append(_reading ? "yes" : "no");
            sb.Append(" basiraPaused=").Append(_basiraPaused ? "yes" : "no");
            sb.Append(" explorer=").Append(_keyExplorer ? "on" : "off");
            AppendIds(sb.Append(" pressed="), _pressed.Keys);
            AppendIds(sb.Append(" swallowed="), _swallowed);
            return sb.ToString();
        }
    }

    private static void AppendIds(StringBuilder sb, IEnumerable<int> ids)
    {
        sb.Append('[');
        bool first = true;
        foreach (int id in ids.Order())
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append(FormatId(id));
        }

        sb.Append(']');
    }

    private static string FormatId(int id) => (id & 0x10_0000) != 0
        ? "vk" + (id & 0xFFFF).ToString("X2", CultureInfo.InvariantCulture)
        : (id & 0xFFFF).ToString("X2", CultureInfo.InvariantCulture) + ((id & 0x1_0000) != 0 ? "+" : "");

    /// <summary>Physical key id: scan code plus extended flag, or the virtual key when there is no scan code.</summary>
    private static int PhysicalId(in KeyEvent e) => e.ScanCode != 0
        ? (e.ScanCode & 0xFFFF) | (e.Extended ? 0x1_0000 : 0)
        : 0x10_0000 | (e.VirtualKey & 0xFFFF);

    private static bool IsEsc(in KeyEvent e) => e.ScanCode == ScanCodes.Escape;

    private static bool IsEnter(in KeyEvent e) => e.ScanCode == ScanCodes.Enter; // main or numpad Enter

    private static bool IsPeriod(in KeyEvent e) => e.ScanCode == ScanCodes.Period && !e.Extended;

    private static LayerCommand? MapLayerCommand(in KeyEvent e) => (e.ScanCode, e.Extended) switch
    {
        (ScanCodes.Space, false) => LayerCommand.ReadFromHere,
        (ScanCodes.Left, true) => LayerCommand.PreviousSentence,
        (ScanCodes.Right, true) => LayerCommand.NextSentence,
        (ScanCodes.Up, true) => LayerCommand.PreviousParagraph,
        (ScanCodes.Down, true) => LayerCommand.NextParagraph,
        (ScanCodes.Period, false) => LayerCommand.Repeat,
        (ScanCodes.Enter, _) => LayerCommand.TypeRequest,
        (ScanCodes.Escape, _) => LayerCommand.StopTask,
        (ScanCodes.F1, _) => LayerCommand.Help,
        _ => null,
    };

    private static bool KeepsLayerOpenWhileReading(LayerCommand command) => command is LayerCommand.ReadFromHere
        or LayerCommand.PreviousSentence
        or LayerCommand.NextSentence
        or LayerCommand.PreviousParagraph
        or LayerCommand.NextParagraph
        or LayerCommand.Repeat;

    private static LayerKey[] CreateLayerKeyActions()
    {
        var all = Enum.GetValues<LayerCommand>();
        var result = new LayerKey[all.Length];
        foreach (LayerCommand c in all)
        {
            result[(int)c] = new LayerKey(c);
        }

        return result;
    }

    private static void AddOnce(List<InputAction> actions, InputAction action)
    {
        if (!actions.Contains(action))
        {
            actions.Add(action);
        }
    }

    private bool HandleDown(in KeyEvent e, int id, bool modifier, long now, List<InputAction> a)
    {
        bool isCtrl = VirtualKeys.IsCtrl(e.VirtualKey);
        bool isTalk = _settings.TalkKey.Matches(e);
        bool isLayerKey = _settings.LayerKey.Matches(e);

        InterruptPressesBy(id, a);
        if (!isLayerKey)
        {
            _lastTapUpAt = null; // another key between taps breaks a double-tap
        }

        if (_keyExplorer)
        {
            if (isCtrl)
            {
                a.Add(StopSpeechAction);
            }

            if (IsEsc(e))
            {
                _keyExplorer = false;
                a.Add(KeyExplorerExitedAction);
            }
            else
            {
                a.Add(new DescribeKey(e));
            }

            return true;
        }

        if (isCtrl)
        {
            a.Add(StopSpeechAction);
        }

        if (_confirmPending)
        {
            return HandleConfirmation(e, id, modifier, isTalk, isLayerKey, now, a);
        }

        if (_agentRunning)
        {
            return HandleAgent(e, id, modifier, isTalk, isLayerKey, now, a);
        }

        if (_layerOpen)
        {
            return HandleLayer(e, id, modifier, isTalk, isCtrl, isLayerKey, now, a);
        }

        if (_basiraPaused && IsEnter(e) && _pausedNoticeAt is long noticeAt && now - noticeAt <= _settings.PausedNoticeWindowMs)
        {
            _basiraPaused = false;
            _pausedNoticeAt = null;
            a.Add(ResumeFromPausedAction);
            return true;
        }

        if (isTalk)
        {
            return BeginTalkPress(id, modifier, isLayerKey, tapEligible: true, now, a);
        }

        if (isLayerKey)
        {
            return BeginLayerKeyPress(id, modifier, now);
        }

        return false;
    }

    private bool HandleConfirmation(in KeyEvent e, int id, bool modifier, bool isTalk, bool isLayerKey, long now, List<InputAction> a)
    {
        if (IsEnter(e))
        {
            // Keys pressed before the prompt finished (plus a grace period) are swallowed and ignored.
            if (_promptFinishedAt <= now - _settings.ConfirmIgnoreMs)
            {
                _confirmPending = false;
                a.Add(ConfirmAcceptedAction);
            }

            return true;
        }

        if (IsEsc(e))
        {
            _confirmPending = false;
            a.Add(ConfirmRejectedAction);
            return true;
        }

        if (IsPeriod(e))
        {
            a.Add(ConfirmRepeatAction);
            return true;
        }

        // The talk key still works so the user can answer by voice.
        return isTalk && BeginTalkPress(id, modifier, isLayerKey, tapEligible: true, now, a);
    }

    private bool HandleAgent(in KeyEvent e, int id, bool modifier, bool isTalk, bool isLayerKey, long now, List<InputAction> a)
    {
        if (isTalk)
        {
            // Tap stops the task (decided at release); a hold lets the user speak to it.
            return BeginTalkPress(id, modifier, isLayerKey, tapEligible: true, now, a);
        }

        if (IsEsc(e))
        {
            StopTask(a);
            return true;
        }

        if (_taskPaused)
        {
            if (IsEnter(e))
            {
                _taskPaused = false;
                a.Add(TaskResumeAction);
                return true;
            }

            return false;
        }

        if (!modifier)
        {
            // The user is doing something: pause, and let the key through.
            _taskPaused = true;
            a.Add(TaskPauseAction);
        }

        return false;
    }

    private bool HandleLayer(in KeyEvent e, int id, bool modifier, bool isTalk, bool isCtrl, bool isLayerKey, long now, List<InputAction> a)
    {
        if (isTalk)
        {
            // Closes the layer and may become a talk if held, but never counts as a tap.
            CloseLayer(a);
            return BeginTalkPress(id, modifier, isLayerKey, tapEligible: false, now, a);
        }

        if (isCtrl)
        {
            CloseLayer(a); // StopSpeech was already emitted; Ctrl passes through to NVDA.
            return false;
        }

        if (MapLayerCommand(e) is LayerCommand command)
        {
            a.Add(LayerKeyActions[(int)command]);
            if (_reading && KeepsLayerOpenWhileReading(command))
            {
                _layerActivityAt = now;
            }
            else
            {
                CloseLayer(a);
            }

            return true;
        }

        CloseLayer(a);
        // An unmapped key reaches the app, unless it is Basira's own dedicated (non-modifier) layer key.
        return isLayerKey && !modifier;
    }

    private bool BeginTalkPress(int id, bool modifier, bool isLayerKey, bool tapEligible, long now, List<InputAction> a)
    {
        if (_talkPhase is TalkPhase.Pressed or TalkPhase.Talking)
        {
            a.Add(TalkCancelledAction); // the previous press lost its key-up
        }

        _talkId = id;
        _talkDownAt = now;
        _talkIsLayerKey = isLayerKey;
        _talkTapEligible = false;
        _talkLayerCandidate = false;

        if (_basiraPaused)
        {
            _talkPhase = TalkPhase.Ignored;
            _pausedNoticeAt = now;
            a.Add(PausedNoticeAction);
            return !modifier;
        }

        if (OtherNonModifierHeld(id, now))
        {
            _talkPhase = TalkPhase.Ignored;
            return false;
        }

        AddOnce(a, StopSpeechAction);
        a.Add(TalkPrerollAction);
        _talkPhase = TalkPhase.Pressed;
        _talkTapEligible = tapEligible;
        _talkLayerCandidate = tapEligible && isLayerKey && IsNormalMode && IsWithinDoubleTap(now);
        _lastTapUpAt = null;
        return !modifier;
    }

    private bool BeginLayerKeyPress(int id, bool modifier, long now)
    {
        if (_basiraPaused)
        {
            return false;
        }

        _layerPressActive = true;
        _layerPressId = id;
        _layerPressDownAt = now;
        _layerPressCancelled = false;
        _layerPressCandidate = IsWithinDoubleTap(now);
        _lastTapUpAt = null;
        return !modifier;
    }

    private bool IsWithinDoubleTap(long now) => _lastTapUpAt is long t && now - t <= _settings.DoubleTapWindowMs;

    private bool HandleUp(int id, long now, List<InputAction> a)
    {
        bool swallow = _swallowed.Remove(id);
        _pressed.Remove(id);

        if (_talkPhase != TalkPhase.Idle && id == _talkId)
        {
            EndTalkPress(now, a);
        }
        else if (_layerPressActive && id == _layerPressId)
        {
            EndLayerKeyPress(now, a);
        }

        return swallow;
    }

    private void EndTalkPress(long now, List<InputAction> a)
    {
        TalkPhase phase = _talkPhase;
        _talkPhase = TalkPhase.Idle;

        if (phase == TalkPhase.Talking)
        {
            a.Add(TalkEndedAction);
            return;
        }

        if (phase != TalkPhase.Pressed)
        {
            return;
        }

        // Released before the hold threshold (CheckTimers ran first): a tap.
        a.Add(TalkCancelledAction);
        if (!_talkTapEligible || _keyExplorer || _confirmPending)
        {
            return;
        }

        if (_agentRunning)
        {
            if (now - _talkDownAt < _settings.TapMaxMs)
            {
                StopTask(a);
            }

            return;
        }

        if (!_talkIsLayerKey || _basiraPaused || _layerOpen)
        {
            return;
        }

        if (_talkLayerCandidate)
        {
            OpenLayer(now, a);
        }
        else
        {
            _lastTapUpAt = now;
        }
    }

    private void EndLayerKeyPress(long now, List<InputAction> a)
    {
        _layerPressActive = false;
        if (_layerPressCancelled || now - _layerPressDownAt >= _settings.TapMaxMs || !IsNormalMode)
        {
            return;
        }

        if (_layerPressCandidate)
        {
            OpenLayer(now, a);
        }
        else
        {
            _lastTapUpAt = now;
        }
    }

    /// <summary>A new key went down: it cancels an in-progress talk press and breaks a layer-key tap.</summary>
    private void InterruptPressesBy(int id, List<InputAction> a)
    {
        if (_talkPhase is TalkPhase.Pressed or TalkPhase.Talking && id != _talkId)
        {
            _talkPhase = TalkPhase.Ignored;
            a.Add(TalkCancelledAction);
        }

        if (_layerPressActive && id != _layerPressId)
        {
            _layerPressCancelled = true;
        }
    }

    private bool OtherNonModifierHeld(int id, long now)
    {
        foreach (KeyValuePair<int, PressedKey> entry in _pressed)
        {
            if (entry.Key != id && !entry.Value.Modifier && now - entry.Value.LastSeenMs <= AutoRepeatStaleMs)
            {
                return true;
            }
        }

        return false;
    }

    private void CheckTimers(long now, List<InputAction> a)
    {
        if (_talkPhase == TalkPhase.Pressed && now - _talkDownAt >= _settings.HoldThresholdMs)
        {
            _talkPhase = TalkPhase.Talking;
            a.Add(TalkStartedAction);
        }

        if (_talkPhase == TalkPhase.Talking && now - _talkDownAt >= _settings.MaxTalkMs)
        {
            _talkPhase = TalkPhase.Ignored;
            a.Add(TalkEndedAction);
        }

        if (_layerOpen && now - _layerActivityAt >= _settings.LayerTimeoutMs)
        {
            CloseLayer(a);
        }

        if (_lastTapUpAt is long tap && now - tap > _settings.DoubleTapWindowMs)
        {
            _lastTapUpAt = null;
        }

        if (_pausedNoticeAt is long notice && now - notice > _settings.PausedNoticeWindowMs)
        {
            _pausedNoticeAt = null;
        }
    }

    private void StopTask(List<InputAction> a)
    {
        // Optimistic: further keys must not pause a task that is already stopping.
        _agentRunning = false;
        _taskPaused = false;
        a.Add(TaskStopAction);
    }

    private void OpenLayer(long now, List<InputAction> a)
    {
        _layerOpen = true;
        _layerActivityAt = now;
        _lastTapUpAt = null;
        a.Add(LayerOpenedAction);
    }

    private void CloseLayer(List<InputAction> a)
    {
        if (_layerOpen)
        {
            _layerOpen = false;
            a.Add(LayerClosedAction);
        }
    }

    /// <summary>Close the layer from a setter; the app hears about it on the next OnKey/OnTick.</summary>
    private void CloseLayerQuietly() => CloseLayer(_pending);

    private void CancelTalkQuietly()
    {
        if (_talkPhase is TalkPhase.Pressed or TalkPhase.Talking)
        {
            _talkPhase = TalkPhase.Ignored;
            _pending.Add(TalkCancelledAction);
        }
    }

    private void FlushPending(List<InputAction> a)
    {
        if (_pending.Count > 0)
        {
            a.AddRange(_pending);
            _pending.Clear();
        }
    }

    private readonly record struct PressedKey(bool Modifier, long LastSeenMs);
}
