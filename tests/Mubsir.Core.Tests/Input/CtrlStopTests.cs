using Mubsir.Core.Common;
using Mubsir.Core.Input;

namespace Mubsir.Core.Tests.Input;

public class CtrlStopTests
{
    private static readonly KeyIdentity C = new(0x43, 0x2E, false);

    private static KeyDecision Key(InputStateMachine input, KeyIdentity key, bool down, bool injected = false) =>
        input.OnKey(new KeyEvent(key.VirtualKey, key.ScanCode, key.Extended, down, injected, Environment.TickCount64));

    private static InputStateMachine Running()
    {
        var input = new InputStateMachine(InputSettings.Default, SystemClock.Instance);
        input.SetAgentRunning(true);
        return input;
    }

    [Fact]
    public void Left_ctrl_alone_stops_the_task_when_released()
    {
        InputStateMachine input = Running();
        Assert.DoesNotContain(Key(input, KeyIdentity.LeftCtrl, true).Actions, a => a is TaskStop);
        Assert.Contains(Key(input, KeyIdentity.LeftCtrl, false).Actions, a => a is TaskStop);
    }

    [Fact]
    public void Ctrl_c_in_another_app_doesnt_stop_the_task()
    {
        InputStateMachine input = Running();
        var all = new List<InputAction>();
        all.AddRange(Key(input, KeyIdentity.LeftCtrl, true).Actions);
        all.AddRange(Key(input, C, true).Actions);
        all.AddRange(Key(input, C, false).Actions);
        all.AddRange(Key(input, KeyIdentity.LeftCtrl, false).Actions);
        Assert.DoesNotContain(all, a => a is TaskStop);
    }

    [Fact]
    public void A_key_sent_by_a_program_cant_stop_or_approve_anything()
    {
        InputStateMachine input = Running();
        Key(input, KeyIdentity.LeftCtrl, true, injected: true);
        Assert.DoesNotContain(Key(input, KeyIdentity.LeftCtrl, false, injected: true).Actions, a => a is TaskStop);
    }
}
