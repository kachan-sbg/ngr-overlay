# NrgOverlay вЂ” Claude Code Context

## What this is
Windows racing simulator overlay app. Transparent HUD overlays on top of racing sims (iRacing, LMU) using Direct2D + UpdateLayeredWindow. Performance is the primary design constraint.

## Collaboration model
- User drives direction and review; Claude writes all code and may run `dotnet` (build, test, benchmark)
- Repo: `G:\ngr-overlay` | Target: `net8.0-windows`, x64

## Current task
**v0.0.1 Alpha shipped** вЂ” Phases 7вЂ“11 `[x]`. Pre-Phase-13 live-session hotfixes landed 2026-04-12.

**Active: Phase 13 вЂ” Data Validation & Audit**
First task: **TASK-1301** (iRacing field audit). See [`docs/tasks/PHASE-13-data-validation.md`](docs/tasks/PHASE-13-data-validation.md).
Roadmap: [`docs/ROADMAP.md`](docs/ROADMAP.md) | Phase 12 (OBS Mode) deferred.

**In-session work (not yet committed):**
- Connection reliability: IRacingPoller watchdog + SafeGet pattern (all telemetry reads guarded)
- Relative overlay: garage car inclusion (`IsInGarage` on `RelativeEntry`), garage cars shown with "GAR" status

## Projects
| Project | Purpose |
|---|---|
| `NrgOverlay.Core` | Domain types, config, data bus, logger вЂ” no dependencies |
| `NrgOverlay.Sim.Contracts` | Sim-agnostic DTOs (SessionData, TelemetryData, RelativeDataвЂ¦) |
| `NrgOverlay.Sim.iRacing` | iRacing: IRSDKSharper wrapper, poller, session decoder, calculator |
| `NrgOverlay.Sim.LMU` | LMU: native shared memory, equivalent poller |
| `NrgOverlay.Rendering` | Direct2D + Win32 window вЂ” BaseOverlay, OverlayWindow, RenderResources |
| `NrgOverlay.Overlays` | Concrete overlay implementations (RelativeOverlay, StandingsOverlayвЂ¦) |
| `NrgOverlay.App` | Entry point, DI, OverlayManager, tray icon, Settings WPF UI |

в†’ File-level navigation: **[`docs/CODE-NAV.md`](docs/CODE-NAV.md)**

## Dependency rules
`Core` в†’ nothing | `Sim.Contracts` в†’ Core | `Sim.iRacing/LMU` в†’ Sim.Contracts+Core | `Rendering` в†’ Core | `Overlays` в†’ Rendering+Sim.Contracts+Core | `App` в†’ everything. **Sim.\* must NOT depend on Rendering or Overlays.**

## Critical constraints
- `WS_EX_LAYERED` required (ULW); **omit** `WS_EX_NOREDIRECTIONBITMAP` (makes ULW invisible) and `WS_EX_TOOLWINDOW` (hides from OBS)
- Render path: software `ID2D1DCRenderTarget` в†’ 32-bit premul-alpha DIB в†’ `UpdateLayeredWindow(ULW_ALPHA)`. No GPU in presentation.
- `OnDataUpdate()` must only store snapshot fields вЂ” no allocation, no blocking (called 60 Hz on data thread)
- OBS capture: Window Capture + "Allow Transparency". BitBlt mode doesn't work.

## Task completion checklist
After every task, before committing:
1. **Mark `[x]`** in `docs/tasks/PHASE-N-*.md`
2. **Same commit:** ARCHITECTURE.md (if changed), DECISIONS.md + `docs/decisions/alpha.md` (non-trivial decisions), INDEX.md
3. **Run tests** вЂ” `dotnet test`
4. **Run benchmarks** for hot-path changes (SimDataBus, OverlayConfig.Resolve, IRacingRelativeCalculator)
5. **Verify acceptance criteria** from the task doc
6. **Update "Current task"** above to next task
7. **Commit** code + docs together

