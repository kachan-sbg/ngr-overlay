# Phase 3 вЂ” Overlay Framework `[x]`

> [в†ђ Index](INDEX.md)

---

**TASK-301** `[x]`
- **Title**: Overlay manager вЂ” create, show, hide overlays from config
- **Description**: Implement `OverlayManager` in `NrgOverlay.App`. At startup: instantiate `RelativeOverlay`, `SessionInfoOverlay`, `DeltaBarOverlay`. Set position/size from config. Show/hide based on `OverlayConfig.Enabled`. Provide `EnableOverlay(string overlayId)` and `DisableOverlay(string overlayId)` that update `OverlayConfig.Enabled` and call `ConfigStore.Save()`.
- **Acceptance Criteria**: Enabled overlays visible at saved positions on startup. Disabled overlays hidden. Enable/disable works. Positions persist after restart.
- **Dependencies**: TASK-106, TASK-102.

---

**TASK-302** `[x]`
- **Title**: Position/size persistence on drag/resize
- **Description**: After `WM_MOVE`/`WM_SIZE`, persist with stream-mode awareness: **position** (X/Y) always written to base config. **Size** written to `StreamOverride.Width`/`Height` when stream mode active + override enabled; otherwise to base config. Debounce all writes with a 500 ms `System.Threading.Timer`.
- **Acceptance Criteria**: Screen mode drag saves position + size to base. Stream mode drag saves position to base, size to override. Switching profiles restores correct dimensions. Rapid dragging causes в‰¤1 file write per 500 ms.
- **Dependencies**: TASK-107, TASK-102.

---

**TASK-303** `[x]`
- **Title**: Config live-update вЂ” apply changes without restart
- **Description**: Implement `OverlayConfig.ConfigChanged` event. Settings UI fires it on save. `BaseOverlay` listens and calls `RenderResources.Invalidate()` on the next render tick (not immediately, to avoid cross-thread D2D issues). Brushes and text formats lazily recreated on next use.
- **Acceptance Criteria**: Font size change takes effect within one render frame (~16 ms). Background color updates immediately. No crashes or leaks when config changes rapidly.
- **Dependencies**: TASK-106, TASK-302.

---

**TASK-304** `[x]`
- **Title**: Sim state display in overlays вЂ” disconnected/waiting states
- **Description**: `BaseOverlay` subscribes to `SimState` changes. Three visual states: `Disconnected` в†’ render "Sim not detected", `WaitingForSession` в†’ render "Waiting for sessionвЂ¦", `Active` в†’ render live data. State stored in `_simState` field updated by data subscription; checked in `OnRender()`.
- **Acceptance Criteria**: No iRacing в†’ "Sim not detected". iRacing at menu в†’ "Waiting for session". In session в†’ live data. Exiting iRacing reverts to "Sim not detected".
- **Dependencies**: TASK-106, TASK-201.

