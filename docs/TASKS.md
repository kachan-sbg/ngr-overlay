# TASKS.md

## Racing Simulator Overlay — Implementation Task Breakdown

> For project overview and document index see [README.md](README.md).
> For *why* decisions were made see [DECISIONS.md](DECISIONS.md).

### Status Legend
- `[ ]` Not started
- `[~]` In progress
- `[x]` Done

### Phase Status

| Phase | Status | Tasks |
|---|---|---|
| 0 — Scaffolding | `[x]` | TASK-001 to TASK-004 |
| 1 — Rendering core | `[~]` | TASK-101 to TASK-108 |
| 2 — iRacing data | `[ ]` | TASK-201 to TASK-205 |
| 3 — Overlay framework | `[ ]` | TASK-301 to TASK-304 |
| 4 — MVP overlays | `[ ]` | TASK-401 to TASK-405 |
| 5 — Settings UI | `[ ]` | TASK-501 to TASK-504 |
| 6 — Polish | `[ ]` | TASK-601 to TASK-606 |

### Phase 0: Project Scaffolding

---

**TASK-001** `[x]`
- **Title**: Create solution and project structure
- **Phase**: 0
- **Description**: Create the `.sln` file and all six `.csproj` files (`Core`, `Rendering`, `Sim.Contracts`, `Sim.iRacing`, `Overlays`, `App`). Set target framework to `net8.0-windows`. Add project references matching the dependency graph in ARCHITECTURE.md. Configure `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` globally in `Directory.Build.props`. Set `<PlatformTarget>x64</PlatformTarget>` globally (required for DirectX P/Invoke).
- **Acceptance Criteria**: `dotnet build SimOverlay.sln` completes with no errors. All project references are correct and no circular dependencies exist. The solution loads in Visual Studio 2022 without errors.
- **Dependencies**: None.

---

