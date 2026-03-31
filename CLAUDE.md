# SimOverlay — Claude Code Context

## What this is
A Windows racing simulator overlay app. Renders transparent HUD overlays on top of racing sims (iRacing first, others later) using Direct2D + DirectComposition. Performance is the primary design constraint — no WPF/Electron/Qt in the rendering path.

## Collaboration model
- User drives product direction, priorities, and review
- Claude writes all code
- Always check `docs/TASKS.md` for what to implement next and current status

## Platform
**Windows only.** Target framework is `net8.0-windows`. Never run `dotnet` or `msbuild` commands locally — the user builds and verifies on a Windows machine. Just write the files.

## Full documentation
All design docs live in `docs/`:
- `README.md` — master index and key constraints
- `ARCHITECTURE.md` — full technical architecture, dependency rules, config schema, thread model
- `OVERLAYS.md` — per-overlay layout specs (column widths, colors, field list)
- `TASKS.md` — phased task breakdown with acceptance criteria (source of truth for what to build)
- `DECISIONS.md` — chronological decision log with rationale

**Read `ARCHITECTURE.md` and the relevant task from `TASKS.md` before writing any code.**

## Solution structure

```
SimOverlay.sln
├── src/
│   ├── SimOverlay.Core/          — domain types, config schema, ISimDataBus
│   ├── SimOverlay.Rendering/     — Direct2D/DirectComposition, OverlayWindow, BaseOverlay
│   ├── SimOverlay.Sim.Contracts/ — ISimProvider, normalized DTOs (SessionData, DriverData, RelativeData)
│   ├── SimOverlay.Sim.iRacing/   — iRSDK MMF reader, 60 Hz polling thread
│   ├── SimOverlay.Overlays/      — RelativeOverlay, SessionInfoOverlay, DeltaBarOverlay
│   └── SimOverlay.App/           — entry point, tray icon, SimDetector, settings window
└── tests/
    ├── SimOverlay.Core.Tests/
    ├── SimOverlay.Sim.iRacing.Tests/
    └── SimOverlay.Overlays.Tests/
```

### Dependency rules (strict — no exceptions)
- `Core` has zero project dependencies
- `Sim.Contracts` depends only on `Core`
- `Sim.iRacing` depends on `Sim.Contracts` + `Core`
- `Rendering` depends only on `Core`
- `Overlays` depends on `Rendering` + `Sim.Contracts` + `Core`
- `App` depends on everything; nothing depends on `App`
- `Sim.*` projects must NOT depend on `Rendering` or `Overlays`

## Tech stack
- C# / .NET 8 (`net8.0-windows`), x64 only
- **Rendering:** `Vortice.Direct2D1`, `Vortice.DirectComposition`, `Vortice.DXGI`, `Vortice.Direct3D11`
- **iRacing SDK:** `IRSDKSharper` NuGet package
- **DI:** `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Hosting`
- **Config:** `System.Text.Json` (BCL, no extra package)
- **Settings UI:** WPF (`net8.0-windows`)
- **Tests:** xUnit

## Key architectural decisions

### Window setup
- `WS_POPUP | WS_VISIBLE` + `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOPMOST`
- **`WS_EX_TOOLWINDOW` is intentionally omitted** — it hides windows from OBS's window picker
- Fixed window titles: `SimOverlay — Relative`, `SimOverlay — Session Info`, `SimOverlay — Delta`

### DXGI swap chain
- `DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL`, `DXGI_ALPHA_MODE_PREMULTIPLIED`, `DXGI_FORMAT_B8G8R8A8_UNORM`
- All geometry drawn with premultiplied alpha
- Clear each frame to `{0, 0, 0, 0}` (fully transparent)

### Thread model
- **UI thread:** Win32 message pump, all HWNDs created here
- **Render thread(s):** one per overlay, 60 fps `Stopwatch`-based loop
- **Data thread:** one per active `ISimProvider` (iRacing: 60 Hz)
- **Detection thread:** `ThreadPool` timer, 2 s interval
- `ID2D1Factory` created with `D2D1_FACTORY_TYPE_MULTI_THREADED`

### Data flow
`IRacingPoller` (60 Hz) → `ISimDataBus.Publish<T>()` → overlay `OnDataUpdate()` stores snapshot → render loop reads snapshot → `OnRender()`

`OnDataUpdate()` must be fast (just a field store — no rendering, no heavy locking).

### Stream override system
- Each overlay has a base `OverlayConfig` + optional `StreamOverrideConfig`
- `StreamOverrideConfig` has all visual fields as `nullable` — `null` = inherit from base
- X/Y position is **never** overridable (shared between profiles)
- `OverlayConfig.Resolve(bool streamModeActive)` returns effective config: `streamOverride.Field ?? base.Field`
- Resize in stream mode → saves to `StreamOverride.Width/Height`; position always saves to base
- `globalSettings.streamModeActive` is persisted (survives restarts)

### Config file
- Location: `%APPDATA%\SimOverlay\config.json`
- Atomic save: serialize → write to `.tmp` → `File.Move(overwrite: true)`
- Position/size debounced 500 ms on `WM_MOVE`/`WM_SIZE`

### OBS capture
- OBS Window Capture (WGC method) + "Allow Transparency" → correct alpha without chroma key
- WGC captures at DWM compositor level — works with `WS_EX_NOREDIRECTIONBITMAP`
- BitBlt/legacy OBS does not work — documented limitation

## MVP overlays (Phase 4)
1. **Relative** — ~15 drivers around player: POS, CAR, NAME, iRTG, LIC, GAP, LAP columns
2. **Session Info** — track, session type, time remaining, temps, lap/best/delta
3. **Delta Bar** — animated bar, green=faster/red=slower vs best lap, trend arrow

## Current status
- Phase 0 (Scaffolding): not started — start with TASK-001
- All subsequent phases: not started
- Docs (TASK-004): done — they are in `docs/`
