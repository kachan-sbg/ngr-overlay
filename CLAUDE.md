# SimOverlay — Claude Code Context

## What this is
Windows racing simulator overlay app. Transparent HUD overlays on top of racing sims (iRacing, LMU) using Direct2D + UpdateLayeredWindow. Performance is the primary design constraint.

## Collaboration model
- User drives product direction, priorities, and review
- Claude writes all code — never run `dotnet` or `msbuild` locally
- Target: `net8.0-windows`, x64 only

## Current task
**Phase 7 — Infrastructure hardening** `[~]` In progress
Next: **TASK-706** — Overlay registration and dynamic creation
File: `docs/tasks/PHASE-7-infrastructure.md`

## Codebase map

### Core (`src/SimOverlay.Core/`)
No project dependencies. Domain types, config, data bus.
- `Config/AppConfig.cs` — root: Version (int), GlobalSettings, List\<OverlayConfig\>
- `Config/ConfigStore.cs` — load/save JSON to `%APPDATA%\SimOverlay\config.json`, atomic write
- `Config/ConfigMigrator.cs` — sequential version migration pipeline (CurrentVersion=2)
- `Config/OverlayConfig.cs` — per-overlay POCO: position, size, colors, font, overlay-specific fields, StreamOverride, `Resolve(bool)`
- `Config/StreamOverrideConfig.cs` — nullable overrides for stream/OBS mode
- `Config/ColorConfig.cs` — RGBA float color with presets (White, Black, DarkBackground, etc.)
- `Config/GlobalSettings.cs` — StreamModeActive, StartWithWindows, SimPriorityOrder (List\<string\>)
- `Config/TemperatureUnit.cs` — enum: Celsius, Fahrenheit
- `SimDataBus.cs` / `ISimDataBus.cs` — pub/sub bus, `Publish<T>()` / `Subscribe<T>()`
- `SimState.cs` — enum: Disconnected, Connected, InSession
- `Events/` — EditModeChangedEvent, SimStateChangedEvent, StreamModeChangedEvent
- `AppLog.cs` — file logger to `%APPDATA%\SimOverlay\sim-overlay.log`

### Sim.Contracts (`src/SimOverlay.Sim.Contracts/`)
Depends: Core. Sim-agnostic DTOs.
- `ISimProvider.cs` — SimId, IsRunning(), Start(), Stop(), StateChanged event
- `SessionData.cs` — track, session type, temps, time (published ~1 Hz)
- `DriverData.cs` — position, laps, delta (published 60 Hz)
- `RelativeData.cs` / `RelativeEntry.cs` — relative list with gaps (published 10 Hz)
- `LicenseClass.cs` — enum: R, D, C, B, A, Pro, WC
- `SessionType.cs` — Practice, Qualify, Race, etc.

### Sim.iRacing (`src/SimOverlay.Sim.iRacing/`)
Depends: Sim.Contracts + Core. Uses `IRSDKSharper` NuGet.
- `IRacingProvider.cs` — ISimProvider implementation, connection lifecycle
- `IRacingPoller.cs` — wraps IRSDKSharper, 60 Hz events → bus publishing
- `IRacingSessionDecoder.cs` — YAML session info → SessionData + DriverSnapshot
- `IRacingRelativeCalculator.cs` — LapDistPct gap calc → RelativeData (10 Hz)
- `DriverSnapshot.cs` / `TelemetrySnapshot.cs` — intermediate data types

### Rendering (`src/SimOverlay.Rendering/`)
Depends: Core. Direct2D + Win32 plumbing.
- `OverlayWindow.cs` — Win32 HWND: WS_EX_LAYERED|TRANSPARENT|TOPMOST, ULW presentation
- `BaseOverlay.cs` — render loop (60fps), config, sim state, data subscriptions
- `RenderResources.cs` — brush/font/layout cache keyed by config
- `ZOrderHook.cs` — WinEvent hook to re-assert TOPMOST z-order
- `MessagePump.cs` — GetMessage/DispatchMessage loop
- `DeviceLostException.cs` — D2DERR_RECREATE_TARGET handling
- `Win32/NativeMethods.cs` — P/Invoke declarations

### Overlays (`src/SimOverlay.Overlays/`)
Depends: Rendering + Sim.Contracts + Core. Concrete overlay implementations.
- `RelativeOverlay.cs` — relative position board
- `SessionInfoOverlay.cs` — session/track/weather info
- `DeltaBarOverlay.cs` — lap delta visualization

