# Phase 4 вЂ” MVP Overlays `[ ]`

> [в†ђ Index](INDEX.md)
> Layout specs: [OVERLAYS.md](../OVERLAYS.md)

---

**TASK-401** `[x]`
- **Title**: Relative overlay вЂ” layout and rendering
- **Description**: Implement `RelativeOverlay : BaseOverlay` in `NrgOverlay.Overlays`. Subscribe to `RelativeData`. Each frame: draw background, column headers, and up to `MaxDriversShown` rows with columns: POS, CAR, NAME, iRTG, LIC, GAP, LAP (per OVERLAYS.md). Highlight player row with `PlayerHighlightColor`. Fill LIC cell background with `LicenseClass.GetColor()`. Use `IDWriteTextLayout` for column alignment.
- **Acceptance Criteria**: All 7 columns render correctly. Player row visually distinct. License colors correct. No unexpected text clipping. Updates visibly when cars pass the player. Correct with 1, 5, and 20+ cars.
- **Dependencies**: TASK-106, TASK-204, TASK-303.

---

**TASK-402** `[x]`
- **Title**: Relative overlay вЂ” column visibility configuration
- **Description**: Implement `showIRating` and `showLicense` flags. When a column is hidden, recalculate remaining column widths proportionally; Driver Name column gets the extra space.
- **Acceptance Criteria**: `showIRating = false` removes iRTG column and expands others. `showLicense = false` removes LIC column. Both can be hidden simultaneously. Changes take effect immediately from Settings.
- **Dependencies**: TASK-401, TASK-303.

---

**TASK-403** `[x]`
- **Title**: Session Info overlay вЂ” layout and rendering
- **Description**: Implement `SessionInfoOverlay : BaseOverlay`. Subscribe to `SessionData` (1 Hz) and `DriverData` (60 Hz). Two snapshots: `_sessionSnapshot` and `_driverSnapshot`. Render per OVERLAYS.md spec. Delta row: green if negative, red if positive. Format times as `M:SS.mmm`. Wall clock uses `DateTime.Now` in `OnRender`. Support Celsius/Fahrenheit temperature toggle.
- **Acceptance Criteria**: All rows show correct live data. Delta color changes on sign change. Wall clock updates each second. Temps show correct unit. "Waiting for session" shows placeholder dashes.
- **Dependencies**: TASK-106, TASK-202, TASK-203, TASK-303.

---

**TASK-404** `[x]`
- **Title**: Delta Bar overlay вЂ” layout and rendering
- **Description**: Implement `DeltaBarOverlay : BaseOverlay`. Subscribe to `DriverData`. Each frame: draw background, centered delta text (green/red), bar background, fill rectangle. `fillFraction = Clamp(Abs(delta) / DeltaBarMaxSeconds, 0, 1)`. Fill extends left from center for negative delta (faster), right for positive (slower). Draw center line. Optional trend arrow from 500 ms rolling buffer.
- **Acceptance Criteria**: Bar extends left (green) when faster, right (red) when slower. Center line always visible. Bar clamped at edge for large deltas. Trend arrow direction correct. Smooth at 60 Hz.
- **Dependencies**: TASK-106, TASK-202, TASK-303.

---

**TASK-405** `[~]`
- **Title**: Overlay smoke testing against live iRacing session
- **Description**: Manual test protocol in `docs/testing/manual-overlay-test.md`: (1) Launch app, all 3 overlays enabled. (2) Start iRacing, enter practice with в‰Ґ3 AI cars. (3) Verify Relative shows correct driver list, player highlighted. (4) Verify Session Info shows track, session time, lap counter, temps. (5) Drive a lap; verify Delta Bar shows value, green on fast sector, red on slow. (6) Complete timed lap; verify Last Lap and Best Lap update. (7) Verify drag/resize in edit mode. (8) Restart; verify positions and settings persist.
- **Acceptance Criteria**: All checklist items pass without crashes, visual glitches, or incorrect data.
- **Dependencies**: TASK-401, TASK-403, TASK-404.
- **Note (2026-04-05)**: Smoke test run against live iRacing session. Data renders and updates correctly across all three overlays. One known visual glitch found: brief blink every ~2 seconds caused by `SimDetector` reacting to transient `IsRunning() == false` вЂ” logged as ISSUE-014, fixed in same session.

