# Phase 1 — Core Rendering Infrastructure `[~]`

> [← Index](INDEX.md)

---

**TASK-101** `[x]`
- **Title**: Implement `SimDataBus`
- **Description**: In `SimOverlay.Core`, implement `ISimDataBus` and `SimDataBus`. The bus maintains a `Dictionary<Type, List<Delegate>>` of subscriber lists. `Publish<T>(T data)` iterates the subscriber list for `typeof(T)` and invokes each delegate. `Subscribe<T>(Action<T> handler)` adds the handler. `Unsubscribe<T>(Action<T> handler)` removes it. Use `ImmutableArray` swap pattern for zero-contention publish path. Thread-safe; publish can be called from any thread.
- **Acceptance Criteria**: Unit tests: (1) subscribe and receive a published message, (2) multiple subscribers all receive the message, (3) unsubscribe stops receiving, (4) publish from background thread is received, (5) subscribing/unsubscribing during publish does not throw.
- **Dependencies**: TASK-001.

---

**TASK-102** `[x]`
- **Title**: Implement `ConfigStore` and config types
- **Description**: In `SimOverlay.Core`, define `AppConfig`, `GlobalSettings`, `ColorConfig`, `OverlayConfig`, and `StreamOverrideConfig` POCOs. `StreamOverrideConfig` mirrors all overridable fields from `OverlayConfig` as nullable — X/Y position fields are NOT included (position is never overridable). Implement `OverlayConfig.Resolve(bool streamModeActive)`. Implement `ConfigStore` with atomic save (`config.json.tmp` → `File.Move` with overwrite).
- **Acceptance Criteria**: Unit tests: (1) round-trip serialize/deserialize, (2) `Resolve(false)` returns base values, (3) `Resolve(true)` with null override returns base values, (4) `Resolve(true)` with partial override mixes correctly, (5) X/Y never from override, (6) missing config returns defaults, (7) corrupt JSON returns defaults.
- **Dependencies**: TASK-001.

---

**TASK-103** `[x]`
- **Title**: Implement `Sim.Contracts` DTOs and `ISimProvider`
- **Description**: In `SimOverlay.Sim.Contracts`, define all normalized data types: `SessionData`, `DriverData`, `RelativeData`, `RelativeEntry`, `SimState` (enum), `SessionType` (enum), `LicenseClass` (enum with static color-mapping helper). Define `ISimProvider` interface with `SimId`, `IsRunning()`, `Start()`, `Stop()`, and `StateChanged` event.
- **Acceptance Criteria**: All types compile. `LicenseClass` color helper returns correct RGBA for each value. Interface is implemented correctly by a trivial stub in a test project.
- **Dependencies**: TASK-001.

---

**TASK-104** `[x]`
- **Title**: Win32 overlay window — basic transparent window creation
- **Description**: In `SimOverlay.Rendering`, create `OverlayWindow`. Register a `WNDCLASSEX`, create a window with `WS_POPUP | WS_VISIBLE` and `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOPMOST`. **Do not use `WS_EX_TOOLWINDOW`** — it hides the window from OBS. Set position and size from `OverlayConfig`. Verify click-through.
- **Acceptance Criteria**: A transparent window appears at the configured position. Always on top. Mouse clicks pass through. No title bar or border. Window appears in OBS "Window Capture" picker.
- **Dependencies**: TASK-001, TASK-102.

---

**TASK-105** `[x]`
- **Title**: DXGI swap chain and Direct2D device context setup
- **Description**: Extend `OverlayWindow` to create a D3D11 device with BGRA support, a `IDXGISwapChain1` via `CreateSwapChainForComposition` with `FLIP_SEQUENTIAL` / `PREMULTIPLIED` / `B8G8R8A8_UNORM`, a `ID2D1DeviceContext`, and a `IDCompositionDevice`/`Target`/`Visual`. Add `Render()` that clears to transparent and presents.
- **Acceptance Criteria**: Window background is genuinely transparent. No DXGI errors. Can run alongside a full-screen borderless window.
- **Dependencies**: TASK-104, TASK-002.

