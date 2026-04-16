using System.Threading;

namespace NrgOverlay.Sim.iRacing;

internal readonly record struct WatchdogTickResult(
    bool ShouldLogStall,
    int StallCount,
    bool RestartSuppressed,
    bool ShouldRestart);

/// <summary>
/// Thread-safe watchdog state machine used by IRacingPoller.
/// It collapses concurrent timer callbacks into a single restart request.
/// </summary>
internal sealed class IRacingWatchdogController
{
    private readonly object _sync = new();
    private readonly int _stallTicksThreshold;
    private int _prevFrame = -1;
    private int _stallCount;
    private int _restartReserved;

    public IRacingWatchdogController(int stallTicksThreshold)
    {
        _stallTicksThreshold = Math.Max(1, stallTicksThreshold);
    }

    public WatchdogTickResult Evaluate(
        int currentFrame,
        bool hasConnected,
        bool simConnected,
        bool restartDisabled)
    {
        lock (_sync)
        {
            if (_prevFrame < 0)
            {
                _prevFrame = currentFrame;
                _stallCount = 0;
                return default;
            }

            if (currentFrame != _prevFrame)
            {
                _prevFrame = currentFrame;
                _stallCount = 0;
                return default;
            }

            if (!hasConnected)
            {
                _prevFrame = currentFrame;
                return default;
            }

            if (!simConnected)
            {
                _stallCount = 0;
                return default;
            }

            _stallCount++;

            if (_stallCount < _stallTicksThreshold)
            {
                return new WatchdogTickResult(
                    ShouldLogStall: true,
                    StallCount: _stallCount,
                    RestartSuppressed: false,
                    ShouldRestart: false);
            }

            if (restartDisabled)
            {
                _stallCount = 0;
                _prevFrame = currentFrame;
                return new WatchdogTickResult(
                    ShouldLogStall: true,
                    StallCount: _stallTicksThreshold,
                    RestartSuppressed: true,
                    ShouldRestart: false);
            }

            var reserved = Interlocked.CompareExchange(ref _restartReserved, 1, 0) == 0;
            return new WatchdogTickResult(
                ShouldLogStall: true,
                StallCount: _stallCount,
                RestartSuppressed: false,
                ShouldRestart: reserved);
        }
    }

    public void CompleteRestartCycle()
    {
        lock (_sync)
        {
            _prevFrame = -1;
            _stallCount = 0;
        }
        Interlocked.Exchange(ref _restartReserved, 0);
    }
}

