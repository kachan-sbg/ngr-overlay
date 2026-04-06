using SimOverlay.Core.Config;
using SimOverlay.Rendering;

namespace SimOverlay.App;

/// <summary>
/// Creates <see cref="BaseOverlay"/> instances by overlay ID.
/// Acts as the composition root for overlay construction — the only place
/// where concrete overlay types are instantiated.
/// </summary>
public interface IOverlayFactory
{
    /// <summary>Creates an overlay instance for the given config.</summary>
    BaseOverlay Create(OverlayConfig config);

    /// <summary>
    /// Default <see cref="OverlayConfig"/> for every registered overlay type, keyed by overlay ID.
    /// Used by <see cref="OverlayManager"/> to ensure all overlay types have a config entry.
    /// </summary>
    IReadOnlyDictionary<string, OverlayConfig> DefaultConfigs { get; }
}
