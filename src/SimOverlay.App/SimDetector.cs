using SimOverlay.Core;
using SimOverlay.Core.Events;
using SimOverlay.Sim.Contracts;
using Timer = System.Threading.Timer;

namespace SimOverlay.App;

/// <summary>
/// Polls registered <see cref="ISimProvider"/> instances every 2 seconds and
/// starts / stops the active provider as the sim process appears or disappears.
/// Only one provider is active at a time.
/// <para>
/// State changes from the active provider are forwarded to <see cref="ISimDataBus"/>
/// as <see cref="SimStateChangedEvent"/> so overlays can react without a direct
/// dependency on <see cref="ISimProvider"/>.
/// </para>
/// </summary>
public sealed class SimDetector : IDisposable
{
    private readonly ISimDataBus _bus;
    private readonly IReadOnlyList<ISimProvider> _providers;
    private readonly Timer _timer;

    private ISimProvider? _activeProvider;
    private int  _disconnectStrikes;   // consecutive IsRunning()==false counts
    private bool _disposed;

    // Stop the provider only after this many consecutive false IsRunning() results.
    // At a 2-second poll interval this is 4 seconds — enough to ignore SDK hiccups
    // while still reacting quickly to an actual sim exit (ISSUE-014).
    private const int DisconnectThreshold = 2;

    /// <summary>
    /// Fires when the active provider changes (null = no sim running).
    /// Raised on a ThreadPool thread.
    /// </summary>
    public event Action<ISimProvider?>? ActiveProviderChanged;

    public SimDetector(ISimDataBus bus, IReadOnlyList<ISimProvider> providers)
        : this(bus, providers, TimeSpan.FromSeconds(2)) { }

    // Internal constructor lets tests disable the timer by passing Timeout.InfiniteTimeSpan.
    internal SimDetector(ISimDataBus bus, IReadOnlyList<ISimProvider> providers, TimeSpan pollInterval)
    {
        _bus       = bus;
        _providers = providers;

        _timer = pollInterval == Timeout.InfiniteTimeSpan
            ? new Timer(Poll, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan)
            : new Timer(Poll, null, TimeSpan.Zero, pollInterval);
    }

    // Exposed as internal so SimDetectorTests can drive it synchronously.
    internal void Poll(object? state = null)
    {
        if (_disposed)
            return;

        // If we have an active provider, check whether it's still running.
        if (_activeProvider is not null)
        {
            if (!_activeProvider.IsRunning())
            {
                _disconnectStrikes++;
                if (_disconnectStrikes < DisconnectThreshold)
                {
                    AppLog.Info($"SimDetector: '{_activeProvider.SimId}' IsRunning()==false " +
                                $"(strike {_disconnectStrikes}/{DisconnectThreshold}) — waiting before stopping.");
                    return;
                }

                var stoppedId = _activeProvider.SimId;
                _activeProvider.StateChanged -= OnProviderStateChanged;
                _activeProvider.Stop();
                _activeProvider = null;
                _disconnectStrikes = 0;
                AppLog.Info($"SimDetector: provider transition: '{stoppedId}' → (none)");
                _bus.Publish(new SimStateChangedEvent(SimState.Disconnected));
                ActiveProviderChanged?.Invoke(null);
            }
            else
            {
                _disconnectStrikes = 0;
            }

            return; // don't switch providers mid-session
        }

        // Scan for the first available provider in priority order.
        foreach (var provider in _providers)
        {
            try
            {
                if (!provider.IsRunning())
                    continue;

                AppLog.Info($"SimDetector: provider transition: (none) → '{provider.SimId}'");
                _activeProvider = provider;
                _activeProvider.StateChanged += OnProviderStateChanged;
                _activeProvider.Start();
                ActiveProviderChanged?.Invoke(_activeProvider);
                return;
            }
            catch (Exception ex)
            {
                AppLog.Exception($"SimDetector: error checking '{provider.SimId}'", ex);
            }
        }
    }

    private void OnProviderStateChanged(SimState state)
    {
        AppLog.Info($"SimDetector: state → {state}");
        _bus.Publish(new SimStateChangedEvent(state));
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();

        if (_activeProvider is not null)
        {
            _activeProvider.StateChanged -= OnProviderStateChanged;
            _activeProvider.Stop();
            _activeProvider = null;
        }
    }
}
