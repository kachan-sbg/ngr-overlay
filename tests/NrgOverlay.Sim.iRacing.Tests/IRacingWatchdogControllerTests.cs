using NrgOverlay.Sim.iRacing;

namespace NrgOverlay.Sim.iRacing.Tests;

public class IRacingWatchdogControllerTests
{
    [Fact]
    public void Evaluate_NoConnectedSession_DoesNotRequestRestart()
    {
        var watchdog = new IRacingWatchdogController(stallTicksThreshold: 2);

        _ = watchdog.Evaluate(currentFrame: 10, hasConnected: false, simConnected: true, restartDisabled: false);
        var tick2 = watchdog.Evaluate(currentFrame: 10, hasConnected: false, simConnected: true, restartDisabled: false);
        var tick3 = watchdog.Evaluate(currentFrame: 10, hasConnected: false, simConnected: true, restartDisabled: false);

        Assert.False(tick2.ShouldRestart);
        Assert.False(tick3.ShouldRestart);
        Assert.False(tick2.ShouldLogStall);
        Assert.False(tick3.ShouldLogStall);
    }

    [Fact]
    public void Evaluate_StallAfterConnected_RequestsRestartAtThreshold()
    {
        var watchdog = new IRacingWatchdogController(stallTicksThreshold: 2);

        _ = watchdog.Evaluate(currentFrame: 100, hasConnected: true, simConnected: true, restartDisabled: false); // baseline
        var tick1 = watchdog.Evaluate(currentFrame: 100, hasConnected: true, simConnected: true, restartDisabled: false);
        var tick2 = watchdog.Evaluate(currentFrame: 100, hasConnected: true, simConnected: true, restartDisabled: false);

        Assert.True(tick1.ShouldLogStall);
        Assert.Equal(1, tick1.StallCount);
        Assert.False(tick1.ShouldRestart);

        Assert.True(tick2.ShouldLogStall);
        Assert.Equal(2, tick2.StallCount);
        Assert.True(tick2.ShouldRestart);
    }

    [Fact]
    public void Evaluate_RestartDisabled_SuppressesAndResets()
    {
        var watchdog = new IRacingWatchdogController(stallTicksThreshold: 2);

        _ = watchdog.Evaluate(currentFrame: 7, hasConnected: true, simConnected: true, restartDisabled: true); // baseline
        _ = watchdog.Evaluate(currentFrame: 7, hasConnected: true, simConnected: true, restartDisabled: true); // stall 1
        var suppressed = watchdog.Evaluate(currentFrame: 7, hasConnected: true, simConnected: true, restartDisabled: true); // stall 2
        var next = watchdog.Evaluate(currentFrame: 7, hasConnected: true, simConnected: true, restartDisabled: true);

        Assert.True(suppressed.RestartSuppressed);
        Assert.False(suppressed.ShouldRestart);
        Assert.True(next.ShouldLogStall);
        Assert.Equal(1, next.StallCount);
    }

    [Fact]
    public void Evaluate_ConcurrentTicks_ReservesSingleRestart()
    {
        var watchdog = new IRacingWatchdogController(stallTicksThreshold: 1);
        _ = watchdog.Evaluate(currentFrame: 42, hasConnected: true, simConnected: true, restartDisabled: false); // baseline

        var restartRequests = 0;
        Parallel.For(0, 64, _ =>
        {
            var result = watchdog.Evaluate(
                currentFrame: 42,
                hasConnected: true,
                simConnected: true,
                restartDisabled: false);
            if (result.ShouldRestart)
                Interlocked.Increment(ref restartRequests);
        });

        Assert.Equal(1, restartRequests);
    }

    [Fact]
    public void CompleteRestartCycle_AllowsFutureRestart()
    {
        var watchdog = new IRacingWatchdogController(stallTicksThreshold: 1);

        _ = watchdog.Evaluate(currentFrame: 9, hasConnected: true, simConnected: true, restartDisabled: false); // baseline
        var first = watchdog.Evaluate(currentFrame: 9, hasConnected: true, simConnected: true, restartDisabled: false);
        Assert.True(first.ShouldRestart);

        watchdog.CompleteRestartCycle();

        _ = watchdog.Evaluate(currentFrame: 9, hasConnected: true, simConnected: true, restartDisabled: false); // new baseline
        var second = watchdog.Evaluate(currentFrame: 9, hasConnected: true, simConnected: true, restartDisabled: false);
        Assert.True(second.ShouldRestart);
    }
}

