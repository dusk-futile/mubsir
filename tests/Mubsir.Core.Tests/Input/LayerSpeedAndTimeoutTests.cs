using Mubsir.Core.Common;
using Mubsir.Core.Input;

namespace Mubsir.Core.Tests.Input;

public class LayerSpeedAndTimeoutTests
{
    private sealed class TestClock : IClock
    {
        public long NowMs { get; set; } = 10_000;

        public DateTimeOffset UtcNow => DateTimeOffset.FromUnixTimeMilliseconds(NowMs);
    }

    private static readonly KeyIdentity J = new(0x4A, 0x24, false);
    private static readonly KeyIdentity MainPlus = new(VirtualKeys.OEM_PLUS, ScanCodes.EqualsKey, false);
    private static readonly KeyIdentity MainMinus = new(VirtualKeys.OEM_MINUS, ScanCodes.Minus, false);
    private static readonly KeyIdentity NumpadPlus = new(VirtualKeys.ADD, ScanCodes.NumpadPlus, false);
    private static readonly KeyIdentity NumpadMinus = new(VirtualKeys.SUBTRACT, ScanCodes.NumpadMinus, false);

    private static KeyDecision Send(InputStateMachine input, TestClock clock, KeyIdentity key, bool down) =>
        input.OnKey(new KeyEvent(key.VirtualKey, key.ScanCode, key.Extended, down, false, clock.NowMs));

    private static void OpenLayer(InputStateMachine input, TestClock clock)
    {
        // First tap: down and up within DoubleTapWindow
        Send(input, clock, KeyIdentity.RightCtrl, true);
        clock.NowMs += 50;
        Send(input, clock, KeyIdentity.RightCtrl, false);
        clock.NowMs += 50;

        // Second tap: down and up opens the layer
        Send(input, clock, KeyIdentity.RightCtrl, true);
        clock.NowMs += 50;
        var dec = Send(input, clock, KeyIdentity.RightCtrl, false);
        Assert.True(input.LayerOpen);
        Assert.Contains(dec.Actions, a => a is LayerOpened);
    }

    [Fact]
    public void Double_tap_right_ctrl_opens_layer()
    {
        var clock = new TestClock();
        var input = new InputStateMachine(InputSettings.Default, clock);
        OpenLayer(input, clock);
        Assert.True(input.LayerOpen);
    }

    [Fact]
    public void Plus_key_emits_faster_layer_command()
    {
        var clock = new TestClock();
        var input = new InputStateMachine(InputSettings.Default, clock);

        OpenLayer(input, clock);
        var plusDec = Send(input, clock, MainPlus, true);
        Send(input, clock, MainPlus, false);
        Assert.True(plusDec.Swallow);
        Assert.Contains(plusDec.Actions, a => a is LayerKey { Command: LayerCommand.Faster });
        Assert.False(input.LayerOpen); // Not reading: closes after command
    }

    [Fact]
    public void Minus_key_emits_slower_layer_command()
    {
        var clock = new TestClock();
        var input = new InputStateMachine(InputSettings.Default, clock);

        OpenLayer(input, clock);
        var minusDec = Send(input, clock, MainMinus, true);
        Send(input, clock, MainMinus, false);
        Assert.True(minusDec.Swallow);
        Assert.Contains(minusDec.Actions, a => a is LayerKey { Command: LayerCommand.Slower });
        Assert.False(input.LayerOpen);
    }

    [Fact]
    public void Numpad_plus_and_minus_keys_emit_speed_commands()
    {
        var clock = new TestClock();
        var input = new InputStateMachine(InputSettings.Default, clock);

        OpenLayer(input, clock);
        var numPlusDec = Send(input, clock, NumpadPlus, true);
        Send(input, clock, NumpadPlus, false);
        Assert.True(numPlusDec.Swallow);
        Assert.Contains(numPlusDec.Actions, a => a is LayerKey { Command: LayerCommand.Faster });

        clock.NowMs += 500;
        OpenLayer(input, clock);
        var numMinusDec = Send(input, clock, NumpadMinus, true);
        Send(input, clock, NumpadMinus, false);
        Assert.True(numMinusDec.Swallow);
        Assert.Contains(numMinusDec.Actions, a => a is LayerKey { Command: LayerCommand.Slower });
    }

    [Fact]
    public void Faster_and_slower_keep_layer_open_while_reading()
    {
        var clock = new TestClock();
        var input = new InputStateMachine(InputSettings.Default, clock);
        input.SetReading(true);

        OpenLayer(input, clock);
        var plusDec = Send(input, clock, MainPlus, true);
        Assert.Contains(plusDec.Actions, a => a is LayerKey { Command: LayerCommand.Faster });
        Assert.True(input.LayerOpen, "Layer should stay open while reading");

        clock.NowMs += 100;
        var minusDec = Send(input, clock, MainMinus, true);
        Assert.Contains(minusDec.Actions, a => a is LayerKey { Command: LayerCommand.Slower });
        Assert.True(input.LayerOpen, "Layer should stay open for subsequent speed keys while reading");
    }

    [Fact]
    public void Unused_key_closes_layer_and_passes_through()
    {
        var clock = new TestClock();
        var input = new InputStateMachine(InputSettings.Default, clock);

        OpenLayer(input, clock);
        Assert.True(input.LayerOpen);

        // Press 'J' key which is not mapped to any LayerCommand
        clock.NowMs += 100;
        var decision = Send(input, clock, J, true);

        Assert.False(decision.Swallow, "Unused key must pass through (not swallowed)");
        Assert.Contains(decision.Actions, a => a is LayerClosed);
        Assert.False(input.LayerOpen, "Layer must close when an unused key is pressed");
    }

    [Fact]
    public void Layer_closes_by_itself_after_timeout_with_layer_closed_action()
    {
        var clock = new TestClock();
        var settings = InputSettings.Default;
        var input = new InputStateMachine(settings, clock);

        OpenLayer(input, clock);
        Assert.True(input.LayerOpen);

        // Advance clock past LayerTimeoutMs
        clock.NowMs += settings.LayerTimeoutMs + 50;

        var tickActions = input.OnTick();
        Assert.False(input.LayerOpen, "Layer must close after timeout");
        Assert.Contains(tickActions, a => a is LayerClosed);
    }
}
