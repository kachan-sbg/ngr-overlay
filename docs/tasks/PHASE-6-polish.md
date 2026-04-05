# Phase 6 — Polish and Integration `[ ]`

> [← Index](INDEX.md)

---

**TASK-601** `[x]`
- **Title**: `SimDetector` — automatic sim detection loop
- **Description**: Implement `SimDetector` in `SimOverlay.App`. Run a `System.Threading.Timer` every 2000 ms. On each tick: if no provider active, iterate registered providers in priority order, call `IsRunning()`. If true, call `Start()` and mark active. If active provider's `IsRunning()` returns false, call `Stop()`, clear active, resume polling. Fire `ActiveProviderChanged` event.
- **Acceptance Criteria**: iRacing start detected within ~2 seconds. iRacing close detected within ~2 seconds. Priority order respected. Unit-testable with simulated `IsRunning()`.
- **Dependencies**: TASK-201, TASK-304.

---

**TASK-602** `[ ]`
- **Title**: Single-instance enforcement
- **Description**: In `Program.cs`, acquire named `Mutex` (`Global\SimOverlay_SingleInstance`) before showing UI. If already held: find existing instance's tray window, post message to bring Settings to foreground, exit. Use `CreateMutex` via P/Invoke.
- **Acceptance Criteria**: Second instance does not create a second set of overlays. Existing instance's Settings window comes to foreground.
- **Dependencies**: TASK-504.

---

**TASK-603** `[ ]`
- **Title**: Application icon and resources
- **Description**: Create `.ico` file (16×16, 32×32, 48×48, 256×256). Set as assembly icon in `SimOverlay.App.csproj`. Use for `NotifyIcon`. Add `Resources/` folder in `App`.
- **Acceptance Criteria**: App has a recognizable icon in Explorer, taskbar, and tray.
- **Dependencies**: TASK-504.

---

**TASK-604** `[ ]`
- **Title**: Error logging
- **Description**: Add `Microsoft.Extensions.Logging` with a file sink writing to `%APPDATA%\SimOverlay\sim-overlay.log`. Log: sim connect/disconnect, device lost/recovery, config load/save, unhandled exceptions. Cap at 10 MB with single rotation (`.log.bak`). Set `WPF Application.DispatcherUnhandledException` and `AppDomain.CurrentDomain.UnhandledException` to log and display an error dialog before exiting.
- **Acceptance Criteria**: Log file contains connection and rendering events after a session. Unhandled exception produces log entry + dialog, not a silent crash.
- **Dependencies**: TASK-001.

---

**TASK-605** `[ ]`
- **Title**: Performance profiling and optimization pass
- **Description**: Profile under: 3 overlays active, iRacing with 40 AI cars, all overlays visible. Targets: total CPU < 3% on a modern 6-core; GPU memory < 50 MB; zero per-frame heap allocations in render loop. Optimize if needed: pre-allocate `IDWriteTextLayout` per row, use value types for snapshot structs, avoid LINQ in hot paths.
- **Acceptance Criteria**: Meets CPU/memory targets during a 10-minute session. No GC pauses causing render stuttering (frame time variance < 2 ms p99).
- **Dependencies**: TASK-401, TASK-403, TASK-404.

---

**TASK-606** `[ ]`
- **Title**: Installer / distribution package
- **Description**: Create a self-contained publish profile (`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`). Optionally create a NSIS/WiX installer placing the exe in `%ProgramFiles%\SimOverlay\`, with a Start Menu shortcut and "Start with Windows" option. Include `README.md` and `CHANGELOG.md`.
- **Acceptance Criteria**: Published `.exe` runs on a machine without .NET 8 runtime. Installer creates correct Start Menu entry and uninstalls cleanly.
- **Dependencies**: TASK-602, TASK-603.
