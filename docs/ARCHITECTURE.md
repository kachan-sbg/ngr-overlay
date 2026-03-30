# ARCHITECTURE.md

## Racing Simulator Overlay — Technical Architecture

### 1. Solution Structure

```
SimOverlay.sln
├── src/
│   ├── SimOverlay.Core/
│   ├── SimOverlay.Rendering/
│   ├── SimOverlay.Sim.Contracts/
│   ├── SimOverlay.Sim.iRacing/
│   ├── SimOverlay.Overlays/
│   └── SimOverlay.App/
├── tests/
│   ├── SimOverlay.Core.Tests/
│   ├── SimOverlay.Sim.iRacing.Tests/
│   └── SimOverlay.Overlays.Tests/
└── docs/
    ├── PROJECT.md
    ├── ARCHITECTURE.md
    ├── OVERLAYS.md
    └── TASKS.md
```

#### Project Dependency Graph

```
App
 ├── Rendering
 ├── Overlays
 ├── Sim.iRacing
 ├── Sim.Contracts
 └── Core

Overlays
 ├── Rendering
 ├── Sim.Contracts
 └── Core

Rendering
 └── Core

Sim.iRacing
 ├── Sim.Contracts
 └── Core

Sim.Contracts
 └── Core
```

`Core` has zero external project dependencies. `Sim.Contracts` depends only on `Core`. No project may depend on `App`. No project in `Sim.*` may depend on `Rendering` or `Overlays`.

### 2. Project Responsibilities

#### SimOverlay.Core

Responsibility: domain types, configuration schema, and the in-process data bus. No UI, no rendering, no P/Invoke.

Key types:
- `OverlayConfig` — POCO: position, size, opacity, colors, font size, enabled flag.
- `AppConfig` — root configuration object: sim priority list, list of `OverlayConfig`, global flags.
- `ConfigStore` — reads and writes `AppConfig` to `%APPDATA%\SimOverlay\config.json` using `System.Text.Json`.
- `ISimDataBus` / `SimDataBus` — thin publish/subscribe bus. Producers call `Publish<T>(T data)`. Consumers call `Subscribe<T>(Action<T> handler)`. Backed by `Channel<object>` or direct delegate dispatch on the data thread. Thread-safe.
- `SimState` — enum: `Disconnected`, `Connected`, `InSession`.

#### SimOverlay.Sim.Contracts

Responsibility: define the normalized, sim-agnostic data model that all providers produce and all overlays consume. No implementation logic.

Key types:

```
ISimProvider
  string SimId { get; }               // "iRacing", "ACC", etc.
  bool IsRunning()                     // fast detection check, polled every ~2s
  void Start()                         // begin polling loop
  void Stop()
  event Action<SimState> StateChanged

SessionData                            // published at ~1 Hz or on change
  string TrackName
  SessionType SessionType              // Practice, Qualify, Race, TimeTrial, etc.
  TimeSpan SessionTimeRemaining
  TimeSpan SessionTimeElapsed
  float AirTempC
  float TrackTempC
  TimeOfDay GameTimeOfDay              // game world time (not wall clock)

DriverData                             // published at 60 Hz
  int Position
  int Lap
  TimeSpan LastLapTime
  TimeSpan BestLapTime
  float LapDeltaVsBestLap             // seconds, negative = faster than best

RelativeEntry                          // published at 10 Hz (relative list)
  int Position
  string CarNumber
  string DriverName
  int IRating
  LicenseClass LicenseClass           // enum: R, D, C, B, A, Pro, WC
  string LicenseLevel                  // e.g., "B 3.45"
  float GapToPlayerSeconds            // negative = ahead of player
  int LapDifference                   // 0 = same lap, +1 = one lap ahead, etc.
  bool IsPlayer

RelativeData                           // published at 10 Hz
  IReadOnlyList<RelativeEntry> Entries // sorted by track position relative to player

LicenseClass                           // enum with color mapping helper
```

`ISimProvider` implementations push data by calling `ISimDataBus.Publish<T>()` directly. They do not return data from methods.

#### SimOverlay.Sim.iRacing

Responsibility: implement `ISimProvider` for iRacing using the `Local\IRSDKMemMapFileName` shared memory file.