---

**TASK-106** `[x]`
- **Title**: Base overlay class — render loop and data snapshot pattern
- **Description**: In `SimOverlay.Rendering`, create `BaseOverlay : OverlayWindow`. Add: `OverlayConfig Config`, abstract `OnRender(ID2D1DeviceContext, OverlayConfig)`, `Subscribe<T>` helper, 60 fps render loop on a dedicated background thread, and `RenderResources` caching `ID2D1SolidColorBrush` and `IDWriteTextFormat`.
- **Acceptance Criteria**: `TestOverlay` subclass drawing a red rectangle renders at ~60 fps. Disposing stops the render loop and releases all D2D resources. `RenderResources` invalidated correctly on config change.
- **Dependencies**: TASK-105, TASK-101, TASK-102.

---

**TASK-107** `[x]`
- **Title**: Lock/unlock (edit mode) — drag and resize
- **Description**: Implement edit mode in `OverlayWindow`. When `IsLocked = false`: (1) Remove `WS_EX_TRANSPARENT` (+ `SetWindowPos(SWP_FRAMECHANGED)` to apply immediately). (2) Handle `WM_NCHITTEST`: return `HTCAPTION` for most of the client area, `HTBOTTOMRIGHT` for a 24×24 px corner hit zone. (3) Draw a 2 px accent-blue border and three diagonal grip dots in the corner. When `IsLocked = true`: re-add `WS_EX_TRANSPARENT`, remove the border. Subscribe `BaseOverlay` to `EditModeChangedEvent` on `ISimDataBus`. `OnMove`/`OnSize` keep `OverlayConfig` X/Y/Width/Height in sync. `WM_EXITSIZEMOVE` re-applies the transparent style after each drag. `WM_GETMINMAXINFO` enforces a minimum window size of `ResizeGripSize × ResizeGripSize` px. **Note**: `WS_EX_LAYERED` must NOT be used — it causes Windows to cache the alpha hit-test mask from the first DComp frame and never expand it on resize, permanently making all pixels outside the initial window size click-through. Transparency is provided by `WS_EX_NOREDIRECTIONBITMAP` + DComp premultiplied alpha.
- **Acceptance Criteria**: In unlocked mode, overlays can be dragged and resized from the bottom-right corner in any direction including growing larger than the initial size. The blue border and grip dots are visible. In locked mode, mouse clicks pass through. Position/size values are preserved in config after re-lock. Window cannot be resized below 24×24 px.
- **Dependencies**: TASK-106, TASK-102.

---

**TASK-108** `[x]`
- **Title**: Device lost recovery
- **Description**: Handle `DXGI_ERROR_DEVICE_REMOVED` and `DXGI_ERROR_DEVICE_RESET` from `Present()` or `EndDraw()`. `OverlayWindow.Render()` catches `SharpGenException` with those HRESULTs and re-throws as `DeviceLostException`. `BaseOverlay.RenderLoop` catches `DeviceLostException` and calls `RecoverDevice()`, which (1) acquires `RenderLock`, (2) releases all D3D/D2D/DComp resources, (3) recreates them at the current size, (4) calls `OnDeviceRecreated()` **while still holding `RenderLock`** so `RenderResources.UpdateContext()` runs before the render thread can execute another frame. `RenderResources` holds a mutable context reference updated by `UpdateContext()`, which also invalidates the brush/format cache. If recovery fails, backs off 1 s before retrying. Dev: F9 hotkey forces recovery, F10 quits; `TestOverlay` shows recovery count on screen.
- **Acceptance Criteria**: Disabling and re-enabling the GPU in Device Manager causes the app to recover within ~2 seconds without a crash. All overlays continue rendering after recovery.
- **Dependencies**: TASK-106.
