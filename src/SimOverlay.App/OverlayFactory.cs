using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Overlays;
using SimOverlay.Rendering;

namespace SimOverlay.App;

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

    // Map: overlay ID → (constructor delegate, default config).
    // To register a new overlay type, add an entry here.
    private static readonly Dictionary<string, (
        Func<ISimDataBus, OverlayConfig, ConfigStore, AppConfig, BaseOverlay> Create,
        OverlayConfig Default)> _registry = new()
    {
        [RelativeOverlay.OverlayId] = (
            (bus, cfg, store, app) => new RelativeOverlay(bus, cfg, store, app),
            RelativeOverlay.DefaultConfig),

        [SessionInfoOverlay.OverlayId] = (
            (bus, cfg, store, app) => new SessionInfoOverlay(bus, cfg, store, app),
            SessionInfoOverlay.DefaultConfig),

        [DeltaBarOverlay.OverlayId] = (
            (bus, cfg, store, app) => new DeltaBarOverlay(bus, cfg, store, app),
            DeltaBarOverlay.DefaultConfig),
    };

    public OverlayFactory(ISimDataBus bus, ConfigStore configStore, AppConfig appConfig)
    {
        _bus         = bus;
        _configStore = configStore;
        _appConfig   = appConfig;
    }

    /// <inheritdoc/>
    public BaseOverlay Create(OverlayConfig config)
    {
        if (!_registry.TryGetValue(config.Id, out var entry))
            throw new ArgumentException($"Unknown overlay ID '{config.Id}'.", nameof(config));
        return entry.Create(_bus, config, _configStore, _appConfig);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, OverlayConfig> DefaultConfigs =>
        _registry.ToDictionary(kv => kv.Key, kv => kv.Value.Default);
}
