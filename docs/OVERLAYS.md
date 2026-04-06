# OVERLAYS.md

## Racing Simulator Overlay — Overlay Specifications

### Design Principles

All overlays follow the TinyPedal aesthetic:
- Dark semi-transparent background (default: ~75% opaque black).
- White or light-grey text.
- Monospaced font for tabular alignment (default: Consolas 13px).
- No window chrome, no title bar, no resize handles visible during locked mode.
- Padding: 8px internal padding on all sides.
- Row height: font size + 6px (e.g., 19px rows at 13px font).

---

## MVP Overlays (Implemented)

### Overlay 1: Relative

**Purpose**: Show approximately 15 drivers positioned relative to the player on track (by track position percentage), with the player's row highlighted.

**Window title:** `SimOverlay — Relative`
**Window defaults**: 500 × 380 px. Minimum 300 × 200 px. Maximum 1200 × 900 px.
**Update frequency**: 10 Hz.

**Columns**: POS | CAR | DRIVER NAME | iRTG | LIC | GAP | LAP

**Config fields:** enabled, x, y, width, height, opacity, backgroundColor, textColor, fontSize, showIRating, showLicense, maxDriversShown (5–21), playerHighlightColor.

Full MVP spec: [archive/mvp/OVERLAYS-MVP.md](archive/mvp/OVERLAYS-MVP.md)

---

### Overlay 2: Session Info

**Purpose**: Display session metadata and driver summary statistics.

**Window title:** `SimOverlay — Session Info`
**Window defaults**: 260 × 280 px. Min 180 × 150. Max 800 × 600.
**Update frequency**: 1 Hz (session) + 60 Hz (lap times/delta).

**Fields**: Track name, session type + time, elapsed, clock, game time, air/track temps, lap count, last/best lap, delta.

**Config fields:** showWeather, showDelta, showGameTime, use12HourClock, temperatureUnit.

Full MVP spec: [archive/mvp/OVERLAYS-MVP.md](archive/mvp/OVERLAYS-MVP.md)

---

### Overlay 3: Delta Bar

**Purpose**: Real-time delta vs best lap with animated bar.

**Window title:** `SimOverlay — Delta`
**Window defaults**: 300 × 80 px. Min 150 × 50. Max 800 × 200.
**Update frequency**: 60 Hz.

**Config fields:** deltaBarMaxSeconds (0.5–5.0), fasterColor, slowerColor, showTrendArrow, showDeltaText.

Full MVP spec: [archive/mvp/OVERLAYS-MVP.md](archive/mvp/OVERLAYS-MVP.md)

---

## Alpha Overlays (Planned)

### Overlay 4: Input Telemetry

**Purpose**: Real-time driver input visualization — throttle, brake, clutch, steering, gear, speed.

**Window title:** `SimOverlay — Input`
**Window defaults:** 200 × 300 px. Min 120 × 180. Max 500 × 600.
**Update frequency**: 60 Hz.

**Components:**
- Vertical pedal bars: throttle (green), brake (red), clutch (blue)
- Gear + speed display (large, centered)
- Scrolling input trace graph (5-second history, optional)

**Config fields:** showThrottle, showBrake, showClutch, showInputTrace, showGearSpeed, speedUnit (Kph/Mph), throttleColor, brakeColor, clutchColor.

Full spec: [tasks/PHASE-10-overlays-part1.md](tasks/PHASE-10-overlays-part1.md#task-901)

---

### Overlay 5: Fuel Calculator

**Purpose**: Fuel management — consumption tracking, laps remaining, fuel-to-add calculation.

**Window title:** `SimOverlay — Fuel`
**Window defaults:** 240 × 200 px. Min 180 × 150. Max 500 × 400.
**Update frequency**: 60 Hz (fuel level) + per-lap (averages).

**Fields:** Fuel level, avg consumption per lap, laps remaining, fuel needed to finish, fuel to add, safety margin, total pit add.

**Config fields:** fuelUnit (Liters/Gallons), fuelSafetyMarginLaps (0.0–5.0), showFuelMargin.

Full spec: [tasks/PHASE-10-overlays-part1.md](tasks/PHASE-10-overlays-part1.md#task-902)

---

### Overlay 6: Standings

**Purpose**: Full-field leaderboard with multi-class support.

**Window title:** `SimOverlay — Standings`
**Window defaults:** 520 × 500 px. Min 300 × 200. Max 1200 × 1000.
**Update frequency**: 10 Hz.

**Columns:** POS | CLS | CAR | DRIVER NAME | iRTG | GAP | BEST
**Modes:** Combined (all cars by overall position) or Class-grouped (separated by class).

**Config fields:** standingsDisplayMode (Combined/ClassGrouped), showClassBadge, showBestLap, maxStandingsRows (10–60).

Full spec: [tasks/PHASE-10-overlays-part1.md](tasks/PHASE-10-overlays-part1.md#task-903)

---

### Overlay 7: Pit Helper

**Purpose**: Pit road assistance — speed compliance, service indicators, pit stop counter.

**Window title:** `SimOverlay — Pit`
**Window defaults:** 280 × 180 px. Min 200 × 120. Max 500 × 300.
**Update frequency**: 10 Hz.

**Modes:** Full layout on pit road (speed limit, current speed, service list, fuel amount); compact off pit road (pit count, next stop estimate).

**Config fields:** pitSpeedUnit, showPitServices, showNextPitEstimate.

Full spec: [tasks/PHASE-11-overlays-part2.md](tasks/PHASE-11-overlays-part2.md#task-1001)

---

### Overlay 8: Weather

**Purpose**: Current weather conditions and forecast.

**Window title:** `SimOverlay — Weather`
**Window defaults:** 220 × 180 px. Min 160 × 120. Max 400 × 300.
**Update frequency**: 1 Hz.

**Fields:** Air temp, track temp, wind speed + direction, humidity, sky condition, track wetness, forecast (if available).

**Config fields:** showForecast, showHumidity, windSpeedUnit (Kph/Mph/Ms).

Full spec: [tasks/PHASE-11-overlays-part2.md](tasks/PHASE-11-overlays-part2.md#task-1002)

---

### Overlay 9: Flat Track Map

**Purpose**: Linearized "flat" track map — a horizontal bar with car position markers.

**Window title:** `SimOverlay — Track Map`
**Window defaults:** 400 × 60 px. Min 200 × 40. Max 800 × 100.
**Update frequency**: 10 Hz.

**Components:** Horizontal track bar (0.0–1.0), start/finish marker, car markers (car number or position labels), player marker (larger/distinct), multi-class coloring, pit car dimming.

**Config fields:** flatMapLabelMode (CarNumber/Position/None), playerMarkerSize, carMarkerSize, showPitCars.

Full spec: [tasks/PHASE-11-overlays-part2.md](tasks/PHASE-11-overlays-part2.md#task-1103)

---