Key types:
- `IRacingProvider : ISimProvider` — wraps IRSDKSharper (preferred) or a raw MMF reader. Manages connection lifecycle.
- `IRacingPoller` — runs on a dedicated background thread (`Thread` with `IsBackground = true`), loops at 60 Hz using a high-resolution timer (`SpinWait` + `Stopwatch` for accuracy). Reads the MMF, decodes fields, and publishes to `ISimDataBus`.
- `IRacingSessionDecoder` — parses the YAML session string (updated at ~1 Hz or on session change) into `SessionData`. Runs on the same background thread but only when the session string version changes.
- `IRacingRelativeCalculator` — computes gap-to-player from track position (`LapDistPct` for each car) and constructs `RelativeData`. Run at 10 Hz (every 6th 60 Hz tick).

Detection: `IRacingProvider.IsRunning()` checks for the existence of the `Local\IRSDKMemMapFileName` named memory-mapped file, or checks whether a process named `iRacingUI` or `iRacing simulator` is running (process name check is less fragile).

Dependency on IRSDKSharper (NuGet `IRSDKSharper`): use `IRacingSdkDatum` for field lookup by name. If IRSDKSharper is unavailable or unsuitable, fall back to raw P/Invoke `OpenFileMapping` / `MapViewOfFile` with manually defined struct offsets from the iRacing SDK documentation.

#### SimOverlay.Rendering

Responsibility: all Direct2D / DirectComposition plumbing. No sim data logic. Provides base types that overlay implementations inherit.

Key types:

`OverlayWindow`
- Creates a Win32 window with styles:
  - `WS_POPUP` (no border/title)
  - `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOPMOST`
  - `WS_EX_TOOLWINDOW` is intentionally **omitted** — its presence hides windows from OBS's window picker. See "OBS Capture Compatibility" section below.
- Sets up DXGI swap chain: `DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL`, `DXGI_ALPHA_MODE_PREMULTIPLIED`, format `DXGI_FORMAT_B8G8R8A8_UNORM`.
- Creates `IDCompositionDevice`, `IDCompositionTarget`, `IDCompositionVisual`. The visual is bound to the swap chain surface.
- Calls `IDCompositionDevice.Commit()` after each frame.
- Exposes `ID2D1DeviceContext` for subclasses to draw into.
- Handles `WM_NCHITTEST` returning `HTTRANSPARENT` in locked mode; returns `HTCAPTION` / `HTNOWHERE` etc. in unlocked mode (to allow drag).
- Handles `WM_SIZE`, `WM_MOVE`, `WM_LBUTTONDOWN` (drag), `WM_SIZING` (resize).
- Exposes `IsLocked` property. When `false`, the window accepts mouse input for drag/resize.

`BaseOverlay : OverlayWindow`
- Adds `OverlayConfig` property.
- Abstract method `OnRender(ID2D1DeviceContext context, OverlayConfig config)` — called each frame.
- Abstract method `OnDataUpdate()` — called by the data thread (via `ISimDataBus` subscription). Stores data in thread-safe fields. Does NOT call render; the render loop reads the latest snapshot.
- Manages a render loop (`ThreadPool` timer or dedicated thread) calling `Render()` at ~60 fps.
- `Render()`: clears to transparent (`ClearValue = {0, 0, 0, 0}`), calls `OnRender()`, presents the swap chain.
- Each overlay subscribes to relevant message types on `ISimDataBus` in its constructor. Subscriptions are unregistered on `Dispose()`.

Render-data synchronization: each `BaseOverlay` subclass maintains a `volatile` reference to a snapshot object (or a simple lock-free struct copy). `OnDataUpdate()` writes a new snapshot. `OnRender()` reads the latest snapshot. No blocking between the data thread and the render thread.

`RenderResources`
- Factory/cache for `ID2D1SolidColorBrush`, `IDWriteTextFormat`, and `IDWriteTextLayout` objects.
- Keyed by color/font parameters from `OverlayConfig`.
- Recreated when `OverlayConfig` changes or when the device is lost (`DXGI_ERROR_DEVICE_REMOVED`).

`DeviceLostRecovery`
- Handles `DXGI_ERROR_DEVICE_REMOVED` / `DXGI_ERROR_DEVICE_RESET`.
- Tears down and recreates the D3D11 device, DXGI swap chain, D2D device context, and `IDCompositionDevice`.
- Signals all active `OverlayWindow` instances to re-create their resources.

