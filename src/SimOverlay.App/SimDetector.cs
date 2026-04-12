using SimOverlay.Core;
using SimOverlay.Core.Events;
using SimOverlay.Sim.Contracts;
using Timer = System.Threading.Timer;

namespace SimOverlay.App;

/// <summary>
/// Polls all registered <see cref="ISimProvider"/> instances every
/// <see cref="PollInterval"/> and starts / stops providers as sims appear
/// or disappear.  Only one provider is active at a time; priority order is
/// determined by the order of the <paramref name="providers"/> list.
/// <para>
/// Handles every startup-order scenario:
/// overlay starts before sim, sim starts before overlay, or the user
/// closes one sim and opens a different one mid-session.
/// </para>
/// </summary>
public sealed class SimDetector : IDisposable
{
    /// <summary>How often all providers are polled.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Consecutive IsRunning()==false results required before the active
    /// provider is stopped.  At 10-second intervals this is 20 seconds —
    /// long enough to ignore brief SDK hiccups without feeling sluggish.
    /// </summary>
    private const int DisconnectThreshold = 2;

    private readonly ISimDataBus _bus;
    private readonly IReadOnlyList<ISimProvider> _providers;
    private readonly Timer _timer;

    private ISimProvider? _activeProvider;
    private int  _disconnectStrikes;
    private bool _disposed;

    /// <summary>
    /// Fires when the active provider changes (null = no sim running).
    /// Raised on a ThreadPool thread.
    /// </summary>
    public event Action<ISimProvider?>? ActiveProviderChanged;

    public SimDetector(ISimDataBus bus, IReadOnlyList<ISimProvider> providers)
        : this(bus, providers, PollInterval) { }

    // Internal constructor lets tests disable the timer by passing Timeout.InfiniteTimeSpan.
    internal SimDetector(ISimDataBus bus, IReadOnlyList<ISimProvider> providers, TimeSpan pollInterval)
    {
        _bus       = bus;
        _providers = providers;

        _timer = pollInterval == Timeout.InfiniteTimeSpan
            ? new Timer(Poll, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan)
            : new Timer(Poll, null, TimeSpan.Zero, pollInterval);
    }

    // ── Poll ──────────────────────────────────────────────────────────────────

    // Exposed as internal so SimDetectorTests can drive it synchronously.
    internal void Poll(object? state = null)
    {
        if (_disposed) return;

        // Check all providers in priority order; the first one that reports
        // IsRunning() is the "preferred" provider for this tick.
        ISimProvider? preferred = null;
        foreach (var p in _providers)
        {
            try
            {
                if (p.IsRunning()) { preferred = p; break; }
            }
            catch (Exception ex)
            {
                AppLog.Exception($"SimDetector: error checking '{p.SimId}'", ex);
            }
        }

        // ── Nothing changed ───────────────────────────────────────────────────
        if (preferred == _activeProvider)
        {
            _disconnectStrikes = 0;
            return;
        }

        // ── Sim switch: different sim now takes priority ───────────────────────
        // (e.g. user closed LMU and opened iRacing, or vice-versa)
        if (preferred != null && _activeProvider != null)
        {
            AppLog.Info(
                $"SimDetector: sim switch — '{_activeProvider.SimId}' → '{preferred.SimId}'");
            StopActive();
            StartProvider(preferred);
            return;
        }

        // ── Active sim stopped, no other sim available ────────────────────────
        if (preferred == null && _activeProvider != null)
        {
            _disconnectStrikes++;
            if (_disconnectStrikes < DisconnectThreshold)
            {
                AppLog.Info(
                    $"SimDetector: '{_activeProvider.SimId}' IsRunning()==false " +
                    $"(strike {_disconnectStrikes}/{DisconnectThreshold}) — waiting.");
                return;
            }

            AppLog.Info($"SimDetector: '{_activeProvider.SimId}' confirmed stopped.");
            StopActive();
            return;
        }

        // ── New sim detected, nothing was active ──────────────────────────────
        if (preferred != null)
        {
            _disconnectStrikes = 0;
            StartProvider(preferred);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void StartProvider(ISimProvider provider)
    {
        try
        {
            AppLog.Info($"SimDetector: starting '{provider.SimId}'");
            _activeProvider = provider;
            _activeProvider.StateChanged += OnProviderStateChanged;
            _activeProvider.Start();
            ActiveProviderChanged?.Invoke(_activeProvider);
        }
        catch (Exception ex)
        {
            AppLog.Exception($"SimDetector: error starting '{provider.SimId}'", ex);
            _activeProvider = null;
        }
    }

    private void StopActive()
    {
        if (_activeProvider == null) return;
        var id = _activeProvider.SimId;
        _activeProvider.StateChanged -= OnProviderStateChanged;
        _activeProvider.Stop();
        _activeProvider = null;
        _disconnectStrikes = 0;
        AppLog.Info($"SimDetector: stopped '{id}'");
        _bus.Publish(new SimStateChangedEvent(SimState.Disconnected));
        ActiveProviderChanged?.Invoke(null);
    }

    private void OnProviderStateChanged(SimState state)
    {
        AppLog.Info($"SimDetector: state → {state}");
        _bus.Publish(new SimStateChangedEvent(state));
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
        StopActive();
    }
}
