# VERIFICATION.md

## Task Completion Checkpoints

Quick verification steps to confirm each task is done correctly before moving on.
Run these after completing (and committing) each task.

---

## Phase 0 вЂ” Scaffolding

### TASK-001 вЂ” Solution and project structure

```
dotnet build NrgOverlay.sln
```
- [ ] Build exits 0 with no errors
- [ ] No circular dependency errors
- [ ] Solution opens in Visual Studio 2022 / VS Code without missing projects

### TASK-002 вЂ” NuGet packages

```
dotnet restore NrgOverlay.sln --force
dotnet build NrgOverlay.sln
```
- [ ] Restore exits 0 with no warnings (especially NU1603 version approximation warnings)
- [ ] Build exits 0

### TASK-003 вЂ” Global build properties and CI

```
dotnet build NrgOverlay.sln -warnaserror
```
- [ ] Build exits 0 with no warnings promoted to errors in src projects
- [ ] Test projects are NOT subject to `-warnaserror` (they build even if they have warnings)
- [ ] `.editorconfig` is recognized in VS Code (check bottom status bar for indentation settings on a `.cs` file)
- [ ] `.github/workflows/build.yml` is present and valid YAML

### TASK-004 вЂ” Docs directory

- [ ] `docs/README.md`, `docs/ARCHITECTURE.md`, `docs/OVERLAYS.md`, `docs/TASKS.md` all exist

---

## Phase 1 вЂ” Core Rendering Infrastructure

### TASK-101 вЂ” SimDataBus

```
dotnet test tests/NrgOverlay.Core.Tests/NrgOverlay.Core.Tests.csproj
```
- [ ] All 5 unit tests pass:
  1. Subscribe and receive a published message
  2. Multiple subscribers all receive the message
  3. Unsubscribe stops delivery
  4. Publish from background thread is received
  5. Concurrent subscribe/unsubscribe during publish does not throw

### TASK-102 вЂ” ConfigStore and config types

```
dotnet test tests/NrgOverlay.Core.Tests/NrgOverlay.Core.Tests.csproj
```
- [ ] All 7 unit tests pass:
  1. Round-trip serialize/deserialize preserves all fields including null overrides
  2. `Resolve(false)` always returns base values
  3. `Resolve(true)` with fully-null override returns base values
  4. `Resolve(true)` with partial override returns a mix
  5. X/Y are never taken from the override
  6. Missing config file returns defaults
  7. Corrupt JSON returns defaults
- [ ] Integration test: full config round-trip to a temp directory succeeds

### TASK-103 вЂ” Sim.Contracts DTOs and ISimProvider

```
dotnet build NrgOverlay.sln
```
- [ ] All DTO types compile with no errors
- [ ] `LicenseClass.GetColor()` returns correct RGBA values (spot-check in unit test or REPL)
- [ ] A trivial `ISimProvider` stub in a test compiles and satisfies the interface

### TASK-104 вЂ” Win32 transparent overlay window

Run the app manually:
```
dotnet run --project src/NrgOverlay.App
```
- [ ] A borderless, always-on-top window appears at the configured position
- [ ] Clicking through the window hits the application underneath (click-through works)
- [ ] Window title is set (e.g., "NrgOverlay вЂ” Relative") вЂ” verify with Spy++ or Task Manager
- [ ] Window is visible in OBS "Window Capture" source picker
- [ ] In OBS with WGC + "Allow Transparency": captured output has correct alpha channel

### TASK-105 вЂ” DXGI swap chain and Direct2D setup

Run the app manually:
- [ ] Window renders without visual corruption or flickering
- [ ] Window background is genuinely transparent (desktop content shows through)
- [ ] No DXGI or D3D errors in the log
- [ ] No crash when running alongside a full-screen borderless window

### TASK-106 вЂ” BaseOverlay render loop

```
dotnet test tests/NrgOverlay.Overlays.Tests/NrgOverlay.Overlays.Tests.csproj
```
- [ ] `TestOverlay` (red rectangle) renders at ~60 fps вЂ” verify with frame counter or stopwatch
- [ ] Disposing the overlay stops the render thread and releases all D2D resources (no memory leak)
- [ ] Changing `Config.FontSize` causes `RenderResources` to recreate correctly on next frame

### TASK-107 вЂ” Lock/unlock edit mode

Run the app manually:
- [ ] Unlocked: overlay can be dragged to a new position
- [ ] Unlocked: 2px highlight border is visible around the overlay
- [ ] Unlocked: overlay can be resized by dragging the bottom-right corner
- [ ] Locked: mouse clicks pass through (click-through restored)
- [ ] Locked: border disappears
- [ ] Position/size changes survive a toggle back to locked mode

