# SimOverlay — Known Issues & Technical Debt

Discovered during post-Phase-2 audit (2026-04-04). Fix before or during Phase 3 unless noted.

---

## High — Fix Before Phase 3

These will cause crashes or data loss as soon as Phase 4 introduces multiple overlays.

### ~~ISSUE-001 · `PostQuitMessage` in every overlay's `WM_DESTROY`~~ ✅ Fixed

**Fixed in:** pre-Phase-3 bugfix commit
Static `_windowCount` (Interlocked) in `OverlayWindow`. Incremented after successful
`CreateWindowEx`; decremented in `WM_DESTROY`. `PostQuitMessage(0)` only fires when
the count reaches zero.

---

### ~~ISSUE-002 · `BaseOverlay._config` mutated in-place across threads~~ ✅ Fixed

**Fixed in:** pre-Phase-3 bugfix commit
`OnMove` and `OnSize` now hold `RenderLock` while updating `_config.X/Y` and
`_config.Width/Height`. The render thread holds the same lock for the full frame, so
it always sees a consistent snapshot.

---

### ~~ISSUE-003 · `RenderResources` not thread-safe~~ ✅ Fixed

**Fixed in:** pre-Phase-3 bugfix commit
Added `private readonly object _lock` to `RenderResources`. `GetBrush`,
`GetTextFormat`, `Invalidate`, `UpdateContext`, and `Dispose` all acquire this lock,
eliminating the `Invalidate`-vs-`GetBrush` race.

---

### ~~ISSUE-004 · Position/size changes never persisted to disk~~ ✅ Fixed

**Fixed in:** pre-Phase-3 bugfix commit
`BaseOverlay` now accepts optional `ConfigStore` and `AppConfig` constructor parameters.
When provided, `OnMove`/`OnSize` schedule a 500 ms debounced save via
`System.Threading.Timer`. Callers (App) supply the dependencies to opt in.

---

## Medium — Fix During Phase 3

### ~~ISSUE-005 · `SimDataBus.Publish` propagates subscriber exceptions~~ ✅ Fixed

**Fixed in:** pre-Phase-3 bugfix commit
Each handler invocation in `Publish` is wrapped in `try/catch`. Exceptions are logged
via `AppLog.Exception` and iteration continues to the next subscriber.

---

### ~~ISSUE-006 · `ConfigStore.Load` silently swallows all exceptions~~ ✅ Fixed

**Fixed in:** pre-Phase-3 bugfix commit
Bare `catch` replaced with `catch (Exception ex)`. Calls
`AppLog.Exception("Failed to load config, using defaults", ex)` before returning
`new AppConfig()`.

---

### ~~ISSUE-007 · `OverlayWindow` created with `WS_VISIBLE` — disabled overlays flash~~ ✅ Fixed

**Fixed in:** pre-Phase-3 bugfix commit
`WS_VISIBLE` removed from `dwStyle` in `CreateWindowEx`. Callers must call `Show()`
explicitly (already the case in `Program.cs`).

---

### ~~ISSUE-008 · `Dispose` calls `DestroyWindow` from finalizer thread~~ ✅ Fixed

**Fixed in:** pre-Phase-3 bugfix commit
`DestroyWindow` and `UnregisterClass` moved inside the `if (disposing)` branch of
`Dispose(bool)`. The finalizer path is a no-op; process exit cleans up handles on
ungraceful shutdown.

---

## Low — Future / Track Only

### ISSUE-009 · `OverlayConfig.Resolve` copies `StreamOverride` reference

**File:** `src/SimOverlay.Core/Config/OverlayConfig.cs:83`
The resolved clone contains `StreamOverride = StreamOverride` (original reference). If anyone calls `Resolve()` on the resolved copy, or mutates the override via the clone, the original is affected. Currently nobody does this, so it is safe but fragile.

---

### ISSUE-010 · `AppConfig` missing `Version` field

**File:** `src/SimOverlay.Core/Config/AppConfig.cs`
No version field means future config schema changes cannot be migrated gracefully. Add `public int Version { get; set; } = 1;` to `AppConfig` before shipping.

---

### ISSUE-011 · `GlobalSettings` missing `SimPriorityOrder`

**File:** `src/SimOverlay.Core/Config/GlobalSettings.cs`
Architecture requires `simPriorityOrder: ["iRacing", "ACC", ...]`. Not yet added — needed when `SimDetector` is implemented in Phase 6. Add `public List<string> SimPriorityOrder { get; set; } = ["iRacing"];` when SimDetector work begins.

---

### ISSUE-012 · `AppLog` opens/closes file per write

**File:** `src/SimOverlay.Core/AppLog.cs`
Uses `File.AppendAllText` which opens and closes the file handle on every log entry. Fine at current logging rates; would become a bottleneck if high-frequency logging is added. Consider `StreamWriter` with auto-flush if this ever becomes a problem.

---

### ISSUE-013 · `IRacingPoller._cachedDrivers` volatile reference to a mutable list

**File:** `src/SimOverlay.Sim.iRacing/IRacingPoller.cs:35`
The volatile reference swap is correct (atomic publish of a new list), but the list object itself is not immutable. Currently safe because `IRacingSessionDecoder.Decode` always returns a fresh list and nobody mutates it after assignment. Consider changing to `ImmutableArray<DriverSnapshot>` to make the contract explicit.

---

### ~~ISSUE-014 · Overlays blink every ~2 seconds~~ ✅ Fixed

**Fixed in:** post-Phase-4 bugfix commit (two separate changes)

Root cause was **not** SimDetector — the sim was not yet started during testing. Actual cause: `BaseOverlay.RenderLoop` called `BringToFront()` every 120 frames (~2 s at 60 fps) as a z-order safety-net fallback. `BringToFront` calls `SetWindowPos(HWND_TOPMOST)`, which causes DWM to briefly re-composite the layered window — producing a 1-frame visual gap. Removed the periodic call; `ZOrderHook` (EVENT_OBJECT_REORDER) already handles z-order reactively so the fallback was redundant.

The SimDetector debounce (`DisconnectThreshold = 2`) was also applied in the same session as a defensive improvement against transient `IsRunning() == false` results, even though it was not the blink cause.

