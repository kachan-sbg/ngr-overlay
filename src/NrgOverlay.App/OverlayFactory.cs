using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Overlays;
using NrgOverlay.Rendering;

namespace NrgOverlay.App;

/// <summary>
/// Registry-based <see cref="IOverlayFactory"/> implementation.
/// <para>
/// To add a new overlay type: (1) write the class, (2) add an entry to <see cref="_registry"/>.
/// No changes to <see cref="OverlayManager"/> or <c>Program.cs</c> are required.
/// </para>
/// </summary>
public sealed class OverlayFactory : IOverlayFactory
{
    private readonly ISimDataBus _bus;
    private readonly ConfigStore _configStore;
    private readonly AppConfig   _appConfig;

    // Ordered list of registrations: (id, displayName, constructor delegate, default config).
    // ORDER MATTERS вЂ” determines sidebar order in Settings.
    // To add a new overlay type: write the class, add one entry here. Nothing else changes.
    private static readonly (
        string Id,
        string DisplayName,
        Func<ISimDataBus, OverlayConfig, ConfigStore, AppConfig, BaseOverlay> Create,
        OverlayConfig Default)[] _registry =
    [
        (RelativeOverlay.OverlayId,    "Relative",
            (bus, cfg, store, app) => new RelativeOverlay(bus, cfg, store, app),
            RelativeOverlay.DefaultConfig),

        (SessionInfoOverlay.OverlayId, "Session Info",
            (bus, cfg, store, app) => new SessionInfoOverlay(bus, cfg, store, app),
            SessionInfoOverlay.DefaultConfig),

        (DeltaBarOverlay.OverlayId,    "Delta Bar",
            (bus, cfg, store, app) => new DeltaBarOverlay(bus, cfg, store, app),
            DeltaBarOverlay.DefaultConfig),

        (InputTelemetryOverlay.OverlayId, "Input Telemetry",
            (bus, cfg, store, app) => new InputTelemetryOverlay(bus, cfg, store, app),
            InputTelemetryOverlay.DefaultConfig),

        (FuelCalculatorOverlay.OverlayId, "Fuel Calculator",
            (bus, cfg, store, app) => new FuelCalculatorOverlay(bus, cfg, store, app),
            FuelCalculatorOverlay.DefaultConfig),

        (StandingsOverlay.OverlayId, "Standings",
            (bus, cfg, store, app) => new StandingsOverlay(bus, cfg, store, app),
            StandingsOverlay.DefaultConfig),

        (PitHelperOverlay.OverlayId, "Pit Helper",
            (bus, cfg, store, app) => new PitHelperOverlay(bus, cfg, store, app),
            PitHelperOverlay.DefaultConfig),

        (WeatherOverlay.OverlayId, "Weather",
            (bus, cfg, store, app) => new WeatherOverlay(bus, cfg, store, app),
            WeatherOverlay.DefaultConfig),

        (FlatTrackMapOverlay.OverlayId, "Track Map",
            (bus, cfg, store, app) => new FlatTrackMapOverlay(bus, cfg, store, app),
            FlatTrackMapOverlay.DefaultConfig),

        (ChatOverlay.OverlayId, "Chat",
            (bus, cfg, store, app) => new ChatOverlay(bus, cfg, store, app),
            ChatOverlay.DefaultConfig),
    ];

    public OverlayFactory(ISimDataBus bus, ConfigStore configStore, AppConfig appConfig)
    {
        _bus         = bus;
        _configStore = configStore;
        _appConfig   = appConfig;
    }

    /// <inheritdoc/>
    public BaseOverlay Create(OverlayConfig config)
    {
        var entry = _registry.FirstOrDefault(r => r.Id == config.Id);
        if (entry == default)
            throw new ArgumentException($"Unknown overlay ID '{config.Id}'.", nameof(config));
        return entry.Create(_bus, config, _configStore, _appConfig);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, OverlayConfig> DefaultConfigs =>
        _registry.ToDictionary(r => r.Id, r => r.Default);

    /// <inheritdoc/>
    public IReadOnlyList<(string Id, string DisplayName)> DisplayNames =>
        _registry.Select(r => (r.Id, r.DisplayName)).ToList();
}