#### SimOverlay.Overlays

Responsibility: concrete overlay implementations. Each class inherits `BaseOverlay`, subscribes to the data types it needs, and implements `OnRender()`.

- `RelativeOverlay`
- `SessionInfoOverlay`
- `DeltaBarOverlay`

Detailed specifications in OVERLAYS.md.

#### SimOverlay.App

Responsibility: entry point, orchestration, sim detection loop, tray icon, settings window.

Key types:

`Program` / `App`
- Single-instance enforcement: named `Mutex`.
- Creates `SimDataBus`.
- Instantiates `SimDetector`.
- Reads `AppConfig` via `ConfigStore`.
- Creates and shows/hides overlay instances based on config.
- Hosts the WPF or WinUI3 settings window (loaded lazily on tray icon click).

`SimDetector`
- Runs a background timer every 2 seconds.
- Iterates the ordered list of registered `ISimProvider` instances.
- Calls `provider.IsRunning()` on each.
- When a provider transitions to running: calls `provider.Start()`, sets it as the active provider.
- When the active provider is no longer running: calls `provider.Stop()`, resumes detection.
- Only one provider is active at a time (constraint from requirements).
- Fires `ActiveProviderChanged` event.

`TrayIconController`
- Creates `NotifyIcon` (WinForms) or equivalent.
- Context menu: "Settings", "Toggle Edit Mode", "Stream Mode: Off / On", "Exit".
- "Toggle Edit Mode" broadcasts `EditModeChangedEvent` to all overlays.
- "Stream Mode" toggles `AppConfig.GlobalSettings.StreamModeActive`, saves config, broadcasts `StreamModeChangedEvent`. Label updates to reflect state.

`SettingsWindow`
- WPF `Window` or WinUI3 `Window`.
- Contains a list of overlays with enable/disable toggles.
- Per-overlay settings panel has two tabs: **"Screen"** (base config) and **"Stream Override"**.
  - Stream Override tab has an "Enable override" toggle at the top. When off, the tab fields are dimmed/disabled.
  - Each field in the Stream Override tab has a "Custom" checkbox next to it. Unchecked = inherit from base (null in JSON). Checked = use the value in the adjacent input.
  - This avoids overwhelming users: they only set the fields they actually want to differ.
- "Apply" writes updated `OverlayConfig` values to `ConfigStore` and fires `ConfigChanged`.
- "Edit Mode" toggle button.
- "Stream Mode" toggle button (mirrors tray menu).

### 3. Process Model

There is a single OS process. All windows (overlay windows + settings window) run within it.

Thread model:
- **UI Thread**: Win32 message pump for all overlay `HWND`s and the settings `Window`. All `OverlayWindow` HWNDs are created on this thread (Win32 requirement). The message pump is the standard `GetMessage` / `TranslateMessage` / `DispatchMessage` loop.
- **Render Thread(s)**: Each `BaseOverlay` has its own render loop. These call `ID2D1DeviceContext` methods and `IDXGISwapChain.Present()`. Direct2D device contexts are not thread-safe across contexts, but each overlay has its own `ID2D1DeviceContext` created from a shared `ID2D1Device` (which is thread-safe for multi-threaded factories). The shared `ID2D1Device` is created with `D2D1_FACTORY_TYPE_MULTI_THREADED`.
- **Data Thread**: One thread per active `ISimProvider`. For iRacing, this is `IRacingPoller`'s dedicated thread. Runs at 60 Hz. Publishes to `ISimDataBus`.
- **Detection Thread**: `SimDetector` timer runs on `ThreadPool`.

### 4. Rendering Pipeline

Per frame, per overlay:

1. Render loop fires (target: 60 fps, `Stopwatch`-based sleep).
2. `BaseOverlay.Render()` acquires the D2D render target from the swap chain back buffer.
3. Calls `deviceContext.BeginDraw()`.
4. Clears to fully transparent black: `deviceContext.Clear(new Color4(0, 0, 0, 0))`.
5. Calls `OnRender(deviceContext, config)` — overlay-specific drawing.
6. Calls `deviceContext.EndDraw()`. Handles `D2DERR_RECREATE_TARGET` by triggering device lost recovery.
7. Calls `swapChain.Present(1, 0)` (vsync interval 1, or 0 for uncapped).
8. Calls `dcompDevice.Commit()` to push the new frame to the composition engine.