### TASK-108 вЂ” Device lost recovery

Manual test (requires GPU Device Manager access):
- [ ] Disabling and re-enabling the GPU in Device Manager causes a recovery within ~2 seconds
- [ ] All overlay windows resume rendering after recovery with no crash or hang
- [ ] Recovery event is recorded in the log

---

## Phase 2 вЂ” iRacing Data Provider

### TASK-201 вЂ” IRacingProvider connection lifecycle

With iRacing **not** running:
- [ ] `IsRunning()` returns `false`

With iRacing running (at main menu):
- [ ] `IsRunning()` returns `true`
- [ ] `Start()`/`Stop()` can be called multiple times without errors or leaks
- [ ] `StateChanged` fires with the correct `SimState` value

### TASK-202 вЂ” IRacingPoller 60 Hz loop

With iRacing in a session:
- [ ] `DriverData` is published at 58вЂ“62 Hz (measure over 5 seconds)
- [ ] `RelativeData` is published at 9вЂ“11 Hz
- [ ] No memory growth after 10 minutes
- [ ] Polling thread CPU usage stays below 2% on a modern machine

### TASK-203 вЂ” Session YAML decoder

With a live iRacing session:
- [ ] `SessionData.TrackName` matches the actual track
- [ ] `SessionData.SessionType` matches (Practice / Qualify / Race)
- [ ] Track/air temps are non-zero and plausible
- [ ] YAML parse completes in < 5 ms (check log or stopwatch)
- [ ] `SessionData` republishes on session change (practice в†’ qualify transition)

### TASK-204 вЂ” Relative gap calculator

```
dotnet test tests/NrgOverlay.Sim.iRacing.Tests/NrgOverlay.Sim.iRacing.Tests.csproj
```
- [ ] Gap is correct for a car directly ahead
- [ ] Gap is correct for a car directly behind
- [ ] Wrap-around at S/F line produces a near-zero gap (not В±1 lap)
- [ ] Player entry is always present and marked `IsPlayer = true`
- [ ] Output list is sorted by gap (most ahead at index 0)

### TASK-205 вЂ” iRacing integration test

With iRacing running in a session:
```
dotnet test tests/NrgOverlay.Sim.iRacing.Tests/ --filter Category=Integration
```
- [ ] At least 280 `DriverData` messages in 5 seconds (в‰Ґ56 Hz)
- [ ] At least 45 `RelativeData` messages in 5 seconds (в‰Ґ9 Hz)
- [ ] `SessionData` received at least once with a non-empty `TrackName`

Without iRacing running:
- [ ] Integration test is **skipped**, not failed

---

## Phase 3 вЂ” Overlay Framework

### TASK-301 вЂ” Overlay manager

Run the app:
- [ ] Overlays marked `Enabled = true` in config are visible at their saved positions on startup
- [ ] Overlays marked `Enabled = false` are hidden
- [ ] `EnableOverlay` / `DisableOverlay` show/hide the correct window
- [ ] Config is persisted: restart the app and positions are still correct

### TASK-302 вЂ” Position/size persistence

Drag and resize overlays in screen mode and stream mode:
- [ ] Screen mode: position and size saved to base config
- [ ] Stream mode (override enabled): position saved to base, size saved to stream override
- [ ] Switching modes restores each profile's own dimensions
- [ ] Rapid dragging does not cause excessive file I/O (check Task Manager or ProcMon)

### TASK-303 вЂ” Config live-update

In the Settings UI, change font size and background color:
- [ ] Changes take effect within one render frame (~16 ms) without restarting the app
- [ ] No crash when changing values rapidly (e.g., dragging a slider quickly)
- [ ] No D2D resource leaks after repeated config changes

### TASK-304 вЂ” Sim state display

- [ ] App started without iRacing running в†’ overlays show "Sim not detected"
- [ ] Start iRacing to the main menu в†’ overlays show "Waiting for sessionвЂ¦"
- [ ] Enter a session в†’ overlays show live data
- [ ] Close iRacing в†’ overlays revert to "Sim not detected"

---

## Phase 4 вЂ” MVP Overlays

### TASK-401 вЂ” Relative overlay

With a live iRacing session (в‰Ґ3 cars):
- [ ] All 7 columns render: POS, CAR, NAME, iRTG, LIC, GAP, LAP
- [ ] Player row is visually distinct (highlighted)
- [ ] License class cell shows correct color (A = blue, B = green, C = yellow, D = orange, R = red)
- [ ] Text is not clipped
- [ ] List updates visibly as cars pass the player
- [ ] Works correctly with 1, 5, and 20+ cars on track

