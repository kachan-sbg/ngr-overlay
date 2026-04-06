# PROJECT.md

## Racing Simulator Overlay — Project Overview

### Summary

A Windows desktop overlay application for racing simulators, designed to display real-time telemetry data as transparent, click-through windows rendered directly over the simulator. The MVP targets iRacing exclusively, but the architecture is designed from the ground up to cleanly accommodate additional simulators (Assetto Corsa, ACC, Le Mans Ultimate, rFactor2) without structural changes.

### Goals

- Provide a set of always-on-top, transparent overlay windows displaying racing telemetry data.
- Support independent positioning, sizing, and customization of each overlay.
- Deliver a minimal, high-information-density visual style similar to TinyPedal.
- Read telemetry data from iRacing via its published shared memory interface at 60 Hz.
- Persist overlay positions, sizes, and appearance settings across application restarts.
- Provide a configuration UI (separate from overlays) for enabling/disabling overlays and editing their appearance.

### Non-Goals (MVP)

- Multi-simulator simultaneous polling. Only one sim is active at a time.
- Replay file analysis or offline data playback.
- Network-based telemetry (e.g., reading data from another machine).
- Skin or theme system beyond per-overlay RGBA color and font configuration.
- Plugin or scripting extension system.
- macOS or Linux support.
- Overlays for simulators other than iRacing (architecture supports them; implementation deferred).

### Architecture Summary

The solution is organized as a multi-project C# / .NET 8 solution. A clean abstraction boundary separates sim-agnostic core infrastructure from sim-specific data providers:

- `SimOverlay.Core` — domain model, shared data contracts, configuration schema, event/notification types.
- `SimOverlay.Rendering` — Direct2D + DirectComposition overlay window host, base overlay class, rendering loop.
- `SimOverlay.Sim.Contracts` — interfaces and data transfer objects (DTOs) that all sim providers must implement.
- `SimOverlay.Sim.iRacing` — iRacing-specific provider: MMF reader, telemetry parser, session decoder.
- `SimOverlay.Overlays` — implementations of the three MVP overlays (Relative, Session Info, Delta Bar).
- `SimOverlay.App` — entry point, tray icon, sim auto-detection, orchestration, configuration UI host.

Data flows from a sim provider into an `ISimDataBus` (an in-process event bus), which all active overlay instances subscribe to. Overlays consume normalized data types defined in `Sim.Contracts`; they have no knowledge of which sim is running.

---