Alpha compositing: all geometry is drawn with premultiplied alpha. The overlay's background is a filled rectangle with premultiplied RGBA from `OverlayConfig.BackgroundColor`. Text and other elements are drawn on top. The DXGI surface with `DXGI_ALPHA_MODE_PREMULTIPLIED` and `WS_EX_NOREDIRECTIONBITMAP` ensures the DWM composites the overlay window correctly over the simulator.

Font rendering: `IDWriteFactory` (shared, created once). Per overlay, `IDWriteTextFormat` objects are created from `OverlayConfig.FontSize` and cached in `RenderResources`. Monospaced font (Consolas or Cascadia Mono) preferred for tabular data in the Relative and Session Info overlays.

### 5. Data Flow

```
[iRacing MMF]
      |
      | (polled at 60 Hz on IRacingPoller thread)
      v
IRacingProvider
  - Decodes raw bytes into iRacing SDK types
  - Maps to normalized DTOs (SessionData, DriverData, RelativeData)
  - Publishes via ISimDataBus.Publish<T>()
      |
      v
SimDataBus (in-process, thread-safe)
  - Maintains subscriber lists per type
  - Calls subscriber delegates synchronously on publishing thread
      |
      +---> RelativeOverlay.OnDataUpdate(RelativeData)
      |       - Atomically replaces _latestRelativeData snapshot
      |
      +---> SessionInfoOverlay.OnDataUpdate(SessionData)
      |       - Atomically replaces _latestSessionData snapshot
      |
      +---> DeltaBarOverlay.OnDataUpdate(DriverData)
              - Atomically replaces _latestDriverData snapshot

[Render Thread per Overlay]
  - Reads latest snapshot (no blocking)
  - Calls OnRender()
  - Presents frame
```

`ISimDataBus` subscriber callbacks run on the data thread. They must be fast (just a field store). No rendering, no locking on heavy resources.

### 6. Configuration System

Configuration file location: `%APPDATA%\SimOverlay\config.json`

#### Stream Override (Dual-Profile) System

