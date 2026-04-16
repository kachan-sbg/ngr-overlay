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


## Stable dotnet commands (avoid restore/test stalls in this env)
Use serialized restore/build/test and disable NuGet audit in command line:

```powershell
dotnet restore NrgOverlay.sln /m:1 -p:NuGetAudit=false
```

```powershell
dotnet build src\NrgOverlay.App\NrgOverlay.App.csproj -c Debug /m:1 -p:NuGetAudit=false
```

```powershell
dotnet test tests\NrgOverlay.Core.Tests\NrgOverlay.Core.Tests.csproj /m:1 -p:NuGetAudit=false
```

```powershell
dotnet test tests\NrgOverlay.App.Tests\NrgOverlay.App.Tests.csproj /m:1 -p:NuGetAudit=false
```

```powershell
dotnet test tests\NrgOverlay.Overlays.Tests\NrgOverlay.Overlays.Tests.csproj /m:1 -p:NuGetAudit=false
```

Fast rerun (after successful restore/build):

```powershell
dotnet test tests\NrgOverlay.Core.Tests\NrgOverlay.Core.Tests.csproj --no-restore /m:1 -p:NuGetAudit=false
```

If a previous run is stuck, stop stale processes first:

```powershell
Get-Process dotnet,MSBuild -ErrorAction SilentlyContinue | Stop-Process -Force
```

## 2026-04-16 iRacing Stability Updates (shipped)

### Critical reliability hardening
- Added `IRacingConnectionProbe` (`src/NrgOverlay.Sim.iRacing/IRacingConnectionProbe.cs`) so all iRacing MMF status reads are centralized and explicitly resilient to `FileNotFoundException`, `UnauthorizedAccessException`, and `IOException`.
- `IRacingProvider.IsRunning()` now reads via probe abstraction and remains fail-safe on probe exceptions.
- `IRacingPoller` watchdog now uses a dedicated thread-safe controller (`IRacingWatchdogController`) to prevent concurrent timer callbacks from triggering overlapping SDK restart attempts.
- Watchdog connectivity checks now use the same probe abstraction (single source of truth).

### Added iRacing resilience tests
- `tests/NrgOverlay.Sim.iRacing.Tests/IRacingSharedMemoryStabilityTests.cs`
  - missing map
  - connected-bit decode
  - randomized create/update/close under concurrent probe reads
  - dual-map isolation
  - provider behavior when probe throws
- `tests/NrgOverlay.Sim.iRacing.Tests/IRacingWatchdogControllerTests.cs`
  - threshold restart behavior
  - restart suppression behavior
  - concurrent tick single-restart reservation
  - restart-cycle reset behavior

## Known Stalled Runs / Manual Handoff

When running commands in this environment, these patterns are known to stall/fail intermittently:
- Running multiple `dotnet` build/test commands in parallel can lock intermediate files (for example `*.deps.json`) and produce transient failures.
- Some direct project builds may fail with generic output (`Build FAILED` with no compiler diagnostics) even when targeted tests pass.
- Broad repo scans with heavy recursion can also time out or be interrupted.

### Required behavior for future runs
- Never run parallel `dotnet` build/test commands.
- Prefer single-project `dotnet test` for verification.
- If a command is important and appears stalled (>90s with no meaningful progress) or fails without actionable diagnostics:
  1. stop it,
  2. report exact command,
  3. ask user to run locally and share output.

### Suggested user-run commands when agent environment is unreliable
```powershell
dotnet test tests\NrgOverlay.Sim.iRacing.Tests\NrgOverlay.Sim.iRacing.Tests.csproj /m:1 -p:NuGetAudit=false
```
```powershell
dotnet test tests\NrgOverlay.Overlays.Tests\NrgOverlay.Overlays.Tests.csproj /m:1 -p:NuGetAudit=false
```
```powershell
dotnet test tests\NrgOverlay.App.Tests\NrgOverlay.App.Tests.csproj /m:1 -p:NuGetAudit=false
```
