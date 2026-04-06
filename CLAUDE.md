# SimOverlay — Claude Code Context

## What this is
A Windows racing simulator overlay app. Renders transparent HUD overlays on top of racing sims (iRacing, LMU) using Direct2D + UpdateLayeredWindow. Performance is the primary design constraint — no WPF/Electron/Qt in the rendering path.

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
- `REVIEW-MVP.md` — post-MVP code and architecture review; blocking issues for Alpha

MVP-era docs are archived in `docs/archive/mvp/`.

**Read `ARCHITECTURE.md` and the relevant task from `docs/tasks/` before writing any code.**

## Solution structure

```
SimOverlay.sln
├── src/
│   ├── SimOverlay.Core/          — domain types, config schema, ISimDataBus
│   ├── SimOverlay.Rendering/     — Direct2D + UpdateLayeredWindow, OverlayWindow, BaseOverlay
│   ├── SimOverlay.Sim.Contracts/ — ISimProvider, normalized DTOs (SessionData, DriverData, RelativeData)
│   ├── SimOverlay.Sim.iRacing/   — iRSDK MMF reader, 60 Hz polling thread
│   ├── SimOverlay.Sim.LMU/       — rFactor 2 shared memory reader (Alpha Phase 9)
│   ├── SimOverlay.Overlays/      — RelativeOverlay, SessionInfoOverlay, DeltaBarOverlay, + Alpha overlays
│   └── SimOverlay.App/           — entry point, tray icon, SimDetector, settings window
└── tests/
    ├── SimOverlay.Core.Tests/
    ├── SimOverlay.Sim.iRacing.Tests/
    ├── SimOverlay.Overlays.Tests/
    └── SimOverlay.Benchmarks/    — BenchmarkDotNet (not a test runner)
```

### Dependency rules (strict — no exceptions)
- `Core` has zero project dependencies
- `Sim.Contracts` depends only on `Core`
- `Sim.iRacing` depends on `Sim.Contracts` + `Core`
- `Sim.LMU` depends on `Sim.Contracts` + `Core`
- `Rendering` depends only on `Core`
- `Overlays` depends on `Rendering` + `Sim.Contracts` + `Core`
- `App` depends on everything; nothing depends on `App`
- `Sim.*` projects must NOT depend on `Rendering` or `Overlays`

## Tech stack
- C# / .NET 8 (`net8.0-windows`), x64 only
- **Rendering:** `Vortice.Direct2D1` — software `ID2D1DCRenderTarget` + `UpdateLayeredWindow`
- **iRacing SDK:** `IRSDKSharper` NuGet package
- **LMU/rF2 SDK:** rFactor 2 shared memory (raw P/Invoke or community library — see TASK-901)
- **DI:** `Microsoft.Extensions.DependencyInjection`
- **Config:** `System.Text.Json` (BCL, no extra package)
- **Settings UI:** WPF (`net8.0-windows`)
- **Tests:** xUnit
- **Benchmarks:** BenchmarkDotNet

## Key architectural decisions

### Window setup
- `WS_POPUP` + `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST`
- `WS_EX_LAYERED` is **required** — rendering uses `UpdateLayeredWindow`
- `WS_EX_NOREDIRECTIONBITMAP` is **intentionally omitted** — makes ULW windows invisible
- **`WS_EX_TOOLWINDOW` is intentionally omitted** — hides windows from OBS's window picker
- Fixed window titles: `SimOverlay — Relative`, `SimOverlay — Session Info`, `SimOverlay — Delta`, etc.

### Rendering pipeline
- `ID2D1DCRenderTarget` (software/CPU) renders into a 32-bit premultiplied-alpha GDI DIB
- `UpdateLayeredWindow(ULW_ALPHA)` presents to DWM each frame
- No GPU resources in the presentation path — avoids interference with game DXGI flip chains

### Thread model
- **UI thread:** Win32 message pump, all HWNDs created here
- **Render thread(s):** one per overlay, 60 fps `Stopwatch`-based loop
- **Data thread:** one per active `ISimProvider` (iRacing: 60 Hz)
- **Detection thread:** `ThreadPool` timer, 2 s interval
- `ID2D1Factory` created with `D2D1_FACTORY_TYPE_MULTI_THREADED`

### Data flow
`IRacingPoller` / `LmuPoller` (60 Hz) → `ISimDataBus.Publish<T>()` → overlay `OnDataUpdate()` stores snapshot → render loop reads snapshot → `OnRender()`

`OnDataUpdate()` must be fast (just a field store — no rendering, no heavy locking).

### OBS Mode / Stream override system
- Each overlay has a base `OverlayConfig` + optional stream override ("OBS Profile")
- Override has all visual fields as `nullable` — `null` = inherit from base
- X/Y position is **never** overridable (shared between profiles)
- `OverlayConfig.Resolve(bool obsModeActive)` returns effective config
- Single window — OBS Mode toggle changes appearance instantly. No dual-window needed for Alpha.

### Config file
- Location: `%APPDATA%\SimOverlay\config.json`
- Atomic save: serialize → write to `.tmp` → `File.Move(overwrite: true)`
- Position/size debounced 500 ms on `WM_MOVE`/`WM_SIZE`
- Config versioning: `AppConfig.Version` field — migration runs on load (TASK-701)

### OBS capture
- OBS Window Capture (WGC method) + "Allow Transparency" → correct alpha without chroma key
- WGC captures at DWM compositor level — works with `WS_EX_LAYERED` + ULW
- BitBlt/legacy OBS does not work — documented limitation

## Current status
- MVP (Phases 0–6): **complete**
- Alpha (Phases 7–12): not started — start with TASK-701 in `docs/tasks/PHASE-7-infrastructure.md`

## Task completion checklist

After completing every task, before committing:

1. **Mark the task `[x]`** in its phase file (`docs/tasks/PHASE-N-*.md`)
2. **Update docs in the same commit:**
   - `ARCHITECTURE.md` — any section whose description no longer matches the code
   - `DECISIONS.md` — add an entry for any non-trivial design decision made during the task
   - `docs/tasks/INDEX.md` — update phase status (`[ ]` → `[~]` → `[x]`) as phases progress
   - `docs/TASKS.md` — update phase status line if a phase completes
   - `docs/README.md` — update implementation status table if a phase completes
3. **Ask the user to run tests** — do not commit if tests are expected to fail:
   - `dotnet test` across all test projects
   - For data pipeline / calculator changes: confirm unit tests pass
4. **Ask the user to run benchmarks** for any changes to hot paths:
   - `SimDataBus`, `OverlayConfig.Resolve`, `IRacingRelativeCalculator`, or any new 60 Hz path
   - Compare against baseline in `benchmarks/baseline/`
5. **Commit** everything together (code + docs + updated task status) in one commit

Never let "I'll update docs later" happen. The post-Phase-2 audit showed that stale docs cost a full session to recover from.
