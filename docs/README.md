# NrgOverlay вЂ” Project Documentation

This is the single entry point for all project documentation. Start here.

---

## What is NrgOverlay?

A Windows desktop overlay application for racing simulators. Transparent, always-on-top windows that display real-time telemetry data (relative standings, lap times, delta, session info, fuel, inputs, and more) directly over the sim. Designed for three priorities, in order:

1. **Minimal resource usage** вЂ” Software Direct2D rendering + `UpdateLayeredWindow` presentation. No WPF, no Electron, no Python on the render path.
2. **UI customization** вЂ” Every overlay has independently configurable colors, opacity, size, and font. Each overlay also has an optional stream override profile (see below).
3. **OBS capture compatibility** вЂ” Each overlay is an independent OBS Window Capture source with real alpha transparency. No chroma key needed.

---

## Core Concepts

### Multi-sim, one active at a time
The app auto-detects which sim is running and activates that data provider. Only one sim is polled at a time. Architecture supports adding new sims (ACC, AC, LMU, rF2) without changing rendering or overlay code.

### Multiple independent overlays
Each overlay (Relative, Session Info, Delta Bar, Fuel, Input, Standings, ...) is its own transparent window. They can be independently positioned, sized, enabled/disabled, and captured in OBS. In **edit mode** (toggled via tray icon) windows accept mouse input for drag and resize; in normal mode they are fully click-through.

