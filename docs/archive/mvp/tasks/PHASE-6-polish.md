# Phase 6 — Polish and Integration `[~]`

> [← Index](INDEX.md)

---

**TASK-601** `[x]`
- **Title**: `SimDetector` — automatic sim detection loop
- **Description**: Implement `SimDetector` in `SimOverlay.App`. Run a `System.Threading.Timer` every 2000 ms. On each tick: if no provider active, iterate registered providers in priority order, call `IsRunning()`. If true, call `Start()` and mark active. If active provider's `IsRunning()` returns false, call `Stop()`, clear active, resume polling. Fire `ActiveProviderChanged` event.
- **Acceptance Criteria**: iRacing start detected within ~2 seconds. iRacing close detected within ~2 seconds. Priority order respected. Unit-testable with simulated `IsRunning()`.
- **Dependencies**: TASK-201, TASK-304.

---

**TASK-602** `[x]`
- **Title**: Single-instance enforcement
- **Description**: In `Program.cs`, acquire named `Mutex` (`Global\SimOverlay_SingleInstance`) before showing UI. If already held: find existing instance's tray window, post message to bring Settings to foreground, exit. Use `CreateMutex` via P/Invoke.
- **Acceptance Criteria**: Second instance does not create a second set of overlays. Existing instance's Settings window comes to foreground.
- **Dependencies**: TASK-504.

---

**TASK-603** `[x]`
- **Title**: Application icon and resources
- **Description**: Create `.ico` file (16×16, 32×32, 48×48, 256×256). Set as assembly icon in `SimOverlay.App.csproj`. Use for `NotifyIcon`. Add `Resources/` folder in `App`.
- **Acceptance Criteria**: App has a recognizable icon in Explorer, taskbar, and tray.
- **Dependencies**: TASK-504.

---

**TASK-604** `[x]`
- **Title**: Error logging
- **Description**: Add `Microsoft.Extensions.Logging` with a file sink writing to `%APPDATA%\SimOverlay\sim-overlay.log`. Log: sim connect/disconnect, device lost/recovery, config load/save, unhandled exceptions. Cap at 10 MB with single rotation (`.log.bak`). Set `WPF Application.DispatcherUnhandledException` and `AppDomain.CurrentDomain.UnhandledException` to log and display an error dialog before exiting.
- **Acceptance Criteria**: Log file contains connection and rendering events after a session. Unhandled exception produces log entry + dialog, not a silent crash.
- **Dependencies**: TASK-001.

---

---

**POST-601** `[x]` *(bug fix, not a planned task)*
- **Title**: Overlay windows appearing in Windows taskbar
- **Root cause**: `OverlayWindow` created with `hWndParent: nint.Zero`; Win32 assigns taskbar buttons to ownerless popup windows.
- **Fix**: Each `OverlayWindow` now creates a hidden 0×0 `WS_EX_TOOLWINDOW` owner HWND and passes it as `hWndParent`. Owned windows don't receive taskbar buttons. OBS visibility unaffected (WGC enumerates owned HWNDs normally).
- **Fixed in:** Phase 6 commit (2026-04-05)

---

**POST-602** `[x]` *(spec change)*
- **Title**: Settings window taskbar entry and icon
- **Change**: `ShowInTaskbar` changed from `False` to `True`; icon loaded from `Resources/simoverlay.ico` at startup. X button already hid to tray (`Window_Closing` cancels + `Hide()`) — no logic change needed.
- **Fixed in:** Phase 6 commit (2026-04-05)

---

**TASK-605** `[x]`
- **Title**: Performance benchmark suite
- **Description**: Automated BenchmarkDotNet project at `tests/SimOverlay.Benchmarks/`. Uses synthetic/mock data — no iRacing required. Benchmarks three hot paths: `IRacingRelativeCalculator.Compute()` (10 Hz data path, 40-car worst case), `SimDataBus.Publish<T>()` (called every tick per overlay), `OverlayConfig.Resolve()` (called every frame per overlay). Each benchmark reports mean execution time and heap bytes allocated per call. Run with: `dotnet run -c Release --project tests/SimOverlay.Benchmarks`. Results export to `BenchmarkDotNet.Artifacts/results/`. For regression tracking, commit a baseline JSON from the reference machine and compare future runs against it. Optimize if targets are missed: pre-allocate `IDWriteTextLayout` per row, use value types for snapshot structs, avoid LINQ in hot paths, replace Dictionary in calculator with array lookup indexed by CarIdx.
- **Acceptance Criteria**:
  - `RelativeCalculator.Compute40Cars`: mean < 50 µs on a modern 6-core.
  - `SimDataBus.Publish` (1 subscriber): mean < 1 µs, 0 B allocated.
  - `ConfigResolve.ResolveNoOverride`: 0 B allocated (returns `this`).
  - `ConfigResolve.ResolveWithOverride`: < 500 B allocated per call (one new `OverlayConfig`).
  - Benchmark project builds and runs in Release without errors.
- **Dependencies**: TASK-401, TASK-403, TASK-404.
- **Note (2026-04-06)**: Benchmark project created and first run completed on AMD Ryzen 5 7535HS / .NET 8.0.25. SimDataBus results: Publish1Subscriber = 9.3 ns / 0 B alloc; Publish3Subscribers = 13.6 ns / 0 B alloc; PublishNoSubscribers = 5.1 ns / 0 B alloc — all zero allocation, all well under the 1 µs target. Full results (RelativeCalculator + ConfigResolve) in `BenchmarkDotNet.Artifacts/results/`. Manual profiling criteria (CPU < 3%, GPU < 50 MB, frame time variance < 2 ms p99) remain valid real-session targets but require a live sim — out of scope for this automated suite.

---

**TASK-606** `[x]`
- **Title**: Installer / distribution package
- **Description**: Create a self-contained publish profile (`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`). Optionally create a NSIS/WiX installer placing the exe in `%ProgramFiles%\SimOverlay\`, with a Start Menu shortcut and "Start with Windows" option. Include `README.md` and `CHANGELOG.md`.
- **Acceptance Criteria**: Published `.exe` runs on a machine without .NET 8 runtime. Installer creates correct Start Menu entry and uninstalls cleanly.
- **Dependencies**: TASK-602, TASK-603.
- **Note (2026-04-05)**: Self-contained single-file publish profile created at `src/SimOverlay.App/Properties/PublishProfiles/win-x64.pubxml`. NSIS/WiX installer deferred — out of scope for MVP. Publish command: `dotnet publish src/SimOverlay.App -p:PublishProfile=win-x64`.
