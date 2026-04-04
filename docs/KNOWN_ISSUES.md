# SimOverlay — Known Issues & Technical Debt

Discovered during post-Phase-2 audit (2026-04-04). Fix before or during Phase 3 unless noted.

---

## High — Fix Before Phase 3

These will cause crashes or data loss as soon as Phase 4 introduces multiple overlays.

### ISSUE-001 · `PostQuitMessage` in every overlay's `WM_DESTROY`

**File:** `src/SimOverlay.Rendering/OverlayWindow.cs:374`
**Problem:** Every `OverlayWindow` calls `PostQuitMessage(0)` on `WM_DESTROY`. Destroying any single overlay window terminates the entire message pump, killing all other overlay windows before they can clean up. Currently harmless (only one overlay exists), but **will crash the app when Phase 4 creates multiple overlays.**
**Fix:** Track a global window count; only call `PostQuitMessage` when the last overlay window is destroyed, or have `App` own the quit signal entirely.

---

### ISSUE-002 · `BaseOverlay._config` mutated in-place across threads

**File:** `src/SimOverlay.Rendering/BaseOverlay.cs:107,167-179`
**Problem:** `OnMove`/`OnSize` (UI thread) mutate individual fields of `_config` (X, Y, Width, Height) while the render thread reads `_config` in `OnRender`. This is a torn read — the render thread can see X updated but Y not yet, or Width updated but Height not yet.
**Architecture requirement:** "volatile reference to a snapshot object" — `_config` should be replaced atomically, not mutated in-place.
**Fix:** In `OnMove`/`OnSize`, create a copy of `_config` with updated fields and assign it via `Volatile.Write` (or a lock). `UpdateConfig` already replaces the reference; the move/size paths should do the same.

---

### ISSUE-003 · `RenderResources` not thread-safe

**File:** `src/SimOverlay.Rendering/RenderResources.cs`
**Problem:** `Invalidate()` (called from `UpdateConfig` on the UI thread, or `OnSize` on the UI thread) disposes D2D brushes and text formats from the brush/format dictionaries while the render thread may be inside `GetBrush` or `GetTextFormat`. Race can corrupt the dictionary or produce use-after-dispose crashes on D2D COM objects.
**Fix:** Add a `lock` guard around `Invalidate`, `GetBrush`, and `GetTextFormat`. Alternatively, use double-buffering: mark a dirty flag atomically; rebuild resources at the top of the next render frame under `RenderLock`.

---

### ISSUE-004 · Position/size changes never persisted to disk

**File:** `src/SimOverlay.Rendering/BaseOverlay.cs:160-183`
**Problem:** `OnMove` and `OnSize` update the in-memory `_config` fields but never call `ConfigStore.Save()`. All window positions and sizes are lost on every restart.
**Architecture requirement:** "Overlay position/size are saved on `WM_MOVE` / `WM_SIZE` with a 500 ms debounce."
**Fix:** Inject `ConfigStore` into `BaseOverlay` (or use the data bus to signal App). Implement a debounced save: on `OnMove`/`OnSize`, cancel any pending save timer and schedule a new one 500 ms out; on expiry, call `configStore.Save(appConfig)`.

---

## Medium — Fix During Phase 3

### ISSUE-005 · `SimDataBus.Publish` propagates subscriber exceptions

**File:** `src/SimOverlay.Core/SimDataBus.cs:45-46`
**Problem:** If any subscriber callback throws, the exception propagates to the publisher and all remaining subscribers in the list are skipped for that message.
**Fix:** Wrap each handler invocation in `try/catch` inside `Publish`. Log exceptions and continue to the next subscriber.

---

### ISSUE-006 · `ConfigStore.Load` silently swallows all exceptions

**File:** `src/SimOverlay.Core/Config/ConfigStore.cs:36`
**Problem:** Bare `catch` returns `new AppConfig()` with no logging. Corrupt config, disk full, permission error — all invisible to the user.
**Fix:** Change to `catch (Exception ex)`, call `AppLog.Exception("Failed to load config, using defaults", ex)` before returning defaults.

---

### ISSUE-007 · `OverlayWindow` created with `WS_VISIBLE` — disabled overlays flash

**File:** `src/SimOverlay.Rendering/OverlayWindow.cs:140` (`dwStyle` includes `WS_VISIBLE`)
**Problem:** The window appears on screen the instant `CreateWindowEx` returns. Architecture §7 says disabled overlays should be "created but hidden". A disabled overlay will flash briefly before the caller can call `Hide()`.
**Fix:** Remove `WS_VISIBLE` from the initial `dwStyle`. Callers explicitly call `Show()` for overlays that should be visible.

---

### ISSUE-008 · `Dispose` calls `DestroyWindow` from finalizer thread

**File:** `src/SimOverlay.Rendering/OverlayWindow.cs:500-507`
**Problem:** `DestroyWindow` is called in the unmanaged disposal path (`disposing == false`), which runs on the GC finalizer thread. Win32 requires `DestroyWindow` to be called from the thread that created the window (the UI thread). From the wrong thread it silently returns `FALSE`, leaking the `HWND`.
**Fix:** Suppress the finalizer after managed disposal (`GC.SuppressFinalize(this)`). Do not call Win32 window destruction from the finalizer. Accept the leak on ungraceful shutdown (process exit cleans up handles anyway).

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