### Stream override (dual-profile)
Each overlay has a **Screen profile** (the driver's view) and an optional **Stream override profile** (for viewers). When **stream mode** is toggled (tray icon or settings window), overlays with an enabled override switch to the stream profile вЂ” same window, different appearance. Alpha Phase 11 will add dual-window mode where each profile renders in its own window for true simultaneous driver + OBS displays.

---

## Document Index

| Document | Purpose |
|---|---|
| **[README.md](README.md)** | This file. Project overview and navigation. |
| **[DECISIONS.md](DECISIONS.md)** | Chronological log of every significant design decision and why it was made. |
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Full technical architecture: solution structure, rendering pipeline, data flow, config system, OBS integration. |
| **[OVERLAYS.md](OVERLAYS.md)** | Specification for each overlay: layout, data fields, update frequency, customization options. |
| **[TASKS.md](TASKS.md)** | Ordered implementation task list grouped by phase. |
| **[CRITICAL-ISSUES.md](CRITICAL-ISSUES.md)** | Crash/hang/data-corruption risks and reliability-hardening status. |
| **[DEBUG-RUNBOOK.md](DEBUG-RUNBOOK.md)** | Step-by-step incident diagnostics for startup, SDK lifecycle, and coexistence scenarios. |
| **[AI-DOCS-GUIDE.md](AI-DOCS-GUIDE.md)** | AI-first documentation loading policy (minimal context, archive usage rules). |
| **[REVIEW-MVP.md](REVIEW-MVP.md)** | Code and architecture review conducted after MVP completion. |
| **[archive/mvp/](archive/mvp/README.md)** | Archived MVP-era documentation (phases 0вЂ“6, known issues, verification checklists). |

---

## Solution Structure

```
NrgOverlay.sln
в”њв”Ђв”Ђ src/
в”‚   в”њв”Ђв”Ђ NrgOverlay.Core            # Domain types, config schema, ISimDataBus
в”‚   в”њв”Ђв”Ђ NrgOverlay.Rendering       # Direct2D + UpdateLayeredWindow, BaseOverlay
в”‚   в”њв”Ђв”Ђ NrgOverlay.Sim.Contracts   # ISimProvider interface + normalized DTOs
в”‚   в”њв”Ђв”Ђ NrgOverlay.Sim.iRacing     # iRSDK shared memory reader, 60 Hz poller
в”‚   в”њв”Ђв”Ђ NrgOverlay.Overlays        # Relative, SessionInfo, DeltaBar, + Alpha overlays
в”‚   в””в”Ђв”Ђ NrgOverlay.App             # Entry point, tray icon, SimDetector, settings UI
в”њв”Ђв”Ђ tests/
в”‚   в”њв”Ђв”Ђ NrgOverlay.Core.Tests
в”‚   в”њв”Ђв”Ђ NrgOverlay.Sim.iRacing.Tests
в”‚   в”њв”Ђв”Ђ NrgOverlay.Overlays.Tests
в”‚   в””в”Ђв”Ђ NrgOverlay.Benchmarks
в””в”Ђв”Ђ docs/
```

Dependency rule: `Core` в†ђ `Sim.Contracts` в†ђ `Sim.iRacing`. `Core` в†ђ `Rendering` в†ђ `Overlays` в†ђ `App`. Nothing may depend on `App`. `Sim.*` projects may not depend on `Rendering` or `Overlays`.

---

## Technology Stack

| Layer | Technology | Why |
|---|---|---|
| Language | C# / .NET 8 | Productive, native Windows APIs accessible via P/Invoke |
| Overlay rendering | Direct2D (`ID2D1DCRenderTarget`) + `UpdateLayeredWindow` | Software CPU rendering avoids GPU interference with game flip chains |
| DirectX bindings | Vortice.Windows (NuGet) | Thin managed wrappers, no COM interop boilerplate |
| iRacing data | iRSDK shared memory (`Local\IRSDKMemMapFileName`) | Official iRacing API, 60 Hz |
| Config storage | `System.Text.Json` в†’ `%APPDATA%\NrgOverlay\config.json` | Zero dependencies, atomic writes |
| Settings UI | WPF | Only used for the settings window, not for overlays |
| iRacing SDK wrapper | IRSDKSharper (NuGet) | Handles MMF connection lifecycle |

---

## Overlays

### MVP (Implemented)
| Overlay | Window Title | Update Rate |
|---|---|---|
| **Relative** | `NrgOverlay вЂ” Relative` | 10 Hz |
| **Session Info** | `NrgOverlay вЂ” Session Info` | 1вЂ“60 Hz |
| **Delta Bar** | `NrgOverlay вЂ” Delta` | 60 Hz |

### Alpha (Planned)
| Overlay | Window Title | Phase |
|---|---|---|
| **Input Telemetry** | `NrgOverlay вЂ” Input` | 10 |
| **Fuel Calculator** | `NrgOverlay вЂ” Fuel` | 10 |
| **Standings** | `NrgOverlay вЂ” Standings` | 10 |
| **Pit Helper** | `NrgOverlay вЂ” Pit` | 11 |
| **Weather** | `NrgOverlay вЂ” Weather` | 11 |
| **Flat Track Map** | `NrgOverlay вЂ” Track Map` | 11 |

See [OVERLAYS.md](OVERLAYS.md) for detailed specifications.

---

## Implementation Status

### MVP (Complete)
| Phase | Focus |
|---|---|
| 0 вЂ” Scaffolding | Solution, projects, NuGet packages |
| 1 вЂ” Rendering core | SimDataBus, ConfigStore, transparent window, Direct2D, render loop |
| 2 вЂ” iRacing data | MMF connection, 60 Hz poller, session decoder, relative calculator |
| 3 вЂ” Overlay framework | Overlay manager, position persistence, live config, sim state |
| 4 вЂ” MVP overlays | Relative, Session Info, Delta Bar |
| 5 вЂ” Settings UI | WPF settings window, per-overlay panels, tray icon |
| 6 вЂ” Polish | SimDetector, single-instance, icon, logging, benchmarks |

### Alpha (Current)
| Phase | Status | Focus |
|---|---|---|
| 7 вЂ” Infrastructure | Not started | Config versioning, DI, tech debt, multi-class data model |
| 8 вЂ” Data pipeline | Not started | Telemetry DTOs, pit data, weather data, track positions |
| 9 вЂ” LMU integration | Not started | Second sim provider, DTO gap handling, multi-sim switching |
| 10 вЂ” Overlays (Part 1) | Not started | Input Telemetry, Fuel Calculator, Standings |
| 11 вЂ” Overlays (Part 2) | Not started | Pit Helper, Weather, Flat Track Map |
| 12 вЂ” OBS Mode & UX | Not started | OBS Mode polish, session profiles, hotkeys, buddy list, columns |

---

## Key Constraints (do not violate without updating DECISIONS.md)

- No `WS_EX_TOOLWINDOW` on overlay windows вЂ” breaks OBS window picker
- No WPF/GDI/Electron for overlay rendering вЂ” violates resource usage goal
- `WS_EX_LAYERED` required on overlay windows вЂ” ULW rendering pipeline depends on it
- `WS_EX_NOREDIRECTIONBITMAP` must NOT be set вЂ” makes ULW windows invisible
- Only one `ISimProvider` active at a time вЂ” no concurrent sim polling
- Config writes are always atomic (`*.tmp` в†’ `File.Move`)
- Render thread never blocks on data thread and vice versa вЂ” snapshot pattern only
- Position (X/Y) is never part of stream override config in single-window mode

