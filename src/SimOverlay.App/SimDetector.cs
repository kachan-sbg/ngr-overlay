using SimOverlay.Core;
using SimOverlay.Core.Events;
using SimOverlay.Sim.Contracts;
using Timer = System.Threading.Timer;

namespace SimOverlay.App;

/// <summary>
/// Polls all registered <see cref="ISimProvider"/> instances on every tick and manages
/// a single active provider lifecycle.
///
/// <para><b>Design rules</b></para>
/// <list type="bullet">
///   <item>All providers are polled on every tick regardless of which is active.</item>
///   <item><b>First-connect wins:</b> the first provider to become
///     <see cref="ProviderState.Available"/> is activated.  Once a provider is
///     <see cref="ProviderState.Active"/>, no other provider can take over — even if a
///     different sim starts while the active one is still running.  Switching only happens
///     naturally: the active sim disconnects, then the next available sim is activated.</item>
///   <item>Disconnect is debounced: the active provider must report
///     <see cref="ISimProvider.IsRunning"/> == <c>false</c> for
///     <see cref="DisconnectThreshold"/> consecutive ticks before it is stopped.  If it
///     comes back before the threshold is reached it recovers to
///     <see cref="ProviderState.Active"/> without a restart.</item>
///   <item>All transitions and the current state of every provider are observable at any
///     time via <see cref="ProviderStates"/>.</item>
/// </list>
/// </summary>
public sealed class SimDetector : IDisposable
{
    /// <summary>How often all providers are polled.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Consecutive <see cref="ISimProvider.IsRunning"/> == <c>false</c> results required
    /// before the active provider is stopped and a disconnect is published.
    /// At a 5-second interval this is 15 seconds — long enough to absorb brief SDK hiccups
    /// without feeling sluggish.
    /// </summary>
    private const int DisconnectThreshold = 3;

    private readonly ISimDataBus _bus;
    private readonly IReadOnlyList<ISimProvider> _providers;
    private readonly Dictionary<ISimProvider, ProviderState> _states = new();
    private readonly Dictionary<ISimProvider, int> _strikes = new();
    private readonly Timer _timer;

    // The one provider that is currently Active or Disconnecting.
    // Null when no provider has been activated.
    private ISimProvider? _activeProvider;

    // Last sim state seen from the active provider — broadcast as a heartbeat on every
    // poll tick so late subscribers (overlays created/subscribed after the initial event)
    // always catch up within one poll interval instead of staying stuck at Disconnected.
    private SimState _currentSimState = SimState.Disconnected;

    private bool _disposed;

    // ── Public surface ────────────────────────────────────────────────────────

    /// <summary>
    /// Fires when the active provider changes.
    /// <c>null</c> = no sim is currently active.
    /// Raised on a ThreadPool thread.
    /// </summary>
    public event Action<ISimProvider?>? ActiveProviderChanged;

    /// <summary>
    /// A point-in-time snapshot of the state of every registered provider,
    /// keyed by <see cref="ISimProvider.SimId"/>.
    /// Safe to call from any thread.
    /// </summary>
    public IReadOnlyDictionary<string, ProviderState> ProviderStates =>
        _states.ToDictionary(kv => kv.Key.SimId, kv => kv.Value);

    /// <summary>The SimId of the currently active provider, or <c>null</c>.</summary>
    public string? ActiveSimId => _activeProvider?.SimId;

    // ── Construction ──────────────────────────────────────────────────────────

    public SimDetector(ISimDataBus bus, IReadOnlyList<ISimProvider> providers)
        : this(bus, providers, PollInterval) { }

    // Internal constructor lets tests disable the automatic timer by passing
    // Timeout.InfiniteTimeSpan and driving Poll() synchronously.
    internal SimDetector(ISimDataBus bus, IReadOnlyList<ISimProvider> providers, TimeSpan pollInterval)
    {
        _bus       = bus;
        _providers = providers;

        foreach (var p in providers)
        {
            _states[p]  = ProviderState.Idle;
            _strikes[p] = 0;
        }

        _timer = pollInterval == Timeout.InfiniteTimeSpan
            ? new Timer(Poll, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan)
            : new Timer(Poll, null, TimeSpan.Zero, pollInterval);
    }

    // ── Poll ──────────────────────────────────────────────────────────────────