### App (`src/SimOverlay.App/`)
Depends: everything. Entry point, orchestration.
- `Program.cs` — single-instance, DI container composition root, message pump
- `IOverlayFactory.cs` / `OverlayFactory.cs` — registry-based overlay factory (add overlay = write class + register here)
- `OverlayManager.cs` — owns overlays (via `IOverlayFactory`), edit/stream mode, preview/apply
- `SimDetector.cs` — polls ISimProvider every 2s, manages active provider
- `TrayIconController.cs` — NotifyIcon context menu
- `SingleInstanceGuard.cs` — named Mutex
- `Settings/SettingsWindow.xaml.cs` — WPF settings, lazy singleton
- `Settings/OverlaySettingsPanel.xaml.cs` — per-overlay settings (Screen + Stream Override tabs)
- `Settings/GlobalSettingsPanel.xaml.cs` — edit mode, stream mode, start with windows
- `Settings/OverlayConfigViewModel.cs` / `StreamOverrideViewModel.cs` — WPF ViewModels
- `Settings/ColorEditor.xaml.cs` / `ColorViewModel.cs` — RGBA editor
- `Settings/FieldRow.xaml.cs` / `OverrideRow.xaml.cs` / `EnumBoolConverter.cs` — helpers

### Tests (`tests/`)
- `SimOverlay.Core.Tests/` — ConfigStore, ConfigMigrator, OverlayConfig.Resolve, SimDataBus, LicenseClass
- `SimOverlay.Sim.iRacing.Tests/` — IRacingRelativeCalculator, IRacingSessionDecoder
- `SimOverlay.Overlays.Tests/` — overlay rendering tests
- `SimOverlay.Benchmarks/` — BenchmarkDotNet (not a test runner)

## Dependency rules (strict)
Core → (nothing) | Sim.Contracts → Core | Sim.iRacing → Sim.Contracts+Core | Rendering → Core | Overlays → Rendering+Sim.Contracts+Core | App → everything. Sim.* must NOT depend on Rendering or Overlays.

## Key constraints
- **Window styles:** `WS_EX_LAYERED` required (ULW). `WS_EX_NOREDIRECTIONBITMAP` omitted (makes ULW invisible). `WS_EX_TOOLWINDOW` omitted (hides from OBS picker).
- **Rendering:** software `ID2D1DCRenderTarget` → 32-bit premultiplied-alpha DIB → `UpdateLayeredWindow(ULW_ALPHA)`. No GPU in presentation path.
- **Threads:** UI (message pump) + render (one per overlay, 60fps) + data (one per sim, 60Hz) + detection (ThreadPool, 2s). D2D factory is `MULTI_THREADED`.
- **Data flow:** Poller (60Hz) → `ISimDataBus.Publish<T>()` → overlay `OnDataUpdate()` stores snapshot → render loop reads → `OnRender()`. OnDataUpdate must be fast (field store only).
- **Config:** `%APPDATA%\SimOverlay\config.json`. Atomic save. Position/size debounced 500ms. Version migration on load.
- **OBS:** Window Capture (WGC) + "Allow Transparency". BitBlt doesn't work (documented limitation).

## Docs reference (read only sections relevant to your task)
- `docs/ARCHITECTURE.md` — full architecture with section index at top
- `docs/OVERLAYS.md` — per-overlay layout specs (column widths, colors, field list)
- `docs/DECISIONS.md` — brief decision summary table (full entries in `docs/decisions/mvp.md` and `docs/decisions/alpha.md`)
- `docs/tasks/PHASE-N-*.md` — task details with acceptance criteria
- `docs/REVIEW-MVP.md` — post-MVP review; blocking issues for Alpha
- MVP-era docs archived in `docs/archive/mvp/`

## Tech stack
C# .NET 8 (`net8.0-windows`) | `Vortice.Direct2D1` | `IRSDKSharper` | `Microsoft.Extensions.DependencyInjection` | `System.Text.Json` | WPF (settings only) | xUnit | BenchmarkDotNet

## Task completion checklist
After completing every task, before committing:
1. **Mark the task `[x]`** in its phase file (`docs/tasks/PHASE-N-*.md`)
2. **Update docs in the same commit:** ARCHITECTURE.md (if changed), DECISIONS.md summary table + `docs/decisions/alpha.md` full entry (if non-trivial decision), INDEX.md + TASKS.md (phase status)
3. **Ask user to run tests** — `dotnet test`
4. **Ask user to run benchmarks** for hot-path changes (SimDataBus, OverlayConfig.Resolve, IRacingRelativeCalculator)
5. **Verify acceptance criteria** — after tests pass, review each criterion in the task and confirm it is met before committing
6. **Update "Current task" section above** to point to the next task
7. **Commit** code + docs + task status together
