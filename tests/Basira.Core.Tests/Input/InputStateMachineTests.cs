using Basira.Core.Common;
using Basira.Core.Input;

namespace Basira.Core.Tests.Input;

public class InputStateMachineTests
{
    // ---------- a) injected events ----------

    [Fact]
    public void InjectedKeys_AreIgnoredCompletely()
    {
        var h = new InputHarness();

        KeyDecision d = h.Down(K.RightCtrl, injected: true);
        Assert.False(d.Swallow);
        Assert.Empty(d.Actions);
        Assert.Empty(h.Up(K.RightCtrl, injected: true).Actions);

        // An injected double tap opens nothing.
        h.Down(K.RightCtrl, injected: true);
        h.Up(K.RightCtrl, injected: true);
        h.Down(K.RightCtrl, injected: true);
        h.Up(K.RightCtrl, injected: true);
        Assert.False(h.Machine.LayerOpen);
    }

    [Fact]
    public void InjectedKeys_DoNotStopOrPauseAnAgentTask()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);

        KeyDecision esc = h.Down(K.Esc, injected: true);
        KeyDecision a = h.Down(K.A, injected: true);

        Assert.False(esc.Swallow);
        Assert.Empty(esc.Actions);
        Assert.Empty(a.Actions);
        Assert.True(h.Machine.AgentRunning);
        Assert.False(h.Machine.TaskPaused);
    }

    [Fact]
    public void InjectedKeys_DoNotCancelATalk()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);
        h.Wait(100);

        Assert.Empty(h.Down(K.A, injected: true).Actions);
        ActionAssert.Has<TalkStarted>(h.Wait(250));
        ActionAssert.Sequence(h.Up(K.RightCtrl).Actions, typeof(TalkEnded));
    }

    [Fact]
    public void InjectedEnter_DoesNotConfirm()
    {
        var h = new InputHarness();
        h.Machine.SetConfirmationPending(true, h.Clock.NowMs - 1000);

        KeyDecision d = h.Down(K.Enter, injected: true);

        Assert.False(d.Swallow);
        Assert.Empty(d.Actions);
        Assert.True(h.Machine.ConfirmationPending);
    }

    // ---------- b) Ctrl always stops speech ----------

    [Fact]
    public void LeftCtrl_StopsSpeechAndPassesThrough()
    {
        var h = new InputHarness();

        KeyDecision down = h.Down(K.LeftCtrl);
        KeyDecision up = h.Up(K.LeftCtrl);

        Assert.False(down.Swallow);
        ActionAssert.Sequence(down.Actions, typeof(StopSpeech));
        Assert.False(up.Swallow);
        Assert.Empty(up.Actions);
    }

    [Fact]
    public void RightCtrl_StopsSpeechEvenWhenNotTheTalkKey()
    {
        var h = new InputHarness(new InputSettings { TalkKey = K.Apps, LayerKey = K.Apps });

        KeyDecision down = h.Down(K.RightCtrl);

        Assert.False(down.Swallow);
        ActionAssert.Sequence(down.Actions, typeof(StopSpeech));
    }

    [Fact]
    public void AutoRepeat_DoesNotRetrigger()
    {
        var h = new InputHarness();
        h.Down(K.LeftCtrl);
        h.Skip(500);

        for (int i = 0; i < 5; i++)
        {
            h.Skip(33);
            KeyDecision repeat = h.Down(K.LeftCtrl);
            Assert.Empty(repeat.Actions);
            Assert.False(repeat.Swallow);
        }

        h.Up(K.LeftCtrl);
        ActionAssert.Sequence(h.Down(K.LeftCtrl).Actions, typeof(StopSpeech));
    }

    [Fact]
    public void KeyDownLongAfterALostKeyUp_CountsAsANewPress()
    {
        var h = new InputHarness();
        h.Down(K.LeftCtrl);
        h.Skip(5000); // its key-up was lost (hook dropped)

        ActionAssert.Sequence(h.Down(K.LeftCtrl).Actions, typeof(StopSpeech));
    }

    [Fact]
    public void Ctrl_StopsSpeechInEveryMode_AndIsNeverSwallowed()
    {
        var confirm = new InputHarness();
        confirm.Machine.SetConfirmationPending(true, 0);
        KeyDecision c = confirm.Down(K.LeftCtrl);
        Assert.False(c.Swallow);
        ActionAssert.Sequence(c.Actions, typeof(StopSpeech));

        var agent = new InputHarness();
        agent.Machine.SetAgentRunning(true);
        KeyDecision a = agent.Down(K.LeftCtrl);
        Assert.False(a.Swallow);
        ActionAssert.Sequence(a.Actions, typeof(StopSpeech));
        Assert.False(agent.Machine.TaskPaused);

        var paused = new InputHarness();
        paused.Machine.SetBasiraPaused(true);
        KeyDecision p = paused.Down(K.LeftCtrl);
        Assert.False(p.Swallow);
        ActionAssert.Sequence(p.Actions, typeof(StopSpeech));
    }

    // ---------- c) talk key ----------

    [Fact]
    public void TalkKeyDown_StartsPrerollAtOnce_AndIsNotSwallowed()
    {
        var h = new InputHarness();

        KeyDecision d = h.Down(K.RightCtrl);

        Assert.False(d.Swallow);
        ActionAssert.Sequence(d.Actions, typeof(StopSpeech), typeof(TalkPreroll));
    }

    [Fact]
    public void HoldPastThreshold_StartsTalk_AndReleaseEndsIt()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);

        Assert.Empty(h.Wait(250));
        ActionAssert.Sequence(h.Wait(50), typeof(TalkStarted));
        Assert.True(h.Machine.IsTalking);
        Assert.Empty(h.Wait(2000));

        KeyDecision up = h.Up(K.RightCtrl);
        Assert.False(up.Swallow);
        ActionAssert.Sequence(up.Actions, typeof(TalkEnded));
        Assert.False(h.Machine.IsTalking);
    }

    [Fact]
    public void HoldIsDetectedOnTheReleaseEvent_WhenNoTickRan()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);
        h.Skip(500);

        ActionAssert.Sequence(h.Up(K.RightCtrl).Actions, typeof(TalkStarted), typeof(TalkEnded));
    }

    [Fact]
    public void HoldIsDetectedOnAnAutoRepeatEvent()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);
        h.Skip(320);

        KeyDecision repeat = h.Down(K.RightCtrl);

        Assert.False(repeat.Swallow);
        ActionAssert.Sequence(repeat.Actions, typeof(TalkStarted));
    }

    [Fact]
    public void ReleaseBeforeThreshold_CancelsSilently()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);
        h.Wait(200);

        ActionAssert.Sequence(h.Up(K.RightCtrl).Actions, typeof(TalkCancelled));
        Assert.Empty(h.Wait(1000));
    }

    [Fact]
    public void OtherKeyDuringHold_CancelsTalk_AndThatKeyPassesThrough()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);
        h.Wait(100);

        KeyDecision c = h.Down(K.C); // slow Ctrl+C
        Assert.False(c.Swallow);
        ActionAssert.Sequence(c.Actions, typeof(TalkCancelled));
        Assert.False(h.Up(K.C).Swallow);

        Assert.Empty(h.Wait(1000)); // no TalkStarted for a cancelled press
        Assert.Empty(h.Up(K.RightCtrl).Actions);
    }

    [Fact]
    public void OtherKeyAfterTalkStarted_CancelsTheTalk()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);
        ActionAssert.Has<TalkStarted>(h.Wait(400));

        ActionAssert.Sequence(h.Down(K.A).Actions, typeof(TalkCancelled));
        Assert.Empty(h.Up(K.RightCtrl).Actions);
    }

    [Fact]
    public void ModifierPressedDuringHold_AlsoCancels()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);

        ActionAssert.Sequence(h.Down(K.Shift).Actions, typeof(TalkCancelled));
    }

    [Fact]
    public void CancelledPress_DoesNotCountAsATap()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);
        h.Down(K.C);
        h.Up(K.C);
        h.Up(K.RightCtrl);
        h.Wait(50);

        KeyDecision second = h.Tap(K.RightCtrl).Up;

        ActionAssert.Lacks<LayerOpened>(second.Actions);
        Assert.False(h.Machine.LayerOpen);
    }

    [Fact]
    public void HeldModifier_DoesNotBlockTalk()
    {
        var h = new InputHarness();
        h.Down(K.Shift);

        ActionAssert.Sequence(h.Down(K.RightCtrl).Actions, typeof(StopSpeech), typeof(TalkPreroll));
    }

    [Fact]
    public void HeldNonModifierKey_BlocksTalk()
    {
        var h = new InputHarness();
        h.Down(K.A);

        KeyDecision d = h.Down(K.RightCtrl);

        ActionAssert.Sequence(d.Actions, typeof(StopSpeech)); // Ctrl still stops speech
        Assert.Empty(h.Wait(600));
        Assert.Empty(h.Up(K.RightCtrl).Actions);
    }

    [Fact]
    public void NonModifierTalkKey_IsSwallowedDownAndUp()
    {
        var h = new InputHarness(new InputSettings { TalkKey = K.Apps, LayerKey = K.Apps });

        KeyDecision down = h.Down(K.Apps);
        Assert.True(down.Swallow);
        ActionAssert.Sequence(down.Actions, typeof(StopSpeech), typeof(TalkPreroll));

        h.Skip(320);
        KeyDecision repeat = h.Down(K.Apps);
        Assert.True(repeat.Swallow);
        ActionAssert.Sequence(repeat.Actions, typeof(TalkStarted));

        KeyDecision up = h.Up(K.Apps);
        Assert.True(up.Swallow);
        ActionAssert.Sequence(up.Actions, typeof(TalkEnded));
    }

    [Fact]
    public void ScrollLockTalkKey_TapIsSwallowedAndSilent()
    {
        var h = new InputHarness(new InputSettings { TalkKey = K.ScrollLock, LayerKey = K.RightCtrl });

        (KeyDecision down, _, KeyDecision up) = h.Tap(K.ScrollLock);

        Assert.True(down.Swallow);
        Assert.True(up.Swallow);
        ActionAssert.Sequence(up.Actions, typeof(TalkCancelled));
    }

    [Fact]
    public void NonModifierTalkKey_PassesThroughWhenAnotherKeyIsHeld()
    {
        var h = new InputHarness(new InputSettings { TalkKey = K.Apps, LayerKey = K.Apps });
        h.Down(K.A);

        KeyDecision down = h.Down(K.Apps);
        KeyDecision up = h.Up(K.Apps);

        Assert.False(down.Swallow);
        Assert.Empty(down.Actions);
        Assert.False(up.Swallow);
    }

    [Fact]
    public void VeryLongTalk_EndsByItself()
    {
        var h = new InputHarness(new InputSettings { MaxTalkMs = 5000 });
        h.Down(K.RightCtrl);

        List<InputAction> actions = h.Wait(6000);

        ActionAssert.Sequence(actions, typeof(TalkStarted), typeof(TalkEnded));
        Assert.Empty(h.Up(K.RightCtrl).Actions);
    }

    // ---------- d) double tap ----------

    [Fact]
    public void DoubleTap_OpensLayerOnSecondRelease()
    {
        var h = new InputHarness();

        (KeyDecision d1, _, KeyDecision u1) = h.Tap(K.RightCtrl);
        ActionAssert.Sequence(d1.Actions, typeof(StopSpeech), typeof(TalkPreroll));
        ActionAssert.Sequence(u1.Actions, typeof(TalkCancelled));
        Assert.Empty(h.Wait(100));

        KeyDecision d2 = h.Down(K.RightCtrl);
        ActionAssert.Sequence(d2.Actions, typeof(StopSpeech), typeof(TalkPreroll));
        Assert.False(h.Machine.LayerOpen);
        h.Wait(60);

        KeyDecision u2 = h.Up(K.RightCtrl);
        Assert.False(u2.Swallow);
        ActionAssert.Sequence(u2.Actions, typeof(TalkCancelled), typeof(LayerOpened));
        Assert.True(h.Machine.LayerOpen);
    }

    [Fact]
    public void SecondPressHeld_BecomesTalkNotLayer()
    {
        var h = new InputHarness();
        h.Tap(K.RightCtrl);
        h.Wait(100);

        h.Down(K.RightCtrl);
        ActionAssert.Sequence(h.Wait(400), typeof(TalkStarted));
        KeyDecision up = h.Up(K.RightCtrl);

        ActionAssert.Sequence(up.Actions, typeof(TalkEnded));
        Assert.False(h.Machine.LayerOpen);
    }

    [Fact]
    public void SecondTapOutsideWindow_DoesNotOpenLayer()
    {
        var h = new InputHarness();
        h.Tap(K.RightCtrl);
        h.Wait(450);

        ActionAssert.Lacks<LayerOpened>(h.Tap(K.RightCtrl).Up.Actions);
        Assert.False(h.Machine.LayerOpen);
    }

    [Fact]
    public void WindowRunsFromFirstKeyUpToSecondKeyDown()
    {
        var h = new InputHarness();
        h.Tap(K.RightCtrl, holdMs: 250); // a slow first tap is still a tap
        h.Wait(400); // exactly at the window edge

        ActionAssert.Has<LayerOpened>(h.Tap(K.RightCtrl, holdMs: 200).Up.Actions);
    }

    [Fact]
    public void KeyBetweenTaps_BreaksTheDoubleTap()
    {
        var h = new InputHarness();
        h.Tap(K.RightCtrl);
        h.Tap(K.A, holdMs: 20);

        ActionAssert.Lacks<LayerOpened>(h.Tap(K.RightCtrl).Up.Actions);
    }

    [Fact]
    public void ThirdTap_ClosesTheLayer()
    {
        var h = new InputHarness();
        h.OpenLayer();
        h.Wait(100);

        KeyDecision third = h.Down(K.RightCtrl);

        ActionAssert.Has<LayerClosed>(third.Actions);
        ActionAssert.Has<StopSpeech>(third.Actions);
        Assert.False(third.Swallow);
        Assert.False(h.Machine.LayerOpen);
    }

    [Fact]
    public void SeparateLayerKey_DoubleTapOpensLayer_AndHoldDoesNotTalk()
    {
        var h = new InputHarness(new InputSettings { TalkKey = K.Apps, LayerKey = K.RightCtrl });

        (KeyDecision d1, _, KeyDecision u1) = h.Tap(K.RightCtrl);
        ActionAssert.Sequence(d1.Actions, typeof(StopSpeech));
        Assert.False(d1.Swallow);
        Assert.Empty(u1.Actions);
        h.Wait(100);
        ActionAssert.Sequence(h.Tap(K.RightCtrl).Up.Actions, typeof(LayerOpened));

        var hold = new InputHarness(new InputSettings { TalkKey = K.Apps, LayerKey = K.RightCtrl });
        hold.Down(K.RightCtrl);
        Assert.Empty(hold.Wait(600));
        Assert.Empty(hold.Up(K.RightCtrl).Actions);
    }

    [Fact]
    public void SeparateLayerKey_SlowPressIsNotATap()
    {
        var h = new InputHarness(new InputSettings { TalkKey = K.Apps, LayerKey = K.RightCtrl });
        h.Tap(K.RightCtrl, holdMs: 350);
        h.Wait(100);

        ActionAssert.Lacks<LayerOpened>(h.Tap(K.RightCtrl).Up.Actions);
    }

    [Fact]
    public void NonModifierSeparateLayerKey_IsSwallowed()
    {
        var h = new InputHarness(new InputSettings { TalkKey = K.RightCtrl, LayerKey = K.ScrollLock });

        (KeyDecision d1, _, KeyDecision u1) = h.Tap(K.ScrollLock);
        h.Wait(100);
        (_, _, KeyDecision u2) = h.Tap(K.ScrollLock);

        Assert.True(d1.Swallow);
        Assert.True(u1.Swallow);
        Assert.True(u2.Swallow);
        ActionAssert.Sequence(u2.Actions, typeof(LayerOpened));
    }

    // ---------- e) command layer ----------

    public static TheoryData<string, LayerCommand> LayerKeys => new()
    {
        { nameof(K.Space), LayerCommand.ReadFromHere },
        { nameof(K.Left), LayerCommand.PreviousSentence },
        { nameof(K.Right), LayerCommand.NextSentence },
        { nameof(K.Up), LayerCommand.PreviousParagraph },
        { nameof(K.Down), LayerCommand.NextParagraph },
        { nameof(K.Period), LayerCommand.Repeat },
        { nameof(K.Enter), LayerCommand.TypeRequest },
        { nameof(K.NumpadEnter), LayerCommand.TypeRequest },
        { nameof(K.Esc), LayerCommand.StopTask },
        { nameof(K.F1), LayerCommand.Help },
    };

    [Theory]
    [MemberData(nameof(LayerKeys))]
    public void LayerKey_RunsItsCommand_IsSwallowed_AndClosesTheLayer(string keyName, LayerCommand expected)
    {
        KeyIdentity key = KeyByName(keyName);
        var h = new InputHarness();
        h.OpenLayer();

        KeyDecision down = h.Down(key);
        KeyDecision up = h.Up(key);

        Assert.True(down.Swallow);
        Assert.Equal(2, down.Actions.Count);
        Assert.Equal(new LayerKey(expected), down.Actions[0]);
        Assert.IsType<LayerClosed>(down.Actions[1]);
        Assert.True(up.Swallow);
        Assert.Empty(up.Actions);
        Assert.False(h.Machine.LayerOpen);
    }

    [Theory]
    [MemberData(nameof(LayerKeys))]
    public void LayerKeys_AreMatchedByPhysicalPosition_NotVirtualKey(string keyName, LayerCommand expected)
    {
        KeyIdentity key = KeyByName(keyName);
        var h = new InputHarness();
        h.OpenLayer();

        // On another layout the same physical key may report a different virtual key.
        KeyDecision down = h.Machine.OnKey(new KeyEvent(0xE2, key.ScanCode, key.Extended, true, false, h.Clock.NowMs));

        Assert.True(down.Swallow);
        Assert.Equal(new LayerKey(expected), down.Actions[0]);
    }

    [Fact]
    public void WhileReading_NavigationKeysKeepTheLayerOpen_AndRestartTheTimeout()
    {
        var h = new InputHarness();
        h.Machine.SetReading(true);
        h.OpenLayer();

        KeyDecision right = h.Down(K.Right);
        Assert.True(right.Swallow);
        Assert.Equal([new LayerKey(LayerCommand.NextSentence)], right.Actions);
        Assert.True(h.Up(K.Right).Swallow);
        Assert.True(h.Machine.LayerOpen);

        Assert.Empty(h.Wait(2500));
        Assert.Equal([new LayerKey(LayerCommand.NextParagraph)], h.Down(K.Down).Actions);
        h.Up(K.Down);
        Assert.Equal([new LayerKey(LayerCommand.Repeat)], h.Down(K.Period).Actions);
        h.Up(K.Period);
        Assert.Equal([new LayerKey(LayerCommand.PreviousSentence)], h.Down(K.Left).Actions);
        h.Up(K.Left);

        Assert.Empty(h.Wait(2950));
        ActionAssert.Sequence(h.Wait(100), typeof(LayerClosed));
    }

    [Theory]
    [InlineData(nameof(K.Enter), LayerCommand.TypeRequest)]
    [InlineData(nameof(K.Esc), LayerCommand.StopTask)]
    [InlineData(nameof(K.F1), LayerCommand.Help)]
    public void WhileReading_OtherCommandsStillCloseTheLayer(string keyName, LayerCommand expected)
    {
        var h = new InputHarness();
        h.Machine.SetReading(true);
        h.OpenLayer();

        KeyDecision d = h.Down(KeyByName(keyName));

        Assert.Equal(new LayerKey(expected), d.Actions[0]);
        ActionAssert.Has<LayerClosed>(d.Actions);
        Assert.False(h.Machine.LayerOpen);
    }

    [Fact]
    public void WhileReading_HeldArrowAutoRepeatDoesNotRepeatTheCommand()
    {
        var h = new InputHarness();
        h.Machine.SetReading(true);
        h.OpenLayer();
        h.Down(K.Right);

        h.Skip(500);
        KeyDecision repeat = h.Down(K.Right);

        Assert.True(repeat.Swallow);
        Assert.Empty(repeat.Actions);
    }

    [Fact]
    public void UnmappedKey_ClosesLayer_AndPassesThrough()
    {
        var h = new InputHarness();
        h.OpenLayer();

        KeyDecision down = h.Down(K.A);
        KeyDecision up = h.Up(K.A);

        Assert.False(down.Swallow);
        ActionAssert.Sequence(down.Actions, typeof(LayerClosed));
        Assert.False(up.Swallow);
        Assert.False(h.Machine.LayerOpen);
    }

    [Fact]
    public void NumpadArrow_IsNotALayerKey()
    {
        var h = new InputHarness();
        h.OpenLayer();

        KeyDecision d = h.Down(K.NumpadLeft);

        Assert.False(d.Swallow);
        ActionAssert.Sequence(d.Actions, typeof(LayerClosed));
    }

    [Fact]
    public void CtrlInLayer_ClosesItAndStopsSpeech_WithoutSwallowing()
    {
        var h = new InputHarness();
        h.OpenLayer();

        KeyDecision d = h.Down(K.LeftCtrl);

        Assert.False(d.Swallow);
        ActionAssert.Sequence(d.Actions, typeof(StopSpeech), typeof(LayerClosed));
    }

    [Fact]
    public void TalkKeyInLayer_ClosesIt_AndCanStillBecomeATalk()
    {
        var h = new InputHarness();
        h.OpenLayer();
        h.Wait(100);

        KeyDecision d = h.Down(K.RightCtrl);
        Assert.False(d.Swallow);
        ActionAssert.Sequence(d.Actions, typeof(StopSpeech), typeof(LayerClosed), typeof(TalkPreroll));
        ActionAssert.Sequence(h.Wait(400), typeof(TalkStarted));
        ActionAssert.Sequence(h.Up(K.RightCtrl).Actions, typeof(TalkEnded));
    }

    [Fact]
    public void TalkKeyTapInLayer_IsNotCountedTowardsAnotherDoubleTap()
    {
        var h = new InputHarness();
        h.OpenLayer();
        h.Wait(50);
        h.Tap(K.RightCtrl); // closes the layer
        h.Wait(100);

        ActionAssert.Lacks<LayerOpened>(h.Tap(K.RightCtrl).Up.Actions);
    }

    [Fact]
    public void Layer_TimesOutWithoutAKey()
    {
        var h = new InputHarness();
        h.OpenLayer();

        Assert.Empty(h.Wait(2950));
        ActionAssert.Sequence(h.Wait(100), typeof(LayerClosed));
        Assert.False(h.Machine.LayerOpen);

        KeyDecision space = h.Down(K.Space);
        Assert.False(space.Swallow);
        Assert.Empty(space.Actions);
    }

    [Fact]
    public void LayerTimeout_IsDetectedOnTheNextKeyWhenNoTickRan()
    {
        var h = new InputHarness();
        h.OpenLayer();
        h.Skip(3500);

        KeyDecision space = h.Down(K.Space);

        Assert.False(space.Swallow);
        ActionAssert.Sequence(space.Actions, typeof(LayerClosed));
    }

    // ---------- f) agent running ----------

    [Fact]
    public void Agent_TalkKeyTapStopsTheTask()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);

        (KeyDecision down, _, KeyDecision up) = h.Tap(K.RightCtrl, holdMs: 100);

        Assert.False(down.Swallow);
        ActionAssert.Sequence(down.Actions, typeof(StopSpeech), typeof(TalkPreroll));
        Assert.False(up.Swallow);
        ActionAssert.Sequence(up.Actions, typeof(TalkCancelled), typeof(TaskStop));
        Assert.False(h.Machine.AgentRunning);
    }

    [Fact]
    public void Agent_EscStopsTheTask_AndIsSwallowedWithItsKeyUp()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);

        KeyDecision down = h.Down(K.Esc);
        KeyDecision up = h.Up(K.Esc);

        Assert.True(down.Swallow);
        ActionAssert.Sequence(down.Actions, typeof(TaskStop));
        Assert.True(up.Swallow);
        Assert.Empty(up.Actions);
    }

    [Fact]
    public void Agent_OtherKeyPausesTask_PassesThrough_ThenEnterResumes()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);

        KeyDecision tab = h.Down(K.Tab);
        Assert.False(tab.Swallow);
        ActionAssert.Sequence(tab.Actions, typeof(TaskPause));
        Assert.True(h.Machine.TaskPaused);
        Assert.False(h.Up(K.Tab).Swallow);

        KeyDecision another = h.Down(K.A);
        Assert.False(another.Swallow);
        Assert.Empty(another.Actions);
        h.Up(K.A);

        KeyDecision enter = h.Down(K.Enter);
        Assert.True(enter.Swallow);
        ActionAssert.Sequence(enter.Actions, typeof(TaskResume));
        Assert.True(h.Up(K.Enter).Swallow);
        Assert.False(h.Machine.TaskPaused);
        Assert.True(h.Machine.AgentRunning);

        ActionAssert.Sequence(h.Down(K.Space).Actions, typeof(TaskPause));
    }

    [Fact]
    public void Agent_WhilePaused_EscStopsAndTalkTapStops()
    {
        var esc = new InputHarness();
        esc.Machine.SetAgentRunning(true);
        esc.Down(K.A);
        esc.Up(K.A);
        KeyDecision d = esc.Down(K.Esc);
        Assert.True(d.Swallow);
        ActionAssert.Sequence(d.Actions, typeof(TaskStop));

        var tap = new InputHarness();
        tap.Machine.SetAgentRunning(true);
        tap.Machine.SetTaskPaused(true);
        ActionAssert.Has<TaskStop>(tap.Tap(K.RightCtrl).Up.Actions);
    }

    [Fact]
    public void Agent_ModifierKeysDoNotPause()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);

        Assert.Empty(h.Down(K.Shift).Actions);
        Assert.Empty(h.Down(K.Alt).Actions);
        Assert.False(h.Machine.TaskPaused);
    }

    [Fact]
    public void Agent_HoldingTalkKeyStillTalks()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);

        h.Down(K.RightCtrl);
        ActionAssert.Sequence(h.Wait(400), typeof(TalkStarted));
        ActionAssert.Sequence(h.Up(K.RightCtrl).Actions, typeof(TalkEnded));
        Assert.True(h.Machine.AgentRunning);
    }

    [Fact]
    public void Agent_PressLongerThanTapMaxButShorterThanHold_DoesNotStop()
    {
        var h = new InputHarness(new InputSettings { TapMaxMs = 150, HoldThresholdMs = 300 });
        h.Machine.SetAgentRunning(true);

        KeyDecision up = h.Tap(K.RightCtrl, holdMs: 200).Up;

        ActionAssert.Sequence(up.Actions, typeof(TalkCancelled));
        Assert.True(h.Machine.AgentRunning);
    }

    [Fact]
    public void Agent_DoubleTapDoesNotOpenTheLayer()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);

        h.DoubleTap(K.RightCtrl);

        Assert.False(h.Machine.LayerOpen);
    }

    [Fact]
    public void Agent_EscWhileTalkKeyHeld_CancelsTalkAndStops()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);
        h.Down(K.RightCtrl);

        KeyDecision esc = h.Down(K.Esc);

        Assert.True(esc.Swallow);
        ActionAssert.Sequence(esc.Actions, typeof(TalkCancelled), typeof(TaskStop));
    }

    // ---------- g) confirmation pending ----------

    [Fact]
    public void Confirm_EnterTooEarly_IsSwallowedAndIgnored()
    {
        var h = new InputHarness();
        h.Machine.SetConfirmationPending(true, h.Clock.NowMs + 2000);

        KeyDecision down = h.Down(K.Enter);
        KeyDecision up = h.Up(K.Enter);

        Assert.True(down.Swallow);
        Assert.Empty(down.Actions);
        Assert.True(up.Swallow);
        Assert.True(h.Machine.ConfirmationPending);
    }

    [Fact]
    public void Confirm_EnterIsAcceptedOnlyAfterTheIgnoreWindow()
    {
        var h = new InputHarness();
        long finished = h.Clock.NowMs;
        h.Machine.SetConfirmationPending(true, finished);

        h.Skip(399);
        KeyDecision early = h.Down(K.Enter);
        h.Up(K.Enter);
        Assert.Empty(early.Actions);

        h.Skip(1);
        KeyDecision onTime = h.Down(K.NumpadEnter);
        Assert.True(onTime.Swallow);
        ActionAssert.Sequence(onTime.Actions, typeof(ConfirmAccepted));
        Assert.True(h.Up(K.NumpadEnter).Swallow);
        Assert.False(h.Machine.ConfirmationPending);
    }

    [Fact]
    public void Confirm_HeldEnterAutoRepeat_NeverAccepts()
    {
        var h = new InputHarness();
        h.Machine.SetConfirmationPending(true, h.Clock.NowMs);
        h.Down(K.Enter); // too early

        for (int i = 0; i < 30; i++)
        {
            h.Skip(33);
            KeyDecision repeat = h.Down(K.Enter);
            Assert.True(repeat.Swallow);
            Assert.Empty(repeat.Actions);
        }

        Assert.True(h.Machine.ConfirmationPending);
    }

    [Fact]
    public void Confirm_EscRejects_PeriodRepeats()
    {
        var h = new InputHarness();
        h.Machine.SetConfirmationPending(true, 0);

        KeyDecision period = h.Down(K.Period);
        Assert.True(period.Swallow);
        ActionAssert.Sequence(period.Actions, typeof(ConfirmRepeat));
        Assert.True(h.Up(K.Period).Swallow);
        Assert.True(h.Machine.ConfirmationPending);

        KeyDecision esc = h.Down(K.Esc);
        Assert.True(esc.Swallow);
        ActionAssert.Sequence(esc.Actions, typeof(ConfirmRejected));
        Assert.True(h.Up(K.Esc).Swallow);
        Assert.False(h.Machine.ConfirmationPending);
    }

    [Fact]
    public void Confirm_OtherKeysPassThroughWithoutAction()
    {
        var h = new InputHarness();
        h.Machine.SetConfirmationPending(true, 0);

        foreach (KeyIdentity k in new[] { K.A, K.Space, K.Tab, K.Down, K.F1 })
        {
            KeyDecision d = h.Down(k);
            Assert.False(d.Swallow);
            Assert.Empty(d.Actions);
            Assert.False(h.Up(k).Swallow);
        }

        Assert.True(h.Machine.ConfirmationPending);
    }

    [Fact]
    public void Confirm_TalkKeyStillWorksForSpokenAnswers()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);
        h.Machine.SetConfirmationPending(true, 0);

        h.Down(K.RightCtrl);
        ActionAssert.Sequence(h.Wait(400), typeof(TalkStarted));
        ActionAssert.Sequence(h.Up(K.RightCtrl).Actions, typeof(TalkEnded));

        // A tap is just a cancelled talk: it does not stop the task under confirmation.
        ActionAssert.Sequence(h.Tap(K.RightCtrl).Up.Actions, typeof(TalkCancelled));
        Assert.True(h.Machine.AgentRunning);
    }

    // ---------- h) Basira paused ----------

    [Fact]
    public void Paused_TalkKeyGivesNoticeOnly()
    {
        var h = new InputHarness();
        h.Machine.SetBasiraPaused(true);

        KeyDecision down = h.Down(K.RightCtrl);
        Assert.False(down.Swallow);
        ActionAssert.Sequence(down.Actions, typeof(StopSpeech), typeof(PausedNotice));
        Assert.Empty(h.Wait(800)); // a hold never starts capture
        Assert.Empty(h.Up(K.RightCtrl).Actions);
    }

    [Fact]
    public void Paused_NonModifierTalkKeyIsSwallowed()
    {
        var h = new InputHarness(new InputSettings { TalkKey = K.Apps, LayerKey = K.Apps });
        h.Machine.SetBasiraPaused(true);

        KeyDecision down = h.Down(K.Apps);

        Assert.True(down.Swallow);
        ActionAssert.Sequence(down.Actions, typeof(PausedNotice));
        Assert.True(h.Up(K.Apps).Swallow);
    }

    [Fact]
    public void Paused_EnterSoonAfterNotice_Resumes()
    {
        var h = new InputHarness();
        h.Machine.SetBasiraPaused(true);
        h.Tap(K.RightCtrl);
        h.Wait(2000);

        KeyDecision enter = h.Down(K.Enter);

        Assert.True(enter.Swallow);
        ActionAssert.Sequence(enter.Actions, typeof(ResumeFromPaused));
        Assert.True(h.Up(K.Enter).Swallow);
        Assert.False(h.Machine.BasiraPaused);

        // Resumed: the talk key works again.
        ActionAssert.Has<TalkPreroll>(h.Down(K.RightCtrl).Actions);
    }

    [Fact]
    public void Paused_EnterAfterTheWindowOrWithoutNotice_PassesThrough()
    {
        var late = new InputHarness();
        late.Machine.SetBasiraPaused(true);
        late.Tap(K.RightCtrl);
        late.Wait(3100);
        KeyDecision d = late.Down(K.Enter);
        Assert.False(d.Swallow);
        Assert.Empty(d.Actions);
        Assert.True(late.Machine.BasiraPaused);

        var none = new InputHarness();
        none.Machine.SetBasiraPaused(true);
        Assert.False(none.Down(K.Enter).Swallow);
    }

    [Fact]
    public void Paused_DoubleTapDoesNotOpenTheLayer()
    {
        var h = new InputHarness();
        h.Machine.SetBasiraPaused(true);

        h.DoubleTap(K.RightCtrl);

        Assert.False(h.Machine.LayerOpen);
    }

    [Fact]
    public void PausingDuringATalk_CancelsIt()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);
        h.Wait(400);

        h.Machine.SetBasiraPaused(true);

        ActionAssert.Sequence(h.Machine.OnTick(), typeof(TalkCancelled));
        Assert.Empty(h.Up(K.RightCtrl).Actions);
    }

    // ---------- i) key explorer ----------

    [Fact]
    public void Explorer_DescribesEveryKey_AndSwallowsIt()
    {
        var h = new InputHarness();
        h.Machine.SetKeyExplorer(true);

        foreach (KeyIdentity k in new[] { K.A, K.Enter, K.Space, K.F1, K.Insert, K.Tab })
        {
            KeyDecision d = h.Down(k);
            Assert.True(d.Swallow);
            DescribeKey describe = Assert.IsType<DescribeKey>(Assert.Single(d.Actions));
            Assert.Equal(k, describe.Key.Identity);
            Assert.True(describe.Key.IsDown);
            Assert.True(h.Up(k).Swallow);
        }
    }

    [Fact]
    public void Explorer_CtrlIsDescribedToo_AndStillStopsSpeech()
    {
        var h = new InputHarness();
        h.Machine.SetKeyExplorer(true);

        KeyDecision d = h.Down(K.RightCtrl);

        Assert.True(d.Swallow);
        ActionAssert.Sequence(d.Actions, typeof(StopSpeech), typeof(DescribeKey));
        Assert.Empty(h.Wait(600)); // no talk in the explorer
        Assert.True(h.Up(K.RightCtrl).Swallow);
    }

    [Fact]
    public void Explorer_AutoRepeatIsNotDescribedAgain()
    {
        var h = new InputHarness();
        h.Machine.SetKeyExplorer(true);
        h.Down(K.A);
        h.Skip(500);

        KeyDecision repeat = h.Down(K.A);

        Assert.True(repeat.Swallow);
        Assert.Empty(repeat.Actions);
    }

    [Fact]
    public void Explorer_EscExits()
    {
        var h = new InputHarness();
        h.Machine.SetKeyExplorer(true);

        KeyDecision esc = h.Down(K.Esc);
        Assert.True(esc.Swallow);
        ActionAssert.Sequence(esc.Actions, typeof(KeyExplorerExited));
        Assert.False(h.Machine.KeyExplorer);
        Assert.True(h.Up(K.Esc).Swallow);

        KeyDecision a = h.Down(K.A);
        Assert.False(a.Swallow);
        Assert.Empty(a.Actions);
    }

    // ---------- j) precedence and swallow bookkeeping ----------

    [Fact]
    public void Precedence_ExplorerBeatsConfirmation()
    {
        var h = new InputHarness();
        h.Machine.SetConfirmationPending(true, 0);
        h.Machine.SetKeyExplorer(true);

        ActionAssert.Sequence(h.Down(K.Enter).Actions, typeof(DescribeKey));
        Assert.True(h.Machine.ConfirmationPending);
    }

    [Fact]
    public void Precedence_ConfirmationBeatsAgent()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);
        h.Machine.SetConfirmationPending(true, 0);

        Assert.Empty(h.Down(K.A).Actions); // no TaskPause
        h.Up(K.A);
        ActionAssert.Sequence(h.Down(K.Esc).Actions, typeof(ConfirmRejected)); // not TaskStop
        Assert.True(h.Machine.AgentRunning);
        Assert.False(h.Machine.TaskPaused);
    }

    [Fact]
    public void Precedence_AgentBeatsLayer()
    {
        var h = new InputHarness();
        h.OpenLayer();

        h.Machine.SetAgentRunning(true);
        Assert.False(h.Machine.LayerOpen);
        ActionAssert.Sequence(h.Machine.OnTick(), typeof(LayerClosed));

        KeyDecision space = h.Down(K.Space);
        Assert.False(space.Swallow);
        ActionAssert.Sequence(space.Actions, typeof(TaskPause));
    }

    [Fact]
    public void Precedence_ConfirmationAndExplorerCloseTheLayer()
    {
        var c = new InputHarness();
        c.OpenLayer();
        c.Machine.SetConfirmationPending(true, 0);
        ActionAssert.Sequence(c.Machine.OnTick(), typeof(LayerClosed));

        var e = new InputHarness();
        e.OpenLayer();
        e.Machine.SetKeyExplorer(true);
        ActionAssert.Sequence(e.Down(K.A).Actions, typeof(LayerClosed), typeof(DescribeKey));
    }

    [Fact]
    public void KeyUp_IsNeverSwallowedWhenItsKeyDownPassed()
    {
        var h = new InputHarness();
        Assert.False(h.Down(K.Enter).Swallow);

        h.Machine.SetConfirmationPending(true, 0);

        Assert.False(h.Up(K.Enter).Swallow);
    }

    [Fact]
    public void KeyUp_IsSwallowedWhenItsKeyDownWas_EvenAfterTheModeChanged()
    {
        var h = new InputHarness();
        h.Machine.SetConfirmationPending(true, 0);
        Assert.True(h.Down(K.Enter).Swallow);
        h.Machine.SetConfirmationPending(false, 0);

        Assert.True(h.Up(K.Enter).Swallow);
        Assert.False(h.Up(K.Enter).Swallow); // a stray second key-up is not
    }

    [Fact]
    public void KeyUpWithoutKeyDown_PassesThrough()
    {
        var h = new InputHarness();

        KeyDecision up = h.Up(K.Esc);

        Assert.False(up.Swallow);
        Assert.Empty(up.Actions);
    }

    // ---------- settings, reset, diagnostics ----------

    [Fact]
    public void UpdateSettings_ChangesTheTalkKey_AndCancelsAPressOfTheOldOne()
    {
        var h = new InputHarness();
        h.Down(K.RightCtrl);
        h.Wait(400);

        h.Machine.UpdateSettings(new InputSettings { TalkKey = K.Apps, LayerKey = K.Apps });

        ActionAssert.Sequence(h.Machine.OnTick(), typeof(TalkCancelled));
        Assert.Empty(h.Up(K.RightCtrl).Actions);
        ActionAssert.Sequence(h.Down(K.RightCtrl).Actions, typeof(StopSpeech));
        h.Up(K.RightCtrl);
        Assert.True(h.Down(K.Apps).Swallow);
    }

    [Fact]
    public void CustomTimings_AreUsed()
    {
        var h = new InputHarness(new InputSettings { HoldThresholdMs = 600, LayerTimeoutMs = 1000 });
        h.Down(K.RightCtrl);
        Assert.Empty(h.Wait(550));
        ActionAssert.Sequence(h.Wait(50), typeof(TalkStarted));
        h.Up(K.RightCtrl);

        h.Wait(100);
        h.OpenLayer();
        Assert.Empty(h.Wait(950));
        ActionAssert.Sequence(h.Wait(50), typeof(LayerClosed));
    }

    [Fact]
    public void ResetKeyState_ForgetsHeldKeys_AndCancelsTalk()
    {
        var h = new InputHarness();
        h.Down(K.LeftCtrl);
        h.Down(K.A);

        h.Machine.ResetKeyState();
        Assert.Empty(h.Machine.OnTick());

        ActionAssert.Sequence(h.Down(K.RightCtrl).Actions, typeof(StopSpeech), typeof(TalkPreroll));
        h.Wait(400);
        h.Machine.ResetKeyState();
        ActionAssert.Sequence(h.Machine.OnTick(), typeof(TalkCancelled));
    }

    [Fact]
    public void ToString_DescribesTheState()
    {
        var h = new InputHarness();
        h.Machine.SetAgentRunning(true);
        h.Down(K.RightCtrl);

        string s = h.Machine.ToString();

        Assert.Contains("talk=Pressed", s);
        Assert.Contains("agent=running", s);
        Assert.Contains("layer=closed", s);
        Assert.Contains("1D+", s);
    }

    [Fact]
    public void Trace_FormatsEventsAndDecisions()
    {
        var e = new KeyEvent(VirtualKeys.RCONTROL, ScanCodes.Ctrl, true, true, false, 1234);
        var d = new KeyDecision(false, [new StopSpeech(), new LayerKey(LayerCommand.Repeat)]);

        Assert.Equal("down sc=1D+ vk=A3 t=1234 -> pass [StopSpeech, LayerKey(Repeat)]", InputTrace.Format(e, d));
        Assert.Equal("swallow []", new KeyDecision(true, []).ToString());
    }

    [Fact]
    public void Constructor_RejectsBadArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new InputStateMachine(null!, new ManualClock()));
        Assert.Throws<ArgumentNullException>(() => new InputStateMachine(new InputSettings(), null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InputStateMachine(new InputSettings { HoldThresholdMs = -1 }, new ManualClock()));
        Assert.Throws<ArgumentException>(() => new InputStateMachine(new InputSettings { TalkKey = default }, new ManualClock()));
    }

    [Fact]
    public async Task ConcurrentCalls_AreSafe()
    {
        var clock = new ManualClock();
        var m = new InputStateMachine(new InputSettings(), clock);
        KeyIdentity[] keys = [K.RightCtrl, K.A, K.Space, K.Esc, K.Enter, K.LeftCtrl];

        Task[] workers = Enumerable.Range(0, 4).Select(w => Task.Run(() =>
        {
            for (int i = 0; i < 2000; i++)
            {
                KeyIdentity k = keys[(i + w) % keys.Length];
                m.OnKey(new KeyEvent(k.VirtualKey, k.ScanCode, k.Extended, i % 2 == 0, false, 0));
                m.OnTick();
                if (i % 97 == 0)
                {
                    m.SetAgentRunning(i % 2 == 0);
                    m.SetReading(i % 3 == 0);
                }
            }
        })).ToArray();

        await Task.WhenAll(workers);
        Assert.NotNull(m.ToString());
    }

    private static KeyIdentity KeyByName(string name) =>
        (KeyIdentity)typeof(K).GetField(name)!.GetValue(null)!;
}
