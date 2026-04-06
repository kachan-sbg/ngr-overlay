# Phase 11 — New Overlays (Part 2)

> **Goal:** Implement Pit Helper, Weather, and Flat Track Map overlays.
> All overlays must handle missing data gracefully across both iRacing and LMU.

## Status: `[ ]` Not started

---

### TASK-1101 · Pit Helper overlay

**Status:** `[ ]`

Pit road assistance: pit limiter status, speed compliance, service indicators, and pit stop counter.

**Window title:** `SimOverlay — Pit`
**Window defaults:** 280 × 180 px. Min 200 × 120. Max 500 × 300.

**Layout (on pit road):**
```
┌─────────────────────────────┐
│ PIT ROAD                    │
│ ═══════════════════════════ │
│ Limit    60.0 km/h          │
│ Speed    58.3 km/h    ✓     │  ← green check if under limit, red X if over
│ ─────────────────────────── │
│ Service:  Fuel  Tires       │  ← text labels for requested services
│ Fuel Add  18.7 L            │
│ ─────────────────────────── │
│ Pit Stops  2                │
└─────────────────────────────┘
```

**Layout (not on pit road):**
```
┌─────────────────────────────┐
│ Pit Stops  2                │
│ Next stop in ~4 laps        │  ← from fuel calc if enough data
└─────────────────────────────┘
```

**What to build:**
- Subscribe to `PitData` and `TelemetryData`
- **Pit limiter section (visible on pit road only):**
  - Pit road speed limit (from `PitData.PitLimiterSpeedMps`, converted to configured units)
  - Current speed with compliance indicator: green if under limit, red if over
  - Large speed display for visibility
- **Service indicators:** Text labels for each requested service (fuel, tires, tearoff, fast repair)
- **Fuel to add:** From `PitData.FuelToAddLiters` (what's set in the sim's pit menu)
- **Pit stop counter:** Total stops this session
- **Compact mode (not on pit road):** Show only pit stop count and estimated laps until next stop (uses fuel data if available)
- Handle missing data: if pit speed limit unavailable (some LMU tracks), hide the limit row
- Mock data: on pit road with 60 km/h limit, fuel + tires requested

**Config fields (new):**
- `showPitServices` (bool, default true)
- `showNextPitEstimate` (bool, default true)

**Acceptance criteria:**
- [ ] Full layout shown when `IsOnPitRoad == true`
- [ ] Compact layout shown when not on pit road
- [ ] Speed limit and current speed display in configured units
- [ ] Compliance indicator is green when under limit, red when over
- [ ] Service flags correctly mapped to visual indicators
- [ ] Pit stop count increments on each pit visit
- [ ] "Next stop in ~N laps" estimate shown when fuel data available
- [ ] Graceful when pit data partially unavailable (LMU)
- [ ] Mock data in edit mode

**Dependencies:** TASK-802 (PitData), TASK-706 (overlay registration).

---

### TASK-1102 · Weather overlay

**Status:** `[ ]`

Current weather conditions display. No forecast section for Alpha — current conditions only.

**Window title:** `SimOverlay — Weather`
**Window defaults:** 220 × 160 px. Min 160 × 100. Max 400 × 250.

**Layout:**
```
┌──────────────────────┐
│ Weather              │
│ ──────────────────── │
│ Air       22.1°C     │
│ Track     38.7°C     │
│ Wind   12 km/h  NNE  │
│ Humidity  45%        │
│ Sky       Partly ☁   │
│ Track     Dry        │
└──────────────────────┘
```

**What to build:**
- Subscribe to `WeatherData`
- **Current conditions:** Air temp, track temp, wind speed + compass direction, humidity, sky condition, track wetness
- **Wind direction:** Convert degrees to compass (N, NNE, NE, etc.)
- **Sky condition:** Map `SkyCoverage` int to descriptive text
- **Track wetness:** Map 0.0–1.0 to descriptive levels (Dry, Damp, Wet, Very Wet, Flooded)
- Handle missing fields: if a field is unavailable from the sim, hide that row
- Mock data: partly cloudy, moderate wind

**Config fields (new):**
- `showHumidity` (bool, default true)
- `showWind` (bool, default true)
- `windSpeedUnit` (enum: Kph, Mph, Ms, default Kph)
- `temperatureUnit` — reuse existing global setting

**Acceptance criteria:**
- [ ] All current conditions display correctly
- [ ] Wind direction converts degrees to 16-point compass
- [ ] Sky coverage mapped to readable text
- [ ] Track wetness mapped to readable levels
- [ ] Temperature respects global temperature unit setting
- [ ] Rows with unavailable data are hidden (not showing "—")
- [ ] Works with both iRacing and LMU weather data
- [ ] Mock data in edit mode

**Dependencies:** TASK-803 (WeatherData), TASK-706 (overlay registration).

---

### TASK-1103 · Flat Track Map overlay

**Status:** `[ ]`

Linearized "flat" track map — a horizontal bar representing the track, with car markers showing position.

**Window title:** `SimOverlay — Track Map`
**Window defaults:** 400 × 60 px. Min 200 × 40. Max 800 × 100.

**Layout:**
```
┌──────────────────────────────────────────────────────────────┐
│ S/F     3  7     ●4    12   22  1     8    55  31          │
│ ║───────·──·──────●─────·────·──·─────·────·───·──────────║ │
│        0.1 0.15  0.25  0.35 0.4 0.45 0.55 0.6 0.65        │
└──────────────────────────────────────────────────────────────┘
```

**What to build:**
- Subscribe to `TrackMapData`
- **Track bar:** A horizontal line from 0.0 (start/finish) to 1.0 (start/finish), filling the overlay width
- **Start/finish marker:** Vertical line or distinctive marker at position 0.0/1.0
- **Car markers:** Small vertical tick or dot placed at each car's `LapDistPct` position on the bar
  - Label: car number (above or below the tick, alternating to avoid overlap)
  - Or: overall position number if preferred
  - Player: larger marker, distinct color, or highlighted
  - Multi-class: marker colored by class color
  - In-pit cars: dimmed or different style
- **Scrolling vs static:** Static bar (all cars placed on fixed line). No scrolling.
- Handle overlap: when cars are very close, labels may collide — use position numbers (smaller) instead of car numbers, or skip labels for densely packed groups
- Mock data: ~20 cars distributed around the track

**Config fields (new):**
- `flatMapLabelMode` (enum: CarNumber, Position, None — default CarNumber)
- `playerMarkerSize` (float, default 8.0)
- `carMarkerSize` (float, default 4.0)
- `showPitCars` (bool, default true) — show dimmed markers for cars on pit road

**Acceptance criteria:**
- [ ] Horizontal bar fills overlay width
- [ ] Start/finish marker visible
- [ ] All on-track cars shown at correct proportional positions
- [ ] Player marker is visually distinct
- [ ] Multi-class markers colored by class
- [ ] In-pit cars visually differentiated (dimmed or smaller)
- [ ] Label overlap handled gracefully (no unreadable text stacking)
- [ ] Works with both iRacing and LMU track position data
- [ ] Mock data in edit mode

**Dependencies:** TASK-804 (TrackMapData), TASK-705 (multi-class colors), TASK-706 (overlay registration).

---
