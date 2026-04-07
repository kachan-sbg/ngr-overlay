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

    /// <summary>
    /// Ordered list of all registered overlay types with their display names.
    /// Ordered by registration order. Used by <see cref="Settings.SettingsWindow"/>
    /// to build the sidebar nav list dynamically without referencing concrete overlay types.
    /// </summary>
    IReadOnlyList<(string Id, string DisplayName)> DisplayNames { get; }
}