### TASK-402 вЂ” Relative column visibility

In settings, toggle `showIRating` and `showLicense`:
- [ ] With `showIRating = false`: iRTG column is absent, other columns expand
- [ ] With `showLicense = false`: LIC column is absent
- [ ] Both can be hidden simultaneously
- [ ] Changes apply immediately without restart

### TASK-403 вЂ” Session Info overlay

With a live iRacing session:
- [ ] Track name, session type, time remaining are correct
- [ ] Track/air temps correct and unit-configurable (В°C / В°F)
- [ ] Current lap, best lap, last lap times correct and formatted `M:SS.mmm`
- [ ] Delta value changes color (green = faster, red = slower) when sign changes
- [ ] Wall clock updates each second
- [ ] Waiting state shows placeholder dashes for all data fields

### TASK-404 вЂ” Delta Bar overlay

While driving a lap in iRacing:
- [ ] Bar extends left (green) when ahead of best lap, right (red) when slower
- [ ] Center line always visible
- [ ] Bar is clamped at the edge for large deltas вЂ” does not overflow the window
- [ ] Trend arrow shows correct direction
- [ ] Smooth visual update at 60 Hz (no stuttering)

### TASK-405 вЂ” Overlay smoke test

Follow the manual test protocol in `docs/testing/manual-overlay-test.md`:
- [ ] All three overlays render correctly in a live practice session
- [ ] Player is highlighted in Relative, data matches iRacing UI
- [ ] Session Info shows correct track name, temps, lap times
- [ ] Delta Bar responds to fast/slow sectors
- [ ] Last Lap and Best Lap update after completing a timed lap
- [ ] Drag/resize works in edit mode
- [ ] App restart preserves all positions and settings

---

## Phase 5 вЂ” Configuration UI

### TASK-501 вЂ” Settings window scaffold

- [ ] Window opens from tray icon в†’ "Settings"
- [ ] "Overlays" and "Global Settings" sections present
- [ ] Window does not appear in the taskbar
- [ ] Reopening the window after closing shows previously set values

### TASK-502 вЂ” Per-overlay settings panel

- [ ] Screen tab edits base config correctly (color, opacity, size, font, toggles)
- [ ] Stream Override tab has "Enable override" toggle
- [ ] Unchecked "Custom" fields show inherited base value as greyed placeholder
- [ ] Applying changes immediately updates a live overlay in the matching mode
- [ ] Min/max constraints enforced on all numeric inputs

### TASK-503 вЂ” Global settings panel

- [ ] "Start with Windows" adds/removes the registry key correctly
- [ ] Stream Mode toggle switches all overlays with an active override
- [ ] Stream Mode state persists across restarts

### TASK-504 вЂ” Tray icon and menu

- [ ] Tray icon appears in the notification area
- [ ] All context menu items work: Settings, Edit Mode, Stream Mode, Exit
- [ ] "Stream Mode" label reflects current state
- [ ] Tooltip updates within ~2 seconds when sim connection state changes
- [ ] Double-click opens Settings

---

## Phase 6 вЂ” Polish and Integration

### TASK-601 вЂ” SimDetector

- [ ] Starting iRacing causes auto-connect within ~2 seconds
- [ ] Closing iRacing causes auto-disconnect within ~2 seconds

### TASK-602 вЂ” Single-instance enforcement

- [ ] Launching a second instance does not create a second set of overlays
- [ ] Launching a second instance brings the existing Settings window to the foreground

### TASK-603 вЂ” Application icon

- [ ] Icon appears in Explorer when browsing to the `.exe`
- [ ] Icon appears in the tray notification area

### TASK-604 вЂ” Error logging

- [ ] Log file created at `%APPDATA%\NrgOverlay\sim-overlay.log`
- [ ] Connect/disconnect events appear in the log
- [ ] Deliberately throwing an unhandled exception produces a log entry and an error dialog

### TASK-605 вЂ” Performance

Profile with 3 overlays + 40 AI cars for 10 minutes:
- [ ] Total CPU < 3% on a modern 6-core machine
- [ ] D2D GPU memory < 50 MB
- [ ] No per-frame heap allocations in the render loop (verify with PerfView)
- [ ] Frame time variance < 2 ms p99

### TASK-606 вЂ” Installer

```
dotnet publish src/NrgOverlay.App -r win-x64 --self-contained -p:PublishSingleFile=true
```
- [ ] Single `.exe` produced
- [ ] `.exe` runs on a machine without .NET 8 runtime installed
- [ ] Installer creates Start Menu entry and can be uninstalled via Add/Remove Programs

