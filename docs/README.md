# NrgOverlay — Project Documentation

This is the single entry point for all project documentation. Start here.

---

## What is NrgOverlay?

A Windows desktop overlay application for racing simulators. Transparent, always-on-top windows that display real-time telemetry data (relative standings, lap times, delta, session info, fuel, inputs, and more) directly over the sim. Designed for three priorities, in order:

1. **Minimal resource usage** — Software Direct2D rendering + `UpdateLayeredWindow` presentation. No WPF, no Electron, no Python on the render path.
2. **UI customization** - Every overlay has independently configurable colors, opacity, size, and font.
3. **OBS capture compatibility** — Each overlay is an independent OBS Window Capture source with real alpha transparency. No chroma key needed.

---

## Core Concepts

### Multi-sim, one active at a time
The app auto-detects which sim is running and activates that data provider. Only one sim is polled at a time. Architecture supports adding new sims (ACC, AC, LMU, rF2) without changing rendering or overlay code.

### Multiple independent overlays
Each overlay (Relative, Session Info, Delta Bar, Fuel, Input, Standings, ...) is its own transparent window. They can be independently positioned, sized, enabled/disabled, and captured in OBS. In **edit mode** (toggled via tray icon) windows accept mouse input for drag and resize; in normal mode they are fully click-through.

## Document Index

| Document | Purpose |
|---|---|
| **[README.md](README.md)** | This file. Project overview and navigation. |
| **[DECISIONS.md](DECISIONS.md)** | Chronological log of every significant design decision and why it was made. |
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Full technical architecture: solution structure, rendering pipeline, data flow, config system, OBS integration. |
| **[OVERLAYS.md](OVERLAYS.md)** | Specification for each overlay: layout, data fields, update frequency, customization options. |
| **[TASKS.md](TASKS.md)** | Ordered implementation task list grouped by phase. |
| **[CRITICAL-ISSUES.md](CRITICAL-ISSUES.md)** | Crash/hang/data-corruption risks and reliability-hardening status. |
| **[PROJECT-MEMORY.md](PROJECT-MEMORY.md)** | Operational memory: known stalled runs and practical verification workflow. |
| **[DEBUG-RUNBOOK.md](DEBUG-RUNBOOK.md)** | Step-by-step incident diagnostics for startup, SDK lifecycle, and coexistence scenarios. |
| **[AI-DOCS-GUIDE.md](AI-DOCS-GUIDE.md)** | AI-first documentation loading policy (minimal context, archive usage rules). |
| **[REVIEW-MVP.md](REVIEW-MVP.md)** | Code and architecture review conducted after MVP completion. |
| **[archive/mvp/](archive/mvp/README.md)** | Archived MVP-era documentation (phases 0–6, known issues, verification checklists). |

---

## Solution Structure

```
NrgOverlay.sln
├── src/
│   ├── NrgOverlay.Core            # Domain types, config schema, ISimDataBus
│   ├── NrgOverlay.Rendering       # Direct2D + UpdateLayeredWindow, BaseOverlay
│   ├── NrgOverlay.Sim.Contracts   # ISimProvider interface + normalized DTOs
│   ├── NrgOverlay.Sim.iRacing     # iRSDK shared memory reader, 60 Hz poller
│   ├── NrgOverlay.Overlays        # Relative, SessionInfo, DeltaBar, + Alpha overlays
│   └── NrgOverlay.App             # Entry point, tray icon, SimDetector, settings UI
├── tests/
│   ├── NrgOverlay.Core.Tests
│   ├── NrgOverlay.Sim.iRacing.Tests
│   ├── NrgOverlay.Overlays.Tests
│   └── NrgOverlay.Benchmarks
└── docs/
```

Dependency rule: `Core` ← `Sim.Contracts` ← `Sim.iRacing`. `Core` ← `Rendering` ← `Overlays` ← `App`. Nothing may depend on `App`. `Sim.*` projects may not depend on `Rendering` or `Overlays`.

---

## Technology Stack

| Layer | Technology | Why |
|---|---|---|
| Language | C# / .NET 8 | Productive, native Windows APIs accessible via P/Invoke |
| Overlay rendering | Direct2D (`ID2D1DCRenderTarget`) + `UpdateLayeredWindow` | Software CPU rendering avoids GPU interference with game flip chains |
| DirectX bindings | Vortice.Windows (NuGet) | Thin managed wrappers, no COM interop boilerplate |
| iRacing data | iRSDK shared memory (`Local\IRSDKMemMapFileName`) | Official iRacing API, 60 Hz |
| Config storage | `System.Text.Json` → `%APPDATA%\NrgOverlay\config.json` | Zero dependencies, atomic writes |
| Settings UI | WPF | Only used for the settings window, not for overlays |
| iRacing SDK wrapper | IRSDKSharper (NuGet) | Handles MMF connection lifecycle |

