using NrgOverlay.Core;
using NrgOverlay.Core.Events;
using NrgOverlay.Sim.Contracts;
using System.Threading;
using Timer = System.Threading.Timer;

namespace NrgOverlay.App;

/// <summary>
/// Polls all registered <see cref="ISimProvider"/> instances on every tick and manages
/// a single active provider lifecycle.
/// </summary>
public sealed class SimDetector : IDisposable
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int DisconnectThreshold = 3;

    private readonly ISimDataBus _bus;
    private readonly IReadOnlyList<ISimProvider> _providers;
    private readonly Dictionary<ISimProvider, ProviderState> _states = new();
    private readonly Dictionary<ISimProvider, int> _strikes = new();
    private readonly Timer _timer;
    private readonly object _sync = new();
    private int _pollInProgress;

    private ISimProvider? _activeProvider;
    private SimState _currentSimState = SimState.Disconnected;
    private bool _disposed;

    public event Action<ISimProvider?>? ActiveProviderChanged;

    public IReadOnlyDictionary<string, ProviderState> ProviderStates
    {
        get
        {
            lock (_sync)
                return _states.ToDictionary(kv => kv.Key.SimId, kv => kv.Value);
        }
    }

    public string? ActiveSimId
    {
        get
        {
            lock (_sync)
                return _activeProvider?.SimId;
        }
    }

    public SimDetector(ISimDataBus bus, IReadOnlyList<ISimProvider> providers)
        : this(bus, providers, PollInterval) { }

    internal SimDetector(ISimDataBus bus, IReadOnlyList<ISimProvider> providers, TimeSpan pollInterval)
    {
        _bus = bus;
        _providers = providers;

        foreach (var p in providers)
        {
            _states[p] = ProviderState.Idle;
            _strikes[p] = 0;
        }

        _timer = pollInterval == Timeout.InfiniteTimeSpan
            ? new Timer(Poll, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan)
            : new Timer(Poll, null, TimeSpan.Zero, pollInterval);
    }

    internal void Poll(object? _ = null)
    {
        if (_disposed) return;
        if (Interlocked.Exchange(ref _pollInProgress, 1) == 1)
            return;

        try
        {
            lock (_sync)
            {
                if (_disposed) return;

                foreach (var provider in _providers)
                {
                    bool running;
                    try { running = provider.IsRunning(); }
                    catch (Exception ex)
                    {
                        AppLog.Exception($"SimDetector: IsRunning() threw for '{provider.SimId}'", ex);
                        running = false;
                    }

                    TransitionState(provider, running);
                }

                if (_activeProvider == null)
                {
                    var candidate = _providers.FirstOrDefault(p => _states[p] == ProviderState.Available);
                    if (candidate != null)
                        Activate(candidate);
                }

                if (_currentSimState != SimState.Disconnected)
                    _bus.Publish(new SimStateChangedEvent(_currentSimState));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _pollInProgress, 0);
        }
    }

    private void TransitionState(ISimProvider provider, bool running)
    {
        switch (_states[provider])
        {
            case ProviderState.Idle:
                if (running)
                {
                    _states[provider] = ProviderState.Available;
                    AppLog.Info($"SimDetector: '{provider.SimId}' -> Available.");
                }
                break;

            case ProviderState.Available:
                if (!running)
                {
                    _states[provider] = ProviderState.Idle;
                    AppLog.Info($"SimDetector: '{provider.SimId}' -> Idle (disappeared before activation).");
                }
                break;

            case ProviderState.Active:
                if (!running)
                {
                    _strikes[provider] = 1;
                    _states[provider] = ProviderState.Disconnecting;
                    AppLog.Info($"SimDetector: '{provider.SimId}' -> Disconnecting (strike 1/{DisconnectThreshold}).");
                }
                break;

            case ProviderState.Disconnecting:
                if (running)
                {
                    _strikes[provider] = 0;
                    _states[provider] = ProviderState.Active;
                    AppLog.Info($"SimDetector: '{provider.SimId}' -> Active (recovered).");
                }
                else
                {
                    _strikes[provider]++;
                    if (_strikes[provider] >= DisconnectThreshold)
                    {
                        AppLog.Info($"SimDetector: '{provider.SimId}' confirmed disconnected after {_strikes[provider]} strikes.");
                        Deactivate(provider);
                    }
                    else
                    {
                        AppLog.Info($"SimDetector: '{provider.SimId}' still Disconnecting (strike {_strikes[provider]}/{DisconnectThreshold}).");
                    }
                }
                break;
        }
    }

    private void Activate(ISimProvider provider)
    {
        try
        {
            AppLog.Info($"SimDetector: activating '{provider.SimId}'.");
            _activeProvider = provider;
            _states[provider] = ProviderState.Active;
            _strikes[provider] = 0;

            provider.StateChanged += OnProviderStateChanged;
            provider.Start();

            ActiveProviderChanged?.Invoke(provider);
        }
        catch (Exception ex)
        {
            AppLog.Exception($"SimDetector: error activating '{provider.SimId}'", ex);
            provider.StateChanged -= OnProviderStateChanged;
            _states[provider] = ProviderState.Idle;
            _activeProvider = null;
        }
    }

    private void Deactivate(ISimProvider provider)
    {
        AppLog.Info($"SimDetector: deactivating '{provider.SimId}'.");
        provider.StateChanged -= OnProviderStateChanged;
        _states[provider] = ProviderState.Idle;
        _strikes[provider] = 0;
        _activeProvider = null;
        _currentSimState = SimState.Disconnected;

        try { provider.Stop(); }
        catch (Exception ex) { AppLog.Exception($"SimDetector: error stopping '{provider.SimId}'", ex); }

        _bus.Publish(new SimStateChangedEvent(SimState.Disconnected));
        ActiveProviderChanged?.Invoke(null);
    }

    private void OnProviderStateChanged(SimState state)
    {
        AppLog.Info($"SimDetector: provider state -> {state}");
        lock (_sync)
            _currentSimState = state;
        _bus.Publish(new SimStateChangedEvent(state));
    }

    public void Dispose()
    {
        lock (_sync)
            _disposed = true;

        using var timerDone = new ManualResetEvent(false);
        _timer.Dispose(timerDone);
        timerDone.WaitOne();

        lock (_sync)
        {
            if (_activeProvider != null)
                Deactivate(_activeProvider);
        }
    }
}

