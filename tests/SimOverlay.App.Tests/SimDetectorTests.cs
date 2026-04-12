using SimOverlay.Core;
using SimOverlay.Core.Events;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.App.Tests;

/// <summary>
/// Unit tests for <see cref="SimDetector"/>.
///
/// The automatic timer is disabled in all tests (Timeout.InfiniteTimeSpan).
/// Tests call Poll() directly so behaviour is fully deterministic with no
/// wall-clock delays.
///
/// Key invariants under test:
/// <list type="bullet">
///   <item>All providers are polled every tick regardless of which is active.</item>
///   <item>First-connect wins — no switching while the active provider is running.</item>
///   <item>Disconnect debounce — <see cref="SimDetector.DisconnectThreshold"/> consecutive
///     false results needed before deactivation.</item>
///   <item>Recovery — a Disconnecting provider that comes back returns to Active without
///     being restarted.</item>
///   <item><see cref="SimDetector.ProviderStates"/> reflects the real state at every
///     point in time.</item>
/// </list>
/// </summary>
public class SimDetectorTests
{
    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class MockProvider : ISimProvider
    {
        public string SimId  { get; }
        public bool   Running { get; set; }
        public bool   Started { get; private set; }
        public bool   Stopped { get; private set; }
        public int    StartCallCount { get; private set; }

        public event Action<SimState>? StateChanged;

        public MockProvider(string simId, bool running = false)
        {
            SimId   = simId;
            Running = running;
        }

        public bool IsRunning() => Running;
        public void Start()     { Started = true; Stopped = false; StartCallCount++; }
        public void Stop()      { Stopped = true; Started = false; }

        public void FireState(SimState state) => StateChanged?.Invoke(state);
    }

    private sealed class MockBus : ISimDataBus
    {
        public readonly List<object> Events = [];
        public void Publish<T>(T data)              => Events.Add(data!);
        public void Subscribe<T>(Action<T> handler)   { }
        public void Unsubscribe<T>(Action<T> handler) { }
    }

    private static SimDetector Make(MockBus bus, IReadOnlyList<ISimProvider> providers)
        => new(bus, providers, Timeout.InfiniteTimeSpan);

    // Shortcut — reads the ProviderState for a named provider from the detector.
    private static ProviderState State(SimDetector det, string simId)
        => det.ProviderStates[simId];

    // ── Baseline ─────────────────────────────────────────────────────────────

    [Fact]
    public void Poll_NoSimRunning_AllProvidersIdle()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing");
        var p2  = new MockProvider("LMU");
        using var det = Make(bus, [p1, p2]);

        det.Poll();