    // Exposed as internal so tests can drive it synchronously without any timer.
    internal void Poll(object? _ = null)
    {
        if (_disposed) return;

        // ── Step 1: update every provider's state based on IsRunning() ────────

        foreach (var provider in _providers)
        {
            bool running;
            try   { running = provider.IsRunning(); }
            catch (Exception ex)
            {
                AppLog.Exception($"SimDetector: IsRunning() threw for '{provider.SimId}'", ex);
                running = false;
            }

            TransitionState(provider, running);
        }

        // ── Step 2: if nothing is active, activate the first Available provider ─

        if (_activeProvider == null)
        {
            var candidate = _providers.FirstOrDefault(p => _states[p] == ProviderState.Available);
            if (candidate != null)
                Activate(candidate);
        }

        // ── Step 3: heartbeat — re-broadcast current state when a sim is active ──
        // This ensures any subscriber that missed the original transition event
        // (e.g. an overlay whose subscription was set up after the first poll fired)
        // catches up within one poll interval.  BaseOverlay only calls BringToFront()
        // when the state value actually changes, so repeated identical broadcasts are cheap.
        // We skip the heartbeat when Disconnected: overlays default to that state already,
        // and broadcasting Disconnected on every poll tick would trigger false-positive
        // "no disconnect event" assertions in tests (and confuse future diagnostics).
        if (_currentSimState != SimState.Disconnected)
            _bus.Publish(new SimStateChangedEvent(_currentSimState));
    }

    // ── State machine ─────────────────────────────────────────────────────────

    private void TransitionState(ISimProvider provider, bool running)
    {
        switch (_states[provider])
        {
            case ProviderState.Idle:
                if (running)
                {
                    _states[provider] = ProviderState.Available;
                    AppLog.Info($"SimDetector: '{provider.SimId}' → Available.");
                }
                break;

            case ProviderState.Available:
                if (!running)
                {
                    _states[provider] = ProviderState.Idle;
                    AppLog.Info($"SimDetector: '{provider.SimId}' → Idle (disappeared before activation).");
                }
                // Still Available — Step 2 will activate it if no other provider is active.
                break;

            case ProviderState.Active:
                if (!running)
                {
                    // First false read: enter Disconnecting with strike 1.
                    _strikes[provider] = 1;
                    _states[provider]  = ProviderState.Disconnecting;
                    AppLog.Info(
                        $"SimDetector: '{provider.SimId}' → Disconnecting " +
                        $"(strike 1/{DisconnectThreshold}).");
                }
                // Still running fine — no change.
                break;

            case ProviderState.Disconnecting:
                if (running)
                {
                    // Recovered — return to Active without restarting the poller.
                    _strikes[provider] = 0;
                    _states[provider]  = ProviderState.Active;
                    AppLog.Info($"SimDetector: '{provider.SimId}' → Active (recovered).");
                }
                else
                {
                    _strikes[provider]++;
                    if (_strikes[provider] >= DisconnectThreshold)
                    {
                        // Confirmed dead.
                        AppLog.Info(
                            $"SimDetector: '{provider.SimId}' confirmed disconnected " +
                            $"after {_strikes[provider]} strikes.");
                        Deactivate(provider);
                    }
                    else
                    {
                        AppLog.Info(
                            $"SimDetector: '{provider.SimId}' still Disconnecting " +
                            $"(strike {_strikes[provider]}/{DisconnectThreshold}).");
                    }
                }
                break;
        }
    }

    // ── Activate / Deactivate ─────────────────────────────────────────────────

    private void Activate(ISimProvider provider)
    {
        try
        {
            AppLog.Info($"SimDetector: activating '{provider.SimId}'.");
            _activeProvider    = provider;
            _states[provider]  = ProviderState.Active;
            _strikes[provider] = 0;

            provider.StateChanged += OnProviderStateChanged;
            provider.Start();

            ActiveProviderChanged?.Invoke(provider);
        }
        catch (Exception ex)
        {
            AppLog.Exception($"SimDetector: error activating '{provider.SimId}'", ex);
            // Roll back to Idle so the next poll can try again.
            provider.StateChanged -= OnProviderStateChanged;
            _states[provider]  = ProviderState.Idle;
            _activeProvider    = null;
        }
    }

    private void Deactivate(ISimProvider provider)
    {
        AppLog.Info($"SimDetector: deactivating '{provider.SimId}'.");
        provider.StateChanged -= OnProviderStateChanged;
        _states[provider]     = ProviderState.Idle;
        _strikes[provider]    = 0;
        _activeProvider       = null;
        _currentSimState      = SimState.Disconnected;

        try   { provider.Stop(); }
        catch (Exception ex) { AppLog.Exception($"SimDetector: error stopping '{provider.SimId}'", ex); }

        _bus.Publish(new SimStateChangedEvent(SimState.Disconnected));
        ActiveProviderChanged?.Invoke(null);
    }

    private void OnProviderStateChanged(SimState state)
    {
        AppLog.Info($"SimDetector: provider state → {state}");
        _currentSimState = state;
        _bus.Publish(new SimStateChangedEvent(state));
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
        if (_activeProvider != null)
            Deactivate(_activeProvider);
    }
}
