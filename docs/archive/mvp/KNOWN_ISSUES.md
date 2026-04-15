# NrgOverlay вЂ” Known Issues & Technical Debt

Discovered during post-Phase-2 audit (2026-04-04). Fix before or during Phase 3 unless noted.

---

## High вЂ” Fix Before Phase 3

These will cause crashes or data loss as soon as Phase 4 introduces multiple overlays.

### ~~ISSUE-001 В· `PostQuitMessage` in every overlay's `WM_DESTROY`~~ вњ… Fixed

**Fixed in:** pre-Phase-3 bugfix commit
Static `_windowCount` (Interlocked) in `OverlayWindow`. Incremented after successful
`CreateWindowEx`; decremented in `WM_DESTROY`. `PostQuitMessage(0)` only fires when
the count reaches zero.

---

### ~~ISSUE-002 В· `BaseOverlay._config` mutated in-place across threads~~ вњ… Fixed

**Fixed in:** pre-Phase-3 bugfix commit
`OnMove` and `OnSize` now hold `RenderLock` while updating `_config.X/Y` and
`_config.Width/Height`. The render thread holds the same lock for the full frame, so
it always sees a consistent snapshot.

---

### ~~ISSUE-003 В· `RenderResources` not thread-safe~~ вњ… Fixed

**Fixed in:** pre-Phase-3 bugfix commit
Added `private readonly object _lock` to `RenderResources`. `GetBrush`,
`GetTextFormat`, `Invalidate`, `UpdateContext`, and `Dispose` all acquire this lock,
eliminating the `Invalidate`-vs-`GetBrush` race.

---

### ~~ISSUE-004 В· Position/size changes never persisted to disk~~ вњ… Fixed

**Fixed in:** pre-Phase-3 bugfix commit
`BaseOverlay` now accepts optional `ConfigStore` and `AppConfig` constructor parameters.
When provided, `OnMove`/`OnSize` schedule a 500 ms debounced save via
`System.Threading.Timer`. Callers (App) supply the dependencies to opt in.

---

## Medium вЂ” Fix During Phase 3

### ~~ISSUE-005 В· `SimDataBus.Publish` propagates subscriber exceptions~~ вњ… Fixed

**Fixed in:** pre-Phase-3 bugfix commit
Each handler invocation in `Publish` is wrapped in `try/catch`. Exceptions are logged
via `AppLog.Exception` and iteration continues to the next subscriber.

---

### ~~ISSUE-006 В· `ConfigStore.Load` silently swallows all exceptions~~ вњ… Fixed

**Fixed in:** pre-Phase-3 bugfix commit
Bare `catch` replaced with `catch (Exception ex)`. Calls
`AppLog.Exception("Failed to load config, using defaults", ex)` before returning
`new AppConfig()`.

---

### ~~ISSUE-007 В· `OverlayWindow` created with `WS_VISIBLE` вЂ” disabled overlays flash~~ вњ… Fixed

**Fixed in:** pre-Phase-3 bugfix commit
`WS_VISIBLE` removed from `dwStyle` in `CreateWindowEx`. Callers must call `Show()`
explicitly (already the case in `Program.cs`).

---

### ~~ISSUE-008 В· `Dispose` calls `DestroyWindow` from finalizer thread~~ вњ… Fixed

**Fixed in:** pre-Phase-3 bugfix commit
`DestroyWindow` and `UnregisterClass` moved inside the `if (disposing)` branch of
`Dispose(bool)`. The finalizer path is a no-op; process exit cleans up handles on
ungraceful shutdown.

---

## Low вЂ” Future / Track Only

### ISSUE-009 В· `OverlayConfig.Resolve` copies `StreamOverride` reference

**File:** `src/NrgOverlay.Core/Config/OverlayConfig.cs:83`
The resolved clone contains `StreamOverride = StreamOverride` (original reference). If anyone calls `Resolve()` on the resolved copy, or mutates the override via the clone, the original is affected. Currently nobody does this, so it is safe but fragile.

---

### ISSUE-010 В· `AppConfig` missing `Version` field

**File:** `src/NrgOverlay.Core/Config/AppConfig.cs`
No version field means future config schema changes cannot be migrated gracefully. Add `public int Version { get; set; } = 1;` to `AppConfig` before shipping.

---

### ISSUE-011 В· `GlobalSettings` missing `SimPriorityOrder`

**File:** `src/NrgOverlay.Core/Config/GlobalSettings.cs`
Architecture requires `simPriorityOrder: ["iRacing", "ACC", ...]`. Not yet added вЂ” needed when `SimDetector` is implemented in Phase 6. Add `public List<string> SimPriorityOrder { get; set; } = ["iRacing"];` when SimDetector work begins.

---

### ISSUE-012 В· `AppLog` opens/closes file per write

**File:** `src/NrgOverlay.Core/AppLog.cs`
Uses `File.AppendAllText` which opens and closes the file handle on every log entry. Fine at current logging rates; would become a bottleneck if high-frequency logging is added. Consider `StreamWriter` with auto-flush if this ever becomes a problem.

---

### ISSUE-013 В· `IRacingPoller._cachedDrivers` volatile reference to a mutable list

**File:** `src/NrgOverlay.Sim.iRacing/IRacingPoller.cs:35`
The volatile reference swap is correct (atomic publish of a new list), but the list object itself is not immutable. Currently safe because `IRacingSessionDecoder.Decode` always returns a fresh list and nobody mutates it after assignment. Consider changing to `ImmutableArray<DriverSnapshot>` to make the contract explicit.

---

### ~~ISSUE-015 В· Overlay windows appear in the Windows taskbar~~ вњ… Fixed

**Fixed in:** Phase 6 commit

Overlay windows were created with `hWndParent: nint.Zero` (no owner), causing Win32 to assign each a taskbar button. Fixed by creating a hidden 0Г—0 `WS_EX_TOOLWINDOW` owner HWND for each `OverlayWindow`. Win32 does not create taskbar buttons for owned windows. The `WS_EX_TOOLWINDOW` flag is only on the invisible owner вЂ” not on the overlays themselves вЂ” so OBS WGC still enumerates them via `EnumWindows`.

---

### ~~ISSUE-016 В· Settings window had no taskbar icon and `ShowInTaskbar=False`~~ вњ… Fixed

**Fixed in:** Phase 6 commit

`SettingsWindow.xaml` had `ShowInTaskbar="False"` per the original TASK-501 spec, making the window impossible to find or restore without the tray icon. Changed to `ShowInTaskbar="True"`. Icon loaded from `Resources/nrgoverlay.ico` at runtime (falls back gracefully if file absent). The X button correctly hides to tray (`Window_Closing` cancels and calls `Hide()`) вЂ” the only way to fully exit is via the tray menu.

---

### ~~ISSUE-014 В· Overlays blink every ~2 seconds~~ вњ… Fixed

**Fixed in:** post-Phase-4 bugfix commit (two separate changes)

Root cause was **not** SimDetector вЂ” the sim was not yet started during testing. Actual cause: `BaseOverlay.RenderLoop` called `BringToFront()` every 120 frames (~2 s at 60 fps) as a z-order safety-net fallback. `BringToFront` calls `SetWindowPos(HWND_TOPMOST)`, which causes DWM to briefly re-composite the layered window вЂ” producing a 1-frame visual gap. Removed the periodic call; `ZOrderHook` (EVENT_OBJECT_REORDER) already handles z-order reactively so the fallback was redundant.

The SimDetector debounce (`DisconnectThreshold = 2`) was also applied in the same session as a defensive improvement against transient `IsRunning() == false` results, even though it was not the blink cause.