**TASK-002** `[x]`
- **Title**: Add NuGet dependencies
- **Phase**: 0
- **Description**: Add NuGet packages to appropriate projects. `SimOverlay.Rendering`: `Vortice.Direct2D1`, `Vortice.DirectComposition`, `Vortice.DXGI`, `Vortice.Direct3D11`. `SimOverlay.Sim.iRacing`: `IRSDKSharper`. `SimOverlay.App`: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`. `SimOverlay.Core`: `System.Text.Json` (already in .NET 8 BCL, no explicit package needed). Create `Directory.Packages.props` for central package version management.
- **Acceptance Criteria**: All packages restore successfully. No version conflicts. `dotnet restore` exits 0.
- **Dependencies**: TASK-001.

---

**TASK-003** `[x]`
- **Title**: Configure global build properties and CI
- **Phase**: 0
- **Description**: Create `Directory.Build.props` with shared properties: nullable, implicit usings, platform target, treat-warnings-as-errors for non-test projects. Create a `.editorconfig` with C# style rules consistent with project conventions. Add a basic `build.yml` GitHub Actions workflow (if using GitHub) or equivalent that builds the solution.
- **Acceptance Criteria**: `dotnet build` with `-warnaserror` completes cleanly on a fresh clone. `.editorconfig` is recognized by the IDE.
- **Dependencies**: TASK-001.

---

**TASK-004** `[x]`
- **Title**: Create `docs/` directory with architecture documents
- **Phase**: 0
- **Description**: Add `PROJECT.md`, `ARCHITECTURE.md`, `OVERLAYS.md`, and `TASKS.md` to the `docs/` folder in the repository root. Commit them as the authoritative design reference.
- **Acceptance Criteria**: All four documents are present and committed.
- **Dependencies**: TASK-001.

---

### Phase 1: Core Rendering Infrastructure

---

**TASK-101** `[x]`
- **Title**: Implement `SimDataBus`
- **Phase**: 1
- **Description**: In `SimOverlay.Core`, implement `ISimDataBus` and `SimDataBus`. The bus maintains a `Dictionary<Type, List<Delegate>>` of subscriber lists. `Publish<T>(T data)` iterates the subscriber list for `typeof(T)` and invokes each delegate. `Subscribe<T>(Action<T> handler)` adds the handler. `Unsubscribe<T>(Action<T> handler)` removes it. Use `ReaderWriterLockSlim` to protect the dictionary for concurrent subscribe/unsubscribe while allowing lock-free reads during publish (or use `ImmutableList` swap pattern for zero-contention publish path). Thread-safe; publish can be called from any thread.
- **Acceptance Criteria**: Unit tests: (1) subscribe and receive a published message, (2) multiple subscribers all receive the message, (3) unsubscribe stops receiving, (4) publish from background thread is received, (5) subscribing/unsubscribing during publish does not throw.
- **Dependencies**: TASK-001.

---

**TASK-102** `[x]`
- **Title**: Implement `ConfigStore` and `AppConfig`/`OverlayConfig`/`StreamOverrideConfig` types
- **Phase**: 1
- **Description**: In `SimOverlay.Core`, define `AppConfig`, `GlobalSettings`, `ColorConfig`, `OverlayConfig`, and `StreamOverrideConfig` POCOs. `OverlayConfig` contains all base visual properties plus `StreamOverrideConfig? StreamOverride`. `StreamOverrideConfig` mirrors all overridable fields from `OverlayConfig` as nullable (`int?`, `float?`, `ColorConfig?`) — X/Y position fields are NOT included (position is never overridable). Implement `OverlayConfig.Resolve(bool streamModeActive)` which returns the effective config: if `streamModeActive && StreamOverride?.Enabled == true`, return a new `OverlayConfig` where each field is `streamOverride.Field ?? this.Field` (X/Y always from `this`). `GlobalSettings` includes `StreamModeActive` (persisted). Implement `ConfigStore` with atomic save (`config.json.tmp` → `File.Move` with overwrite). Define defaults: stream override disabled, no override values set.
- **Acceptance Criteria**: Unit tests: (1) round-trip serialize/deserialize preserves all fields including null override fields, (2) `Resolve(false)` always returns base config values, (3) `Resolve(true)` with a fully-null override returns base config values, (4) `Resolve(true)` with some override fields set returns a mix of override and base values, (5) X/Y are never taken from the override, (6) missing config file returns defaults, (7) corrupt JSON returns defaults. Integration test: full config round-trip to temp directory.
- **Dependencies**: TASK-001.

---

**TASK-103** `[x]`
- **Title**: Implement `Sim.Contracts` DTOs and `ISimProvider`
- **Phase**: 1
- **Description**: In `SimOverlay.Sim.Contracts`, define all normalized data types: `SessionData`, `DriverData`, `RelativeData`, `RelativeEntry`, `SimState` (enum), `SessionType` (enum), `LicenseClass` (enum with static color-mapping helper method returning RGBA floats). Define `ISimProvider` interface with `SimId`, `IsRunning()`, `Start()`, `Stop()`, and `StateChanged` event.
- **Acceptance Criteria**: All types compile. `LicenseClass` color helper returns correct RGBA for each value. Interface is implemented correctly by a trivial stub in a test project.
- **Dependencies**: TASK-001.

---

**TASK-104** `[x]`
- **Title**: Win32 overlay window — basic transparent window creation
- **Phase**: 1
- **Description**: In `SimOverlay.Rendering`, create `OverlayWindow`. Use `P/Invoke` (or Vortice's Win32 helpers) to register a `WNDCLASSEX`, create a window with `WS_POPUP | WS_VISIBLE` and extended styles `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOPMOST`. **Do not use `WS_EX_TOOLWINDOW`** — it hides the window from OBS's window picker. Set each window's title to its display name (e.g., `"SimOverlay — Relative"`). Set position and size from `OverlayConfig`. Show a fully transparent black window. Verify it is click-through.
- **Acceptance Criteria**: A transparent window appears at the configured position. The window is always on top. Mouse clicks pass through. No title bar or border. Window title is set and stable. Window appears in OBS "Window Capture" picker. With OBS WGC + "Allow Transparency", the overlay is captured with correct alpha (semi-transparent background, opaque text).
- **Dependencies**: TASK-001, TASK-102.

---

**TASK-105** `[x]`
- **Title**: DXGI swap chain and Direct2D device context setup
- **Phase**: 1
- **Description**: Extend `OverlayWindow` to create: (1) a `D3D11` device with `D3D11_CREATE_DEVICE_BGRA_SUPPORT`. (2) A `IDXGISwapChain1` via `IDXGIFactory2.CreateSwapChainForHwnd` with `DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL`, `DXGI_ALPHA_MODE_PREMULTIPLIED`, `DXGI_FORMAT_B8G8R8A8_UNORM`, buffer count 2. (3) A `ID2D1Device` from the DXGI device, and a `ID2D1DeviceContext` from the D2D device. (4) A `IDCompositionDevice`, `IDCompositionTarget` for the HWND, and a `IDCompositionVisual` bound to the swap chain. Call `IDCompositionDevice.Commit()`. Add a `Render()` method that clears to transparent and calls `swapChain.Present(1, 0)`.
- **Acceptance Criteria**: The window renders without visual artifacts. No DXGI errors. The window background is genuinely transparent (content behind it shows through). Can run alongside a full-screen borderless window without issues.
- **Dependencies**: TASK-104, TASK-002.

---

**TASK-106** `[x]`
- **Title**: Base overlay class — render loop and data snapshot pattern
- **Phase**: 1
- **Description**: In `SimOverlay.Rendering`, create `BaseOverlay : OverlayWindow`. Add: (1) `OverlayConfig Config` property. (2) Abstract `OnRender(ID2D1DeviceContext context, OverlayConfig config)`. (3) `Subscribe<T>` helper that delegates to `ISimDataBus` and stores the subscription token for cleanup on `Dispose()`. (4) A render loop on a dedicated `Thread` (`IsBackground = true`) running at 60 fps using `Stopwatch`-based timing. The render loop calls `Render()`. (5) `RenderResources` inner class for caching `ID2D1SolidColorBrush` and `IDWriteTextFormat` objects, invalidated when `OverlayConfig` changes.
- **Acceptance Criteria**: A `TestOverlay : BaseOverlay` subclass that draws a red rectangle renders at ~60 fps without memory leaks. Disposing the overlay stops the render loop and releases all D2D resources. `RenderResources` is recreated correctly when `Config.FontSize` changes.
- **Dependencies**: TASK-105, TASK-101, TASK-102.

---

**TASK-107**
- **Title**: Lock/unlock (edit mode) — drag and resize
- **Phase**: 1
- **Description**: Implement edit mode in `OverlayWindow`. When `IsLocked = false`: (1) Remove `WS_EX_TRANSPARENT` via `SetWindowLongPtr`. (2) Handle `WM_NCHITTEST`: return `HTCAPTION` for most of the client area (allows drag), `HTBOTTOMRIGHT` for a 16×16 px corner hit zone (allows resize). (3) Draw a 2px highlight border around the overlay interior (rendered in `OnRender` as a non-filled rectangle in accent color). When `IsLocked = true`: (1) Re-add `WS_EX_TRANSPARENT`. (2) Remove the border. Subscribe `OverlayWindow` to an `EditModeChangedEvent` on `ISimDataBus`.
- **Acceptance Criteria**: In unlocked mode, overlays can be dragged to new positions and resized by dragging the bottom-right corner. The border is visible. In locked mode, mouse clicks pass through. Position/size changes persist after re-lock.
- **Dependencies**: TASK-106, TASK-102.

---

**TASK-108**
- **Title**: Device lost recovery
- **Phase**: 1
- **Description**: Implement `DeviceLostRecovery` in `SimOverlay.Rendering`. Handle `DXGI_ERROR_DEVICE_REMOVED` and `DXGI_ERROR_DEVICE_RESET` returned from `Present()` or `EndDraw()`. Recovery procedure: (1) Pause all overlay render loops. (2) Release all D3D11, D2D, DXGI, and DirectComposition resources in correct order. (3) Recreate D3D device, swap chain, D2D device context, DComp objects. (4) Re-create `RenderResources` for each overlay. (5) Resume render loops. Log the recovery event.
- **Acceptance Criteria**: Artificially triggering a device reset (e.g., disabling and re-enabling the GPU in Device Manager, or using a debug layer trigger) causes the app to recover within ~2 seconds without a crash or hang. All overlay windows continue rendering after recovery.
- **Dependencies**: TASK-106.

---

### Phase 2: iRacing Data Provider

---

**TASK-201**
- **Title**: IRacingProvider — MMF connection lifecycle
- **Phase**: 2
- **Description**: In `SimOverlay.Sim.iRacing`, implement `IRacingProvider : ISimProvider`. `IsRunning()`: attempt to open the named memory-mapped file `Local\IRSDKMemMapFileName` with `MemoryMappedFile.OpenExisting()`; return true if it succeeds (close immediately). `Start()`: open the MMF, create a `MemoryMappedViewAccessor`, fire `StateChanged(Connected)`. `Stop()`: dispose the accessor and MMF, fire `StateChanged(Disconnected)`. If IRSDKSharper is used, delegate to its connection management. Handle `FileNotFoundException` in `IsRunning()` gracefully (return false).
- **Acceptance Criteria**: With iRacing not running: `IsRunning()` returns false. With iRacing running (at main menu or in-session): `IsRunning()` returns true. `Start()`/`Stop()` cycle can be called multiple times without leaks or errors. `StateChanged` fires correctly.
- **Dependencies**: TASK-103, TASK-002.

---

**TASK-202**
- **Title**: IRacingPoller — 60 Hz telemetry polling loop
- **Phase**: 2
- **Description**: Implement `IRacingPoller`. Runs on a dedicated background thread. Uses `Stopwatch` + `SpinWait` or `Thread.Sleep(1)` + correction loop for ~16.67 ms tick timing (target: within ±1 ms jitter on most hardware). Each tick: (1) Read the iRacing SDK header to check data validity (check `status` flag and `tickCount`). (2) If valid, read variable offsets for: `PlayerCarIdx`, `LapDistPct` (all cars), `CarIdxLapDistPct`, `CarIdxPosition`, `CarIdxLap`, `Lap`, `LapBestLapTime`, `LapLastLapTime`, `LapDeltaToBestLap`. (3) Publish `DriverData` to `ISimDataBus`. (4) Every 6th tick (10 Hz), compute `RelativeData` and publish.
- **Acceptance Criteria**: With iRacing running and a session active, `DriverData` is published at a measured rate of 58–62 Hz. `RelativeData` is published at 9–11 Hz. No memory leaks after running for 10 minutes. CPU usage on the polling thread stays below 2% on modern hardware.
- **Dependencies**: TASK-201, TASK-101.

---

**TASK-203**
- **Title**: IRacingSessionDecoder — YAML session string parser
- **Phase**: 2
- **Description**: Implement `IRacingSessionDecoder`. The iRacing SDK exposes a YAML-formatted session string at a specific memory offset, updated when `sessionInfoUpdate` counter changes. Parse the YAML into a `SessionData` DTO. Fields to extract: `WeekendInfo.TrackDisplayName`, `SessionInfo.Sessions[N].SessionType`, `SessionInfo.Sessions[N].SessionTime` or `SessionLaps`, `WeekendInfo.TrackSurfaceTemp`, `WeekendInfo.TrackAirTemp`. For driver list (used in Relative): `DriverInfo.Drivers[N].UserName`, `DriverInfo.Drivers[N].CarNumber`, `DriverInfo.Drivers[N].IRating`, `DriverInfo.Drivers[N].LicString`. Use a simple line-by-line YAML parser or `YamlDotNet` (NuGet). Publish `SessionData` when updated.
- **Acceptance Criteria**: With a live iRacing session, `SessionData` contains the correct track name, session type, temperatures. YAML parsing completes in under 5 ms. `SessionData` is re-published when the session changes (e.g., practice to qualify transition).
- **Dependencies**: TASK-201, TASK-101, TASK-103.

---

**TASK-204**
- **Title**: IRacingRelativeCalculator — gap computation
- **Phase**: 2
- **Description**: Implement `IRacingRelativeCalculator.Compute(rawTelemetry, driverList)` → `RelativeData`. Algorithm: (1) Get `playerLapDistPct` for the player car index. (2) For each other car: compute `delta = carLapDistPct - playerLapDistPct`. Normalize delta to `[-0.5, 0.5]` by wrapping: if `delta > 0.5` subtract 1.0, if `delta < -0.5` add 1.0. This gives track-position delta as a fraction of the lap. (3) Convert fractional delta to time using estimated lap time (best lap or a configurable fallback). `gapSeconds = delta * estimatedLapTime`. (4) Sort by `gapSeconds`. (5) Join with `DriverInfo` data to populate `DriverName`, `CarNumber`, `IRating`, `LicenseClass`. (6) Select the N drivers nearest to the player (configurable, default 15). Mark the player entry with `IsPlayer = true`.
- **Acceptance Criteria**: Unit tests with synthetic telemetry data: gap calculation is correct for cars ahead, behind, and on different laps. Wrap-around at the start/finish line produces correct near-zero gaps. The player's own entry is always included and marked correctly. Output list is always sorted by gap (most ahead at top).
- **Dependencies**: TASK-203.

---

**TASK-205**
- **Title**: Integration test — iRacing data provider end-to-end
- **Phase**: 2
- **Description**: Write an integration test (requires iRacing to be running in a session). The test: starts `IRacingProvider`, waits for `StateChanged(InSession)`, subscribes to `DriverData` and `RelativeData` on a test `SimDataBus`, runs for 5 seconds, asserts that at least 280 `DriverData` messages were received (≥56 Hz), and that at least 45 `RelativeData` messages were received (≥9 Hz). Also assert that `SessionData` was received at least once with a non-empty `TrackName`. This test is marked `[Category("Integration")]` and excluded from standard CI unless iRacing is running.
- **Acceptance Criteria**: Test passes when iRacing is running in a session. Test is skipped (not failed) when iRacing is not running.
- **Dependencies**: TASK-202, TASK-203, TASK-204.

---

### Phase 3: Overlay Framework

---

**TASK-301**
- **Title**: Overlay manager — create, show, hide overlays from config
- **Phase**: 3
- **Description**: In `SimOverlay.App`, implement `OverlayManager`. At startup: instantiate one instance of each overlay class (`RelativeOverlay`, `SessionInfoOverlay`, `DeltaBarOverlay`). For each overlay, read its `OverlayConfig` and set position/size. Show or hide based on `OverlayConfig.Enabled`. Provide `EnableOverlay(string overlayId)` and `DisableOverlay(string overlayId)` methods that show/hide the window and update `OverlayConfig.Enabled` in-memory. Call `ConfigStore.Save()` after changes.
- **Acceptance Criteria**: On startup, enabled overlays are visible at their saved positions. Disabled overlays are not visible. Calling `EnableOverlay` shows the window. Calling `DisableOverlay` hides it. Positions are correct after a restart.
- **Dependencies**: TASK-106, TASK-102.

---

**TASK-302**
- **Title**: Position/size persistence on drag/resize
- **Phase**: 3
- **Description**: In `OverlayWindow`, after `WM_MOVE` or `WM_SIZE`, persist changes with stream-mode awareness: **position** (X/Y) is always written to the base `OverlayConfig` regardless of stream mode (position is shared between profiles). **Size** (Width/Height) is written to `StreamOverride.Width`/`StreamOverride.Height` when stream mode is active and the override is enabled; otherwise written to the base `OverlayConfig.Width`/`OverlayConfig.Height`. This ensures resizing in stream mode does not disturb the driver's screen profile dimensions. Debounce all writes with a 500 ms `System.Threading.Timer`.
- **Acceptance Criteria**: Dragging in screen mode saves position to base config, size to base config. Dragging in stream mode (override enabled) saves position to base config, size to stream override. After switching modes, each profile restores its own dimensions. Rapidly dragging does not cause excessive file writes.
- **Dependencies**: TASK-107, TASK-102.

---

**TASK-303**
- **Title**: Config live-update — apply changes without restart
- **Phase**: 3
- **Description**: When `OverlayConfig` properties change (e.g., from the Settings UI), overlays must update their appearance without restarting. Implement `OverlayConfig.ConfigChanged` event. When the Settings UI saves a config change, it fires `ConfigChanged` on the relevant `OverlayConfig` instance. `BaseOverlay` listens to `ConfigChanged` and calls `RenderResources.Invalidate()` on its next render tick (not immediately, to avoid cross-thread Direct2D issues). `RenderResources.Invalidate()` releases cached brushes and text formats; they are lazily re-created on the next call to `GetBrush()` or `GetTextFormat()`.
- **Acceptance Criteria**: Changing font size in the Settings UI takes effect within one render frame (~16 ms) without restarting. Changing background color updates immediately. No crashes or resource leaks when config changes rapidly (e.g., dragging a color slider).
- **Dependencies**: TASK-106, TASK-302.

---

**TASK-304**
- **Title**: Sim state display in overlays — disconnected/waiting states
- **Phase**: 3
- **Description**: `BaseOverlay` subscribes to `SimState` changes via `ISimDataBus`. Define three visual states: `Disconnected` (sim not running), `WaitingForSession` (sim running, not in a session), `Active` (in session, data flowing). In `Disconnected` state, overlays render a dim message like "Sim not detected". In `WaitingForSession`, render "Waiting for session…". In `Active`, render live data. Transitions are handled in `OnRender()` by checking a `_simState` field updated by `OnDataUpdate()`.
- **Acceptance Criteria**: Starting the app without iRacing running shows "Sim not detected". Starting iRacing to the main menu shows "Waiting for session". Entering a session shows live data. Exiting iRacing reverts to "Sim not detected".
- **Dependencies**: TASK-106, TASK-201.

---

### Phase 4: MVP Overlays

---

**TASK-401**
- **Title**: Relative overlay — layout and rendering
- **Phase**: 4
- **Description**: Implement `RelativeOverlay : BaseOverlay` in `SimOverlay.Overlays`. Subscribe to `RelativeData`. On each render: (1) Draw the background rectangle (from `Config.BackgroundColor`). (2) Draw the column header row. (3) For each `RelativeEntry` in the snapshot (up to `Config.MaxDriversShown`): draw a row with columns as specified in OVERLAYS.md. (4) Highlight the player row with `Config.PlayerHighlightColor`. (5) Draw the license class color as a filled background behind the LIC cell, using `LicenseClass.GetColor()`. Use `IDWriteTextLayout` for text measurement to ensure correct column alignment regardless of font metrics. Columns are positioned by fixed pixel offsets calculated from `Config.Width`.
- **Acceptance Criteria**: All 7 columns render correctly. Player row is visually distinct. License color cells are correct for each class. Text is not clipped unexpectedly. Overlay updates visibly when cars pass the player. Correct behavior with 1, 5, and 20+ cars on track.
- **Dependencies**: TASK-106, TASK-204, TASK-303.

---

**TASK-402**
- **Title**: Relative overlay — column visibility configuration
- **Phase**: 4
- **Description**: Implement the `showIRating` and `showLicense` configuration flags in `RelativeOverlay`. When a column is hidden, recalculate the remaining column widths to fill the available space proportionally. Ensure the Driver Name column gets the extra space when optional columns are hidden.
- **Acceptance Criteria**: With `showIRating = false`, the iRTG column is absent and other columns expand to fill the space. With `showLicense = false`, the LIC column is absent. Both can be hidden simultaneously. Changes take effect immediately when applied from Settings.
- **Dependencies**: TASK-401, TASK-303.

---

**TASK-403**
- **Title**: Session Info overlay — layout and rendering
- **Phase**: 4
- **Description**: Implement `SessionInfoOverlay : BaseOverlay`. Subscribe to `SessionData` (1 Hz) and `DriverData` (60 Hz). Maintain two snapshots: `_sessionSnapshot` (updated by `SessionData`) and `_driverSnapshot` (updated by `DriverData`). In `OnRender`: draw background, then draw each labeled row as per OVERLAYS.md spec. Delta value row: use green/red color based on sign of `LapDeltaVsBestLap`. Format `TimeSpan` values as `M:SS.mmm`. Format `GameTimeOfDay` as `HH:mm`. Wall clock uses `DateTime.Now` read directly in `OnRender` (no subscription needed). Implement `temperatureUnit` conversion for display.
- **Acceptance Criteria**: All rows display correct data from a live iRacing session. Delta value changes color when sign changes. Wall clock updates each second. Track/air temp shows in both C and F based on config. "Waiting for session" state shows placeholder dashes for all data fields.
- **Dependencies**: TASK-106, TASK-202, TASK-203, TASK-303.

---

**TASK-404**
- **Title**: Delta Bar overlay — layout and rendering
- **Phase**: 4
- **Description**: Implement `DeltaBarOverlay : BaseOverlay`. Subscribe to `DriverData`. In `OnRender`: (1) Draw background. (2) Draw the centered delta value text in green or red. (3) Draw the bar background (slightly lighter than overlay background, full width). (4) Compute fill width: `fillFraction = Clamp(Abs(delta) / Config.DeltaBarMaxSeconds, 0, 1)`. `fillPixels = fillFraction * (contentWidth / 2)`. (5) Draw the fill rectangle: if delta < 0, from `(center - fillPixels)` to `center`; if delta > 0, from `center` to `(center + fillPixels)`. (6) Draw the center line (1–2 px vertical line at the horizontal midpoint). (7) Optionally draw trend arrow. Maintain a 500 ms rolling buffer of delta values to compute trend direction.
- **Acceptance Criteria**: Bar extends left (green) when faster than best lap, right (red) when slower. Center line is always visible. Bar is clamped at the edge for large deltas. Trend arrow shows correct direction. Delta text is readable at all window sizes. Smooth visual update at 60 Hz.
- **Dependencies**: TASK-106, TASK-202, TASK-303.

---

**TASK-405**
- **Title**: Overlay smoke testing against live iRacing session
- **Phase**: 4
- **Description**: Manual test protocol (documented in a `docs/testing/manual-overlay-test.md`): (1) Launch app with all three overlays enabled. (2) Start iRacing, enter a practice session with at least 3 AI cars. (3) Verify Relative overlay shows correct driver list, player highlighted. (4) Verify Session Info shows correct track name, session time, lap counter, temperatures. (5) Drive a lap; verify Delta Bar shows a value, turns green on a fast sector, red on a slow sector. (6) Complete a timed lap; verify Last Lap and Best Lap update. (7) Verify drag/resize works in edit mode. (8) Restart app; verify positions and settings persist.
- **Acceptance Criteria**: All checklist items pass without crashes, visual glitches, or incorrect data.
- **Dependencies**: TASK-401, TASK-403, TASK-404.

---

### Phase 5: Configuration UI

---

**TASK-501**
- **Title**: Settings window scaffold (WPF)
- **Phase**: 5
- **Description**: In `SimOverlay.App`, create a WPF `SettingsWindow`. The window contains: (1) A tab control or sidebar navigation with sections: "Overlays", "Global Settings". (2) "Overlays" section: a list of overlay names with enable/disable checkboxes. Selecting an overlay shows its properties panel on the right. (3) An "Apply" button and a "Close" button. The window is created lazily (only when the user clicks "Settings" in the tray menu). It is not shown in the taskbar (`ShowInTaskbar = false`).
- **Acceptance Criteria**: Window opens from tray icon. Contains correct sections. Closing and reopening shows previously set values. Window does not appear in the taskbar.
- **Dependencies**: TASK-301, TASK-102.

---

**TASK-502**
- **Title**: Per-overlay settings panel — color pickers, numeric inputs, stream override
- **Phase**: 5
- **Description**: Implement the per-overlay properties panel in `SettingsWindow` with two tabs: **"Screen"** and **"Stream Override"**. Screen tab controls: (1) Background color RGBA picker. (2) Text color RGBA picker. (3) Player highlight color (Relative only). (4) Opacity slider (0–100%). (5) Width/Height numeric inputs (min/max enforced). (6) Font size (10–48). (7) Overlay-specific toggles (`showIRating`, `showLicense`, `showTrendArrow`, etc.). Stream Override tab: an "Enable override" toggle at the top (dims all controls when off). Each field has a "Custom" checkbox next to it — unchecked means inherit from Screen config (null in JSON), checked means use the adjacent input value. Fields present: same set as Screen tab except position (X/Y not overridable). Bind to `OverlayConfigViewModel` with a matching `StreamOverrideViewModel`. On "Apply", push changes to the live `OverlayConfig.StreamOverride` and call `ConfigStore.Save()`. The live overlay updates immediately if stream mode is currently active.
- **Acceptance Criteria**: Screen tab shows and edits base config correctly. Stream Override tab shows "Enable override" toggle. With override enabled, "Custom" checkboxes control which fields are active. Unchecked fields show the inherited base value as greyed-out placeholder text. Applying override changes immediately updates a live overlay that is currently in stream mode. Applying Screen changes immediately updates a live overlay in screen mode. Min/max constraints enforced on both tabs. Switching between Screen and Stream Override tabs does not lose unsaved changes.
- **Dependencies**: TASK-501, TASK-303.

---

**TASK-503**
- **Title**: Global settings panel — sim priority, startup behavior, stream mode
- **Phase**: 5
- **Description**: Implement the "Global Settings" panel. Controls: (1) "Start with Windows" checkbox — adds/removes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. (2) Sim priority order list (future use): a sortable `ListBox` showing registered sim names. (3) "Edit Mode" toggle button (mirrors tray menu). (4) "Stream Mode" toggle button — activates/deactivates stream overrides globally, saves `globalSettings.streamModeActive` to config, broadcasts `StreamModeChangedEvent` on `ISimDataBus`. Button label reflects current state ("Stream Mode: Screen" / "Stream Mode: Stream").
- **Acceptance Criteria**: "Start with Windows" correctly adds/removes the registry entry. Stream Mode toggle correctly switches all overlays with an enabled override to their stream profile. Toggling stream mode from this panel is identical in effect to toggling from the tray menu. State persists across restarts.
- **Dependencies**: TASK-501, TASK-107.

---

**TASK-504**
- **Title**: Tray icon and context menu
- **Phase**: 5
- **Description**: In `SimOverlay.App`, implement `TrayIconController` using `System.Windows.Forms.NotifyIcon`. Context menu items: "Settings" (opens `SettingsWindow`), "Edit Mode: Off / Edit Mode: On" (toggles, label reflects state), "Stream Mode: Screen / Stream Mode: Stream" (toggles `globalSettings.streamModeActive`, saves config, broadcasts `StreamModeChangedEvent`, label reflects state), separator, "Exit". On double-click: open Settings. Tray tooltip shows current sim connection status (e.g., "iRacing — In Session", "No sim detected").
- **Acceptance Criteria**: Tray icon appears in the system notification area. All context menu actions work correctly. "Stream Mode" label updates immediately on toggle. Toggling stream mode from the tray is identical in effect to toggling from the Settings window. Tooltip reflects sim state changes within ~2 seconds.
- **Dependencies**: TASK-501, TASK-107, TASK-304.

---

### Phase 6: Polish and Integration

---

**TASK-601**
- **Title**: `SimDetector` — automatic sim detection loop
- **Phase**: 6
- **Description**: Implement `SimDetector` in `SimOverlay.App`. Register all `ISimProvider` instances (initially just `IRacingProvider`). Run a `System.Threading.Timer` every 2000 ms. On each tick: if no provider is active, iterate registered providers in the configured priority order and call `IsRunning()`. If a provider returns true, call `Start()` on it and mark it as active. If the active provider's `IsRunning()` returns false, call `Stop()` on it, clear active provider, and resume polling. Fire `ActiveProviderChanged` event.
- **Acceptance Criteria**: Starting iRacing while the app is running causes automatic connection within ~2 seconds. Closing iRacing causes disconnection within ~2 seconds. The priority order is respected when multiple `IsRunning()` checks are simulated in unit tests.
- **Dependencies**: TASK-201, TASK-304.

---

**TASK-602**
- **Title**: Single-instance enforcement
- **Phase**: 6
- **Description**: In `Program.cs`, acquire a named `Mutex` (`Global\SimOverlay_SingleInstance`) before showing any UI. If the mutex is already held, find the existing instance's tray icon window and post a message to bring its Settings window to foreground, then exit. Use `CreateMutex` via P/Invoke for global session awareness.
- **Acceptance Criteria**: Launching a second instance of the app while one is already running does not create a second set of overlays. The existing instance's Settings window is brought to the foreground.
- **Dependencies**: TASK-504.

---

**TASK-603**
- **Title**: Application icon and resources
- **Phase**: 6
- **Description**: Create a simple application icon (`.ico` file, multiple sizes: 16×16, 32×32, 48×48, 256×256). Set as the assembly icon in `SimOverlay.App.csproj`. Use the same icon for the `NotifyIcon`. Add a `Resources/` folder in `App` for embedded resources.
- **Acceptance Criteria**: Application has a recognizable icon in Explorer, taskbar (if ever shown), and the tray notification area.
- **Dependencies**: TASK-504.

---

**TASK-604**
- **Title**: Error logging
- **Phase**: 6
- **Description**: Add `Microsoft.Extensions.Logging` with a file sink writing to `%APPDATA%\SimOverlay\sim-overlay.log`. Log: sim connect/disconnect events, device lost/recovery events, config load/save events, unhandled exceptions. Cap log file at 10 MB with a single rotation (keep previous as `.log.bak`). Set `WPF Application.DispatcherUnhandledException` and `AppDomain.CurrentDomain.UnhandledException` to log and display an error dialog before exiting.
- **Acceptance Criteria**: After a session, the log file contains connection and rendering events. Deliberately throwing an unhandled exception results in a log entry and a dialog box, not a silent crash.
- **Dependencies**: TASK-001.

---

**TASK-605**
- **Title**: Performance profiling and optimization pass
- **Phase**: 6
- **Description**: Profile the running application under the following conditions: 3 overlays active, iRacing running with 40 AI cars, all overlays visible and updating. Target metrics: total CPU usage < 3% on a modern 6-core machine; GPU memory for D2D surfaces < 50 MB total; no per-frame heap allocations in the render loop (verify with PerfView/dotMemory). Optimize if needed: pre-allocate `IDWriteTextLayout` objects per row, use value types for snapshot structs, avoid LINQ in hot paths.
- **Acceptance Criteria**: App meets CPU and memory targets during a 10-minute profiling session. No observable GC pauses causing render stuttering (frame time variance < 2 ms p99 measured with `Stopwatch`).
- **Dependencies**: TASK-401, TASK-403, TASK-404.

---

**TASK-606**
- **Title**: Installer / distribution package
- **Phase**: 6
- **Description**: Create a self-contained publish profile (`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`). Output a single `.exe` with all dependencies bundled. Optionally create a simple NSIS or WiX installer that places the exe in `%ProgramFiles%\SimOverlay\`, creates a Start Menu shortcut, and offers a "Start with Windows" option during install. Include a `README.md` and `CHANGELOG.md` in the distribution.
- **Acceptance Criteria**: The published `.exe` runs on a machine without the .NET 8 runtime installed. The installer creates the correct Start Menu entry and can be uninstalled cleanly via Add/Remove Programs.
- **Dependencies**: TASK-602, TASK-603.

---

### Critical Files for Implementation

These are the foundational files that all other implementation work depends on. Getting these right first establishes the contract and structure for the entire codebase:

- `/Users/tom/work/sim/overlays/src/SimOverlay.Sim.Contracts/ISimProvider.cs` — defines the abstraction boundary between sim-specific and sim-agnostic code; all data types and the provider interface live here, and every other project depends on getting this contract correct
- `/Users/tom/work/sim/overlays/src/SimOverlay.Core/SimDataBus.cs` — the central nervous system of the application; all data flows through this bus from providers to overlays, and its thread-safety characteristics directly affect rendering correctness and sim polling reliability
- `/Users/tom/work/sim/overlays/src/SimOverlay.Rendering/OverlayWindow.cs` — the Win32 + DXGI + DirectComposition plumbing that all three MVP overlays build on; the transparent window creation, swap chain setup, and lock/unlock behavior are the most technically risky parts of the project
- `/Users/tom/work/sim/overlays/src/SimOverlay.Rendering/BaseOverlay.cs` — establishes the render loop, data snapshot pattern, and `RenderResources` lifecycle that every concrete overlay inherits; mistakes here propagate to all three MVP overlays
- `/Users/tom/work/sim/overlays/src/SimOverlay.Sim.iRacing/IRacingRelativeCalculator.cs` — the gap-to-player calculation is algorithmically subtle (wrap-around at start/finish, lap difference handling, integration with driver info), and correctness here directly determines whether the Relative overlay — the most complex and highest-value MVP overlay — shows accurate data