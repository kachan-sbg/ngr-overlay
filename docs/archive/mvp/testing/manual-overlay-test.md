# Manual Overlay Smoke Test — TASK-405

Run this checklist against a live iRacing session before marking TASK-405 complete.

---

## Setup

1. Build `SimOverlay.sln` (Release or Debug, x64).
2. Delete `%APPDATA%\SimOverlay\config.json` if one exists from a previous run (ensures defaults are used).
3. Launch `SimOverlay.App.exe`.
4. Open iRacing, create a **practice** session with at least 3 AI cars at any track.
5. Drive to the grid / pit lane so you are in-session.

---

## Checklist

### A — App startup

- [ ] App starts without crashing.
- [ ] All 3 overlay windows appear (Relative, Session Info, Delta Bar).
- [ ] Overlays are visible and not fully transparent.
- [ ] Overlays are always-on-top (visible over iRacing window).

---

### B — Relative overlay

- [ ] Drivers are listed with POS, CAR (#N), DRIVER NAME, GAP, LAP columns.
- [ ] Player row is highlighted with a different background color.
- [ ] `►` marker appears on the player row.
- [ ] GAP shows `0.00` for the player row.
- [ ] Other drivers show positive/negative gap in `±X.XX` format.
- [ ] As AI cars lap, the list updates and gaps change.
- [ ] Player row stays centered when cars pass.
- [ ] LAP column shows `0`, `+1`, `-1` etc. correctly.

Optional (if iRating/License visible in config):
- [ ] iRTG column shows numeric values or `----`.
- [ ] LIC column shows colored background cell.

---

### C — Session Info overlay

- [ ] Track name shown at top (e.g., "Lime Rock Park").
- [ ] Session type shown (e.g., "Practice · MM:SS remaining" or "Practice · HH:MM:SS remaining").
- [ ] Session elapsed time counts up each second.
- [ ] Clock shows correct local wall time, updating each second.
- [ ] Game time of day shows sim time with descriptor (morning/afternoon/etc.).
- [ ] Air and Track temps show numeric values with °C unit.
- [ ] Lap counter increments when you cross the start/finish line.
- [ ] Last lap shows `--:--.---` until you complete a lap, then updates.
- [ ] Best lap shows `--:--.---` until you complete a timed lap, then updates.
- [ ] Delta row shows a value; color is green when ahead of best, red when behind.

---

### D — Delta Bar overlay

- [ ] Delta numeric value is displayed (centered, larger text).
- [ ] Value is green when negative (faster than best lap).
- [ ] Value is red when positive (slower than best lap).
- [ ] Bar fills to the LEFT (green) when faster.
- [ ] Bar fills to the RIGHT (red) when slower.
- [ ] Center zero line is always visible.
- [ ] Bar is clamped at edge for delta > 2.0 s.
- [ ] Trend arrow (▲/▼) appears after ~30 frames of data and changes direction.

---

### E — Edit mode (drag and resize)

- [ ] Right-click tray icon → "Edit Overlays" (or equivalent) enters edit mode.
- [ ] Blue border and resize grip appear on all overlays.
- [ ] Overlays can be dragged to a new position.
- [ ] Overlays can be resized from the bottom-right corner grip.
- [ ] After locking, mouse clicks pass through again.
- [ ] After restarting the app, positions and sizes are preserved.

---

### F — Restart persistence

- [ ] Close and relaunch the app.
- [ ] Overlays reappear at the same positions and sizes as before.
- [ ] Enabled/disabled state is preserved.

---

## Known limitations (not failures)

- iRTG and LIC columns are hidden by default (`showIRating: false`, `showLicense: false`). Edit `config.json` manually to test them.
- Temperature Fahrenheit toggle requires manual `config.json` edit (`"temperatureUnit": "Fahrenheit"`).
- Stream override mode requires manual config edit to test.