---

## Overlays

### MVP (Implemented)
| Overlay | Window Title | Update Rate |
|---|---|---|
| **Relative** | `NrgOverlay — Relative` | 10 Hz |
| **Session Info** | `NrgOverlay — Session Info` | 1–60 Hz |
| **Delta Bar** | `NrgOverlay — Delta` | 60 Hz |

### Alpha (Planned)
| Overlay | Window Title | Phase |
|---|---|---|
| **Input Telemetry** | `NrgOverlay — Input` | 10 |
| **Fuel Calculator** | `NrgOverlay — Fuel` | 10 |
| **Standings** | `NrgOverlay — Standings` | 10 |
| **Pit Helper** | `NrgOverlay — Pit` | 11 |
| **Weather** | `NrgOverlay — Weather` | 11 |
| **Flat Track Map** | `NrgOverlay — Track Map` | 11 |

See [OVERLAYS.md](OVERLAYS.md) for detailed specifications.

---

## Implementation Status

### MVP (Complete)
| Phase | Focus |
|---|---|
| 0 — Scaffolding | Solution, projects, NuGet packages |
| 1 — Rendering core | SimDataBus, ConfigStore, transparent window, Direct2D, render loop |
| 2 — iRacing data | MMF connection, 60 Hz poller, session decoder, relative calculator |
| 3 — Overlay framework | Overlay manager, position persistence, live config, sim state |
| 4 — MVP overlays | Relative, Session Info, Delta Bar |
| 5 — Settings UI | WPF settings window, per-overlay panels, tray icon |
| 6 — Polish | SimDetector, single-instance, icon, logging, benchmarks |

### Alpha (Current)
| Phase | Status | Focus |
|---|---|---|
| 7 — Infrastructure | Not started | Config versioning, DI, tech debt, multi-class data model |
| 8 — Data pipeline | Not started | Telemetry DTOs, pit data, weather data, track positions |
| 9 — LMU integration | Not started | Second sim provider, DTO gap handling, multi-sim switching |
| 10 — Overlays (Part 1) | Not started | Input Telemetry, Fuel Calculator, Standings |
| 11 — Overlays (Part 2) | Not started | Pit Helper, Weather, Flat Track Map |
| 12 — OBS Mode & UX | Not started | OBS Mode polish, session profiles, hotkeys, buddy list, columns |

---

## Key Constraints (do not violate without updating DECISIONS.md)

- No `WS_EX_TOOLWINDOW` on overlay windows — breaks OBS window picker
- No WPF/GDI/Electron for overlay rendering — violates resource usage goal
- `WS_EX_LAYERED` required on overlay windows — ULW rendering pipeline depends on it
- `WS_EX_NOREDIRECTIONBITMAP` must NOT be set — makes ULW windows invisible
- Only one `ISimProvider` active at a time — no concurrent sim polling
- Config writes are always atomic (`*.tmp` → `File.Move`)
- Render thread never blocks on data thread and vice versa — snapshot pattern only


---

### Streamer mode removal (2026-04-16)
- Stream mode and per-overlay stream override were removed from runtime code and settings UI.
- Overlay sizing now always persists to base overlay config.
- Chat overlay/widget was removed from the overlay registry and source tree.

## Recent Reliability Updates (2026-04-16)

### iRacing shared-memory and watchdog hardening
- Added a dedicated iRacing MMF status probe (`IRacingConnectionProbe`) and switched provider/poller checks to use it.
- Hardened `IsRunning()` behavior to stay fail-safe for MMF open/read exceptions.
- Added a thread-safe watchdog controller (`IRacingWatchdogController`) to prevent overlapping restart attempts when timer callbacks race.

### New stability test coverage
- `IRacingSharedMemoryStabilityTests`
  - random MMF create/update/close concurrency
  - dual-map isolation behavior
  - exception/failure-path behavior
- `IRacingWatchdogControllerTests`
  - stall threshold and suppression behavior
  - concurrent callback single-restart reservation
  - restart-cycle reset behavior

These changes target race-critical reliability issues where iRacing/OBS/other overlay apps can be destabilized by restart churn and MMF lifecycle races.

### Environment note for contributors
In this agent environment, broad or parallel `dotnet` runs can hang or lock outputs. Use serialized single-project test runs first. If an important run stalls or returns non-diagnostic failure output, run it locally and attach logs.

Known stalled-prone runs in agent sessions:
- `dotnet build NrgOverlay.sln` (full-solution build)
- broad multi-project `dotnet test` invocations against the whole repo


