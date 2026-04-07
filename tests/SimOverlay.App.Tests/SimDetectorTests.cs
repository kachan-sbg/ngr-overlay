using SimOverlay.Core;
using SimOverlay.Core.Events;
using SimOverlay.Sim.Contracts;

namespace SimOverlay.App.Tests;

/// <summary>
/// Unit tests for <see cref="SimDetector"/> using synchronous Poll() calls.
/// The timer is disabled (Timeout.InfiniteTimeSpan) so tests drive polling
/// directly and are fully deterministic with no wall-clock delays.
/// </summary>
public class SimDetectorTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class MockProvider : ISimProvider
    {
        public string SimId { get; }
        public bool Running { get; set; }
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public event Action<SimState>? StateChanged;

        public MockProvider(string simId) => SimId = simId;

        public bool IsRunning() => Running;
        public void Start() => Started = true;
        public void Stop() { Stopped = true; Started = false; }
        public void FireStateChange(SimState state) => StateChanged?.Invoke(state);
    }

    private sealed class MockDataBus : ISimDataBus
    {
        public readonly List<object> Published = [];

        public void Publish<T>(T data) => Published.Add(data!);
        public void Subscribe<T>(Action<T> handler) { }
        public void Unsubscribe<T>(Action<T> handler) { }
    }

    /// Creates a SimDetector with the automatic timer disabled.
    /// Tests drive Poll() manually so there are no timing races.
    private static SimDetector MakeDetector(ISimDataBus bus, IReadOnlyList<ISimProvider> providers)
        => new(bus, providers, Timeout.InfiniteTimeSpan);

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Poll_NoSimRunning_NoProviderActivated()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = false };
        var p2  = new MockProvider("LMU")     { Running = false };
        using var det = MakeDetector(bus, [p1, p2]);

        det.Poll();

        Assert.False(p1.Started);
        Assert.False(p2.Started);
    }

    [Fact]
    public void Poll_ActivatesFirstRunningProviderInPriorityOrder()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = true };
        var p2  = new MockProvider("LMU")     { Running = true };
        using var det = MakeDetector(bus, [p1, p2]);

        ISimProvider? activated = null;
        det.ActiveProviderChanged += p => activated = p;
        det.Poll();

        // First in the list wins — iRacing.
        Assert.Same(p1, activated);
        Assert.True(p1.Started);
        Assert.False(p2.Started);
    }

    [Fact]
    public void Poll_ActivatesLmu_WhenIRacingNotRunning()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = false };
        var p2  = new MockProvider("LMU")     { Running = true  };
        using var det = MakeDetector(bus, [p1, p2]);

        ISimProvider? activated = null;
        det.ActiveProviderChanged += p => activated = p;
        det.Poll();

        Assert.Same(p2, activated);
        Assert.False(p1.Started);
        Assert.True(p2.Started);
    }

    [Fact]
    public void Poll_OnlyOneProviderActiveAtATime()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = true };
        var p2  = new MockProvider("LMU")     { Running = true };
        using var det = MakeDetector(bus, [p1, p2]);

        det.Poll(); // activates p1

        // Even though p2 is also running, p1 stays active — no switching mid-session.
        det.Poll();
        det.Poll();

        Assert.True(p1.Started);
        Assert.False(p2.Started);
    }

    [Fact]
    public void Poll_Debounce_SingleFalse_DoesNotStopProvider()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = true };
        using var det = MakeDetector(bus, [p1]);

        det.Poll();        // activates p1
        p1.Running = false;
        det.Poll();        // strike 1 — must NOT stop yet (threshold = 2)

        Assert.False(p1.Stopped);
        Assert.DoesNotContain(bus.Published,
            e => e is SimStateChangedEvent { State: SimState.Disconnected });
    }

    [Fact]
    public void Poll_Debounce_TwoConsecutiveFalse_StopsProvider()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = true };
        using var det = MakeDetector(bus, [p1]);

        det.Poll();        // activates p1
        p1.Running = false;
        det.Poll();        // strike 1
        det.Poll();        // strike 2 → stops

        Assert.True(p1.Stopped);
        Assert.Contains(bus.Published,
            e => e is SimStateChangedEvent { State: SimState.Disconnected });
    }

    [Fact]
    public void Poll_Debounce_TrueResetStrikes_DelaysStop()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = true };
        using var det = MakeDetector(bus, [p1]);

        det.Poll();        // activates p1
        p1.Running = false;
        det.Poll();        // strike 1
        p1.Running = true;
        det.Poll();        // back to true — resets strikes
        p1.Running = false;
        det.Poll();        // strike 1 again (not strike 2)

        // Only one strike since the reset; provider still active.
        Assert.False(p1.Stopped);

        det.Poll();        // strike 2 → now stops

        Assert.True(p1.Stopped);
    }

    [Fact]
    public void Poll_ActiveProviderChanged_FiresOnActivateAndDeactivate()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = true };
        using var det = MakeDetector(bus, [p1]);

        var changes = new List<ISimProvider?>();
        det.ActiveProviderChanged += p => changes.Add(p);

        det.Poll();        // activate
        p1.Running = false;
        det.Poll();        // strike 1
        det.Poll();        // strike 2 → deactivate

        Assert.Equal(2, changes.Count);
        Assert.Same(p1, changes[0]); // activate event
        Assert.Null(changes[1]);     // deactivate event
    }

    [Fact]
    public void Poll_StateChangedFromProvider_ForwardedToDataBus()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = true };
        using var det = MakeDetector(bus, [p1]);

        det.Poll(); // activate

        p1.FireStateChange(SimState.Connected);
        p1.FireStateChange(SimState.InSession);

        var states = bus.Published.OfType<SimStateChangedEvent>().Select(e => e.State).ToList();
        Assert.Contains(SimState.Connected,  states);
        Assert.Contains(SimState.InSession,  states);
    }

    [Fact]
    public void Poll_AfterFirstProviderStops_SecondProviderCanActivate()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = true  };
        var p2  = new MockProvider("LMU")     { Running = false };
        using var det = MakeDetector(bus, [p1, p2]);

        det.Poll();        // p1 activates
        p1.Running = false;
        det.Poll();        // strike 1
        det.Poll();        // strike 2 → p1 stops
        p2.Running = true;
        det.Poll();        // p2 should now activate

        Assert.True(p2.Started);
    }

    [Fact]
    public void Poll_PriorityOrder_FirstInListWins_WhenBothRunning()
    {
        var bus     = new MockDataBus();
        // LMU is listed first, iRacing second — reversed from default config.
        var pLmu     = new MockProvider("LMU")     { Running = true };
        var pIRacing = new MockProvider("iRacing") { Running = true };
        using var det = MakeDetector(bus, [pLmu, pIRacing]);

        ISimProvider? activated = null;
        det.ActiveProviderChanged += p => activated = p;
        det.Poll();

        Assert.Same(pLmu, activated);
        Assert.False(pIRacing.Started);
    }

    [Fact]
    public void Poll_DisposedDetector_DoesNothing()
    {
        var bus = new MockDataBus();
        var p1  = new MockProvider("iRacing") { Running = true };
        var det = MakeDetector(bus, [p1]);

        det.Dispose();
        det.Poll(); // should be a no-op

        Assert.False(p1.Started);
    }
}
