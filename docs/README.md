# SimOverlay — Project Documentation

This is the single entry point for all project documentation. Start here.

---

## What is SimOverlay?

A Windows desktop overlay application for racing simulators. Transparent, always-on-top windows that display real-time telemetry data (relative standings, lap times, delta, session info) directly over the sim. Designed for three priorities, in order:

1. **Minimal resource usage** — Direct2D + DirectComposition rendering with no CPU framebuffer copy. No WPF, no Electron, no Python.
2. **UI customization** — Every overlay has independently configurable colors, opacity, size, and font. Each overlay also has an optional stream override profile (see below).
3. **OBS capture compatibility** — Each overlay is an independent OBS Window Capture source with real alpha transparency. No chroma key needed.

---

## Core Concepts

### Multi-sim, one active at a time
The app auto-detects which sim is running and activates that data provider. Only one sim is polled at a time. Architecture supports adding new sims (ACC, AC, LMU, rF2) without changing rendering or overlay code.

### Multiple independent overlays
Each overlay (Relative, Session Info, Delta Bar, …) is its own transparent window. They can be independently positioned, sized, enabled/disabled, and captured in OBS. In **edit mode** (toggled via tray icon) windows accept mouse input for drag and resize; in normal mode they are fully click-through.

### Stream override (dual-profile)
Each overlay has a **Screen profile** (the driver's view) and an optional **Stream override profile** (for viewers). When **stream mode** is toggled (tray icon or settings window), overlays with an enabled override switch to the stream profile — same window, different appearance. The driver might use a minimal 5-column relative; viewers see a 10-column colourful version. Toggle once before going live, toggle back after.

---

## Document Index

| Document | Purpose |
|---|---|
| **[README.md](README.md)** | This file. Project overview and navigation. |
| **[DECISIONS.md](DECISIONS.md)** | Chronological log of every significant design decision and why it was made. Read this to understand *why* the project is shaped the way it is. |
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Full technical architecture: solution structure, rendering pipeline, data flow, config system, OBS integration, stream override model, extensibility. |
| **[OVERLAYS.md](OVERLAYS.md)** | Specification for each MVP overlay: layout, data fields, update frequency, customization options. |
| **[TASKS.md](TASKS.md)** | Ordered implementation task list grouped by phase. Each task has description, acceptance criteria, and dependencies. Updated as work progresses. |

---

## Solution Structure

```
SimOverlay.sln
├── src/
│   ├── SimOverlay.Core            # Domain types, config schema, ISimDataBus
│   ├── SimOverlay.Rendering       # Direct2D + DirectComposition, BaseOverlay
│   ├── SimOverlay.Sim.Contracts   # ISimProvider interface + normalized DTOs
│   ├── SimOverlay.Sim.iRacing     # iRSDK shared memory reader, 60 Hz poller
│   ├── SimOverlay.Overlays        # Relative, SessionInfo, DeltaBar overlays
│   └── SimOverlay.App             # Entry point, tray icon, SimDetector, settings UI
├── tests/
│   ├── SimOverlay.Core.Tests
│   ├── SimOverlay.Sim.iRacing.Tests
│   └── SimOverlay.Overlays.Tests
└── docs/                          # ← you are here
```

Dependency rule: `Core` ← `Sim.Contracts` ← `Sim.iRacing`. `Core` ← `Rendering` ← `Overlays` ← `App`. Nothing may depend on `App`. `Sim.*` projects may not depend on `Rendering` or `Overlays`.

---

## Technology Stack

| Layer | Technology | Why |
|---|---|---|
| Language | C# / .NET 8 | Productive, native Windows APIs accessible via P/Invoke, AOT-compilable |
| Overlay rendering | Direct2D + DirectComposition (`WS_EX_NOREDIRECTIONBITMAP`) | GPU-only compositing — no CPU framebuffer copy, no VRR conflicts |
| DirectX bindings | Vortice.Windows (NuGet) | Thin managed wrappers, no COM interop boilerplate |
| iRacing data | iRSDK shared memory (`Local\IRSDKMemMapFileName`) | Official iRacing API, 60 Hz, language-agnostic |
| Config storage | `System.Text.Json` → `%APPDATA%\SimOverlay\config.json` | Zero dependencies, atomic writes |
| Settings UI | WPF | Only used for the settings window, not for overlays |
| DI container | `Microsoft.Extensions.DependencyInjection` | Standard, minimal overhead |
| iRacing SDK wrapper | IRSDKSharper (NuGet) | Handles MMF connection lifecycle |

---

## MVP Overlays

| Overlay | Window Title | Data | Update Rate |
|---|---|---|---|
| **Relative** | `SimOverlay — Relative` | ~15 drivers around player: position, car#, name, iRating, license, gap, lap delta | 10 Hz |
| **Session Info** | `SimOverlay — Session Info` | Track, session type, time remaining, temps, last/best lap, lap#, wall clock | 60 Hz (driver data) + 1 Hz (session) |
| **Delta Bar** | `SimOverlay — Delta` | Real-time delta vs best lap, animated bar (green = faster, red = slower), trend indicator | 60 Hz |

---

## Implementation Phases

| Phase | Status | Focus |
|---|---|---|
| 0 — Scaffolding | Not started | Solution, projects, NuGet packages, CI |
| 1 — Rendering core | Not started | SimDataBus, ConfigStore, transparent window, Direct2D, render loop, edit mode |
| 2 — iRacing data | Not started | MMF connection, 60 Hz poller, session YAML, relative calculator |
| 3 — Overlay framework | Not started | Overlay manager, position persistence, live config updates, sim state display |
| 4 — MVP overlays | Not started | Relative, Session Info, Delta Bar implementations |
| 5 — Settings UI | Not started | WPF settings window, color pickers, stream override editor, tray icon |
| 6 — Polish | Not started | Auto sim detection, single instance, logging, perf profiling, installer |

Update the Status column above as phases complete.

---

## Key Constraints (do not violate without updating DECISIONS.md)

- No `WS_EX_TOOLWINDOW` on overlay windows — breaks OBS window picker
- No WPF/GDI/Electron for overlay rendering — violates resource usage goal
- Position (X/Y) is never part of stream override config — position is always shared between profiles
- Only one `ISimProvider` active at a time — no concurrent sim polling
- Config writes are always atomic (`*.tmp` → `File.Move`)
- Render thread never blocks on data thread and vice versa — snapshot pattern only