        Assert.Equal(ProviderState.Idle, State(det, "iRacing"));
        Assert.Equal(ProviderState.Idle, State(det, "LMU"));
        Assert.False(p1.Started);
        Assert.False(p2.Started);
        Assert.Null(det.ActiveSimId);
    }

    [Fact]
    public void Poll_SimAppears_TransitionsIdleToAvailableToActive()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing");
        using var det = Make(bus, [p1]);

        // Not running yet — stays Idle.
        det.Poll();
        Assert.Equal(ProviderState.Idle, State(det, "iRacing"));

        // Sim starts.
        p1.Running = true;
        det.Poll();
        // Available → Active in the same tick (no other provider was active).
        Assert.Equal(ProviderState.Active, State(det, "iRacing"));
        Assert.True(p1.Started);
        Assert.Equal("iRacing", det.ActiveSimId);
    }

    [Fact]
    public void Poll_ActiveProviderChanged_FiresOnActivate()
    {
        var bus     = new MockBus();
        var p1      = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        ISimProvider? activated = null;
        det.ActiveProviderChanged += p => activated = p;

        det.Poll();

        Assert.Same(p1, activated);
    }

    // ── First-connect-wins (no switching) ────────────────────────────────────

    [Fact]
    public void Poll_FirstConnectWins_NoSwitchWhenSecondSimAppears()
    {
        var bus = new MockBus();
        var pA  = new MockProvider("LMU",     running: true);
        var pB  = new MockProvider("iRacing", running: false);
        using var det = Make(bus, [pA, pB]);

        det.Poll();
        Assert.Equal(ProviderState.Active,    State(det, "LMU"));
        Assert.Equal(ProviderState.Idle,      State(det, "iRacing"));

        // iRacing starts — must NOT trigger a switch.
        pB.Running = true;
        det.Poll();
        det.Poll();

        Assert.Equal(ProviderState.Active,    State(det, "LMU"));
        Assert.Equal(ProviderState.Available, State(det, "iRacing"));
        Assert.False(pB.Started);                  // iRacing must NOT have been started.
        Assert.Equal("LMU", det.ActiveSimId);
    }

    [Fact]
    public void Poll_NoDoubleStart_WhenActivePollContinues()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        det.Poll();
        det.Poll();
        det.Poll();

        // Start() must be called exactly once.
        Assert.Equal(1, p1.StartCallCount);
    }

    [Fact]
    public void Poll_BothSimsRunningAtStart_FirstInListActivates()
    {
        var bus      = new MockBus();
        var p1       = new MockProvider("iRacing", running: true);
        var p2       = new MockProvider("LMU",     running: true);
        using var det = Make(bus, [p1, p2]);

        ISimProvider? activated = null;
        det.ActiveProviderChanged += p => activated = p;

        det.Poll();

        // First provider in list wins the bootstrap race.
        Assert.Same(p1, activated);
        Assert.Equal(ProviderState.Active,    State(det, "iRacing"));
        Assert.Equal(ProviderState.Available, State(det, "LMU"));
        Assert.False(p2.Started);
    }

    // ── Disconnect debounce ──────────────────────────────────────────────────

    [Fact]
    public void Poll_FirstFalse_GoesDisconnecting_NotStopped()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        det.Poll();       // Active
        p1.Running = false;
        det.Poll();       // strike 1 → Disconnecting

        Assert.Equal(ProviderState.Disconnecting, State(det, "iRacing"));
        Assert.False(p1.Stopped);
        Assert.DoesNotContain(bus.Events, e => e is SimStateChangedEvent { State: SimState.Disconnected });
    }

    [Fact]
    public void Poll_ThresholdConsecutiveFalse_StopsProvider()
    {
        // DisconnectThreshold = 3: Active → Disconnecting (strike 1) → strike 2 → strike 3 → Idle.
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        det.Poll();        // Active
        p1.Running = false;
        det.Poll();        // strike 1 → Disconnecting
        det.Poll();        // strike 2
        det.Poll();        // strike 3 → Idle, deactivated

        Assert.Equal(ProviderState.Idle, State(det, "iRacing"));
        Assert.True(p1.Stopped);
        Assert.Contains(bus.Events, e => e is SimStateChangedEvent { State: SimState.Disconnected });
        Assert.Null(det.ActiveSimId);
    }

    [Fact]
    public void Poll_BelowThreshold_DoesNotStop()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        det.Poll();        // Active
        p1.Running = false;
        det.Poll();        // strike 1
        det.Poll();        // strike 2  (threshold = 3, so still Disconnecting)

        Assert.Equal(ProviderState.Disconnecting, State(det, "iRacing"));
        Assert.False(p1.Stopped);
    }

    [Fact]
    public void Poll_Disconnecting_RecoveryBeforeThreshold_ReturnsToActive()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        det.Poll();        // Active
        p1.Running = false;
        det.Poll();        // strike 1 → Disconnecting
        p1.Running = true;
        det.Poll();        // recovery → Active (no restart)

        Assert.Equal(ProviderState.Active, State(det, "iRacing"));
        Assert.False(p1.Stopped);
        // Start() must have been called exactly once (no restart on recovery).
        Assert.Equal(1, p1.StartCallCount);
    }

    [Fact]
    public void Poll_StrikesReset_AfterRecovery_RequiresFullThresholdAgain()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        det.Poll();        // Active

        // Go to strike 2, recover, then go to strike 2 again — still should not stop.
        p1.Running = false;
        det.Poll();        // strike 1
        det.Poll();        // strike 2
        p1.Running = true;
        det.Poll();        // recovery — reset strikes
        p1.Running = false;
        det.Poll();        // strike 1 (reset from 0)
        det.Poll();        // strike 2

        Assert.Equal(ProviderState.Disconnecting, State(det, "iRacing"));
        Assert.False(p1.Stopped);

        det.Poll();        // strike 3 → stop

        Assert.True(p1.Stopped);
    }

    // ── Post-disconnect activation ───────────────────────────────────────────

    [Fact]
    public void Poll_AfterActiveDisconnects_SecondSimActivates_SameTick()
    {
        // When A is deactivated in the same poll that B is Available, B activates
        // immediately (Step 2 runs after all state transitions).
        var bus = new MockBus();
        var pA  = new MockProvider("LMU",     running: true);
        var pB  = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [pA, pB]);

        det.Poll();
        // pA active (first in list), pB Available (second, not activated).
        Assert.Equal(ProviderState.Active,    State(det, "LMU"));
        Assert.Equal(ProviderState.Available, State(det, "iRacing"));

        // pA stops; pB is still running.
        pA.Running = false;
        det.Poll();  // strike 1 → pA Disconnecting
        det.Poll();  // strike 2
        det.Poll();  // strike 3 → pA Idle, pB activates in same tick

        Assert.Equal(ProviderState.Idle,   State(det, "LMU"));
        Assert.Equal(ProviderState.Active, State(det, "iRacing"));
        Assert.True(pB.Started);
    }

    [Fact]
    public void Poll_AfterActiveDisconnects_NewSimAppearingNextPollActivates()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        var p2  = new MockProvider("LMU",     running: false);
        using var det = Make(bus, [p1, p2]);

        det.Poll();        // p1 active
        p1.Running = false;
        det.Poll(); det.Poll(); det.Poll(); // three strikes → p1 idle

        Assert.Equal(ProviderState.Idle, State(det, "iRacing"));
        Assert.Null(det.ActiveSimId);

        // LMU appears on the next poll.
        p2.Running = true;
        det.Poll();

        Assert.Equal(ProviderState.Active, State(det, "LMU"));
        Assert.True(p2.Started);
    }

    // ── ProviderStates snapshot ──────────────────────────────────────────────

    [Fact]
    public void ProviderStates_ReflectsAllProviders_AtEveryStage()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing");
        var p2  = new MockProvider("LMU");
        using var det = Make(bus, [p1, p2]);

        // Initial state.
        Assert.Equal(ProviderState.Idle, State(det, "iRacing"));
        Assert.Equal(ProviderState.Idle, State(det, "LMU"));

        p1.Running = true;
        det.Poll();
        Assert.Equal(ProviderState.Active, State(det, "iRacing"));
        Assert.Equal(ProviderState.Idle,   State(det, "LMU"));

        p2.Running = true;
        det.Poll();
        Assert.Equal(ProviderState.Active,    State(det, "iRacing"));
        Assert.Equal(ProviderState.Available, State(det, "LMU"));

        p1.Running = false;
        det.Poll();
        Assert.Equal(ProviderState.Disconnecting, State(det, "iRacing"));
        Assert.Equal(ProviderState.Available,     State(det, "LMU"));
    }

    // ── Bus events ───────────────────────────────────────────────────────────

    [Fact]
    public void Poll_ProviderStateChangedEvent_ForwardedToBus()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        det.Poll();  // activates p1

        p1.FireState(SimState.Connected);
        p1.FireState(SimState.InSession);

        var states = bus.Events.OfType<SimStateChangedEvent>().Select(e => e.State).ToList();
        Assert.Contains(SimState.Connected,  states);
        Assert.Contains(SimState.InSession,  states);
    }

    [Fact]
    public void Poll_DisconnectPublishedOnBus_WhenDeactivated()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        det.Poll();
        p1.Running = false;
        det.Poll(); det.Poll(); det.Poll();

        Assert.Contains(bus.Events, e => e is SimStateChangedEvent { State: SimState.Disconnected });
    }

    [Fact]
    public void Poll_ActiveProviderChanged_FiresNullOnDeactivate()
    {
        var bus  = new MockBus();
        var p1   = new MockProvider("iRacing", running: true);
        using var det = Make(bus, [p1]);

        var changes = new List<ISimProvider?>();
        det.ActiveProviderChanged += p => changes.Add(p);

        det.Poll();             // activate
        p1.Running = false;
        det.Poll(); det.Poll(); det.Poll(); // three strikes → deactivate

        Assert.Equal(2, changes.Count);
        Assert.Same(p1, changes[0]);  // activate
        Assert.Null(changes[1]);      // deactivate
    }

    // ── Exception resilience ─────────────────────────────────────────────────

    [Fact]
    public void Poll_IsRunningThrows_TreatedAsNotRunning_OtherProviderUnaffected()
    {
        var bus      = new MockBus();
        var pBad     = new ThrowingProvider("BadSim");
        var pGood    = new MockProvider("LMU", running: true);
        using var det = Make(bus, [pBad, pGood]);

        // Should not throw; pGood should activate normally.
        det.Poll();

        Assert.Equal(ProviderState.Active, State(det, "LMU"));
        Assert.True(pGood.Started);
    }

    private sealed class ThrowingProvider : ISimProvider
    {
        public string SimId { get; }
        public ThrowingProvider(string id) => SimId = id;
        public bool IsRunning() => throw new InvalidOperationException("SDK crash");
        public void Start() { }
        public void Stop()  { }
        public event Action<SimState>? StateChanged { add { } remove { } }
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_StopsActiveProvider()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        var det = Make(bus, [p1]);

        det.Poll();  // activate
        det.Dispose();

        Assert.True(p1.Stopped);
        Assert.Contains(bus.Events, e => e is SimStateChangedEvent { State: SimState.Disconnected });
    }

    [Fact]
    public void Dispose_PollAfterDispose_IsNoOp()
    {
        var bus = new MockBus();
        var p1  = new MockProvider("iRacing", running: true);
        var det = Make(bus, [p1]);

        det.Dispose();
        det.Poll();  // must not throw or activate

        Assert.False(p1.Started);
    }
}
