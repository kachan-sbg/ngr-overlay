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
    {
        _bus       = bus;
        _providers = providers;

        // Start immediately, then repeat every 2 seconds.
        _timer = new Timer(Poll, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    private void Poll(object? state)
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

                AppLog.Info($"SimDetector: '{_activeProvider.SimId}' stopped — resuming detection.");
                _activeProvider.StateChanged -= OnProviderStateChanged;
                _activeProvider.Stop();
                _activeProvider = null;
                _disconnectStrikes = 0;
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

                AppLog.Info($"SimDetector: '{provider.SimId}' detected — starting.");
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