Each overlay has a **base config** (the driver's default view) and an optional **stream override config**. When stream mode is globally active, each overlay resolves its effective config by merging: stream override values take precedence over base config values for any field that is explicitly set in the override. Fields left null in the override fall back to the base config value.

This is a single window with a switchable appearance profile — not two separate windows. The same overlay window changes its visual layout, colors, column set, and size when stream mode is toggled. Both the driver's screen and OBS capture reflect the currently active profile.

**Typical use case**: Driver's base config is minimal — 5 columns, small font, subtle dark background. Stream override has more columns, larger font, colored accents. Before going live, driver activates stream mode via the tray icon or a hotkey. The overlay expands to the stream layout. After the session, they toggle back.

**Position is not part of the stream override.** The window stays at the same screen position in both profiles. Only visual/layout properties are overridable. Size (width/height) IS overridable, meaning the window can be larger in stream mode — it simply resizes.

#### Config Schema

```json
{
  "version": 1,
  "globalSettings": {
    "startWithWindows": false,
    "streamModeActive": false,
    "simPriorityOrder": ["iRacing", "ACC", "LMU", "RF2"]
  },
  "overlays": {
    "Relative": {
      "enabled": true,
      "x": 100,
      "y": 200,
      "width": 500,
      "height": 380,
      "opacity": 0.85,
      "backgroundColor": { "r": 0, "g": 0, "b": 0, "a": 0.75 },
      "textColor": { "r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0 },
      "fontSize": 13,
      "showIRating": false,
      "showLicense": false,
      "maxDriversShown": 15,
      "streamOverride": {
        "enabled": true,
        "width": 680,
        "height": 480,
        "opacity": null,
        "backgroundColor": { "r": 0.05, "g": 0.05, "b": 0.15, "a": 0.92 },
        "textColor": null,
        "fontSize": 15,
        "showIRating": true,
        "showLicense": true,
        "maxDriversShown": null
      }
    },
    "SessionInfo": { "...": "...", "streamOverride": { "enabled": false } },
    "DeltaBar": { "...": "...", "streamOverride": { "enabled": false } }
  }
}
```

`null` in a stream override field means "inherit from base config". Fields absent from the override object are also treated as null (inherit).

#### Config Types

```csharp
// Base overlay config — all fields required, no nulls
public class OverlayConfig
{
    public bool Enabled { get; set; }
    public int X { get; set; }          // position — NOT overridable by stream mode
    public int Y { get; set; }          // position — NOT overridable by stream mode
    public int Width { get; set; }
    public int Height { get; set; }
    public float Opacity { get; set; }
    public ColorConfig BackgroundColor { get; set; }
    public ColorConfig TextColor { get; set; }
    public int FontSize { get; set; }
    // overlay-specific fields (e.g., showIRating, maxDriversShown) defined in subclasses
    public StreamOverrideConfig? StreamOverride { get; set; }

    // Returns effective config for rendering — merges stream override if active
    public OverlayConfig Resolve(bool streamModeActive);
}

// Stream override — all visual fields nullable; null = inherit from base
public class StreamOverrideConfig
{
    public bool Enabled { get; set; }   // false = override defined but not active
    public int? Width { get; set; }
    public int? Height { get; set; }
    public float? Opacity { get; set; }
    public ColorConfig? BackgroundColor { get; set; }
    public ColorConfig? TextColor { get; set; }
    public int? FontSize { get; set; }
    // overlay-specific nullable fields
}
```

`Resolve(bool streamModeActive)` returns a new `OverlayConfig` where each field is taken from the stream override (if `streamModeActive && StreamOverride?.Enabled == true` and the field is non-null) or from `this`. X and Y are always from `this`.

#### ConfigStore

- Reads on startup. If file does not exist, writes defaults (all stream overrides disabled, no override values set).
- `Save()` is atomic: serialize to string → write to `config.json.tmp` → `File.Move(..., overwrite: true)`.
- `OverlayConfig` objects are passed by reference into each overlay. Config changes fire `OverlayConfig.ConfigChanged`.
- Overlay position/size are saved on `WM_MOVE` / `WM_SIZE` with a 500 ms debounce.
- `globalSettings.streamModeActive` is persisted — stream mode survives restarts (so OBS scene setup doesn't require re-toggling on every launch).

### 7. Overlay Lifecycle

1. `App` creates all overlay instances at startup (whether enabled or not).
2. Disabled overlays are created but their window is hidden (`ShowWindow(SW_HIDE)`).
3. When the user enables an overlay in Settings, `ShowWindow(SW_SHOW)` is called and the render loop starts.
4. When disabled, `ShowWindow(SW_HIDE)` and render loop pauses.
5. On `SimState.Connected`, overlays display a "waiting for session" state.
6. On `SimState.InSession`, overlays begin rendering live data.
7. On `SimState.Disconnected`, overlays display a "sim not running" state (or are hidden per user preference).
8. On application exit: `ISimProvider.Stop()` is called, all overlay windows are destroyed, `ConfigStore.Save()` is called.

### 8. Lock / Unlock (Edit Mode)

Two modes, toggled globally via tray icon or settings window:

- **Locked** (default): overlay windows have `WS_EX_TRANSPARENT` set. `WM_NCHITTEST` returns `HTTRANSPARENT`. Mouse events pass through to the simulator.
- **Unlocked (Edit Mode)**: `WS_EX_TRANSPARENT` is removed via `SetWindowLongPtr`. `WM_NCHITTEST` returns `HTCAPTION` for the interior (enabling drag) and `HTBOTTOMRIGHT` etc. for a resize grip area in the bottom-right corner. A subtle highlight border is drawn around each overlay to indicate edit mode.

Transition: `SimDataBus` publishes an `EditModeChangedEvent`. Each overlay responds by toggling its `WS_EX_TRANSPARENT` and repainting the border.

**Edit mode + Stream Mode interaction**: Edit mode operates on whichever config profile is currently active. If stream mode is on and the user drags/resizes an overlay in edit mode, the position is saved to the base config (position is shared), but the size is saved to the stream override's `Width`/`Height` fields (since size is profile-specific). This ensures that resizing in stream mode does not disturb the screen profile's dimensions.

### 9. Extensibility: Adding a New Sim

To add a new sim (e.g., Assetto Corsa Competizione):

1. Create project `SimOverlay.Sim.ACC`.
2. Implement `ISimProvider` — the detection check, start/stop lifecycle, and polling loop.
3. Map ACC-specific data to the normalized DTOs in `Sim.Contracts`. If a field does not exist in ACC, publish a sentinel value (e.g., `TimeSpan.Zero`, empty string, or a well-documented "unavailable" constant).
4. Register the new provider in `App`'s DI container / provider list.
5. Add `"ACC"` to the `simPriorityOrder` in config defaults.

No changes to `Rendering`, `Overlays`, or `Core` are required. The overlay implementations are fully sim-agnostic.

### 10. Dependency Injection

The `App` project uses `Microsoft.Extensions.DependencyInjection`. The DI container is configured at startup:

- `ISimDataBus` → `SimDataBus` (singleton)
- `ConfigStore` (singleton)
- `AppConfig` (singleton, loaded from `ConfigStore`)
- `ISimProvider` registrations: `IRacingProvider`, plus future providers
- `SimDetector` (singleton)
- All overlay classes (singleton each)
- `TrayIconController` (singleton)
- `SettingsWindow` (transient — created on demand)

### 11. OBS Capture Compatibility

SimOverlay overlays are designed as first-class OBS sources. Each overlay window can be independently added to an OBS scene as a Window Capture source.

#### Why it works
- OBS 28+ uses the **Windows Graphics Capture (WGC)** API by default for Window Capture on Windows 10 1903+.
- WGC captures at the DWM compositor level, which includes DirectComposition surfaces. It works correctly with `WS_EX_NOREDIRECTIONBITMAP` windows.
- The DXGI swap chain uses `DXGI_ALPHA_MODE_PREMULTIPLIED`. OBS Window Capture with "Allow Transparency" checked reads the alpha channel correctly — overlay backgrounds appear semi-transparent in the OBS scene without any chroma key.

#### Why `WS_EX_TOOLWINDOW` is not used
`WS_EX_TOOLWINDOW` hides the window from the taskbar and from many window enumeration APIs, including the window picker OBS uses to populate its "Window" dropdown. Without it, all overlay windows appear as selectable sources in OBS. They will briefly appear in the Windows taskbar, but this is acceptable since overlay windows are typically set up once.

#### Window titles
Each overlay has a fixed, stable title:
- `SimOverlay — Relative`
- `SimOverlay — Session Info`
- `SimOverlay — Delta`

Titles do not change between sessions. OBS remembers window capture sources by title, so sources remain valid across app restarts.

#### OBS setup per overlay
```
Add Source → Window Capture
  Window:  [SimOverlay — Relative]
  Method:  Windows Graphics Capture   ← default on modern OBS
  ✓ Allow Transparency
```

Each overlay becomes an independent OBS source that can be independently positioned, scaled, and shown/hidden per scene.

#### Legacy OBS (BitBlt method)
OBS's legacy BitBlt window capture does not work with `WS_EX_NOREDIRECTIONBITMAP` because there is no GDI redirection surface to copy. Users on OBS versions older than 28 or Windows versions older than 1903 should use Display Capture instead, or upgrade OBS. This is a documented limitation.

#### Stream Mode and OBS
When stream mode is active, the overlay switches to its stream override profile (larger, more columns, different colors etc.). Since OBS captures the same window, it automatically captures the stream layout without any OBS reconfiguration. The driver toggles stream mode once before going live; OBS sees whatever is on screen.

#### Future: OBS-only overlays
A post-MVP feature could allow marking an overlay as "stream only" — visible to OBS capture but hidden from the physical monitor the sim is displayed on. This is achievable by checking monitor assignment against the sim window's monitor and conditionally calling `ShowWindow(SW_HIDE)` for displays that match the sim's monitor.

---

### 12. Error Handling Strategy

- Device lost (GPU reset, driver update): `DeviceLostRecovery` recreates all D3D/D2D resources. Overlays pause rendering during recovery, resume automatically.
- Sim MMF not available: `ISimProvider.IsRunning()` returns false; `SimDetector` keeps polling.
- Config file corrupt: `ConfigStore` catches `JsonException`, logs it, and falls back to defaults.
- Unhandled exceptions: top-level `Application.DispatcherUnhandledException` (WPF) or equivalent logs to `%APPDATA%\SimOverlay\error.log` and displays a message box before exiting.

---