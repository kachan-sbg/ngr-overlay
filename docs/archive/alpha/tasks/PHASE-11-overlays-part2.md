# Phase 11 вЂ” New Overlays (Part 2)

> **Goal:** Implement Pit Helper, Weather, and Flat Track Map overlays.
> All overlays must handle missing data gracefully across both iRacing and LMU.

## Status: `[x]` Complete

---

### TASK-1101 В· Pit Helper overlay

**Status:** `[x]`

Pit road assistance: pit limiter status, speed compliance, service indicators, and pit stop counter.

**Window title:** `NrgOverlay вЂ” Pit`
**Window defaults:** 280 Г— 180 px. Min 200 Г— 120. Max 500 Г— 300.

**Layout (on pit road):**
```
в”Њв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”ђ
в”‚ PIT ROAD                    в”‚
в”‚ в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ в”‚
в”‚ Limit    60.0 km/h          в”‚
в”‚ Speed    58.3 km/h    вњ“     в”‚  в†ђ green check if under limit, red X if over
в”‚ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ в”‚
в”‚ Service:  Fuel  Tires       в”‚  в†ђ text labels for requested services
в”‚ Fuel Add  18.7 L            в”‚
в”‚ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ в”‚
в”‚ Pit Stops  2                в”‚
в””в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”
```

**Layout (not on pit road):**
```
в”Њв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”ђ
в”‚ Pit Stops  2                в”‚
в”‚ Next stop in ~4 laps        в”‚  в†ђ from fuel calc if enough data
в””в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”
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
- [x] Full layout shown when `IsOnPitRoad == true`
- [x] Compact layout shown when not on pit road
- [x] Speed limit and current speed display in configured units
- [x] Compliance indicator is green when under limit, red when over
- [x] Service flags correctly mapped to visual indicators
- [x] Pit stop count increments on each pit visit
- [x] "Next stop in ~N laps" estimate shown when fuel data available
- [x] Graceful when pit data partially unavailable (LMU)
- [x] Mock data in edit mode

**Dependencies:** TASK-802 (PitData), TASK-706 (overlay registration).

---

### TASK-1102 В· Weather overlay

**Status:** `[x]`

Current weather conditions display. No forecast section for Alpha вЂ” current conditions only.

**Window title:** `NrgOverlay вЂ” Weather`
**Window defaults:** 220 Г— 160 px. Min 160 Г— 100. Max 400 Г— 250.

**Layout:**
```
в”Њв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”ђ
в”‚ Weather              в”‚
в”‚ в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ в”‚
в”‚ Air       22.1В°C     в”‚
в”‚ Track     38.7В°C     в”‚
в”‚ Wind   12 km/h  NNE  в”‚
в”‚ Humidity  45%        в”‚
в”‚ Sky       Partly вЃ   в”‚
в”‚ Track     Dry        в”‚
в””в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”
```

**What to build:**
- Subscribe to `WeatherData`
- **Current conditions:** Air temp, track temp, wind speed + compass direction, humidity, sky condition, track wetness
- **Wind direction:** Convert degrees to compass (N, NNE, NE, etc.)
- **Sky condition:** Map `SkyCoverage` int to descriptive text
- **Track wetness:** Map 0.0вЂ“1.0 to descriptive levels (Dry, Damp, Wet, Very Wet, Flooded)
- Handle missing fields: if a field is unavailable from the sim, hide that row
- Mock data: partly cloudy, moderate wind

**Config fields (new):**
- `showHumidity` (bool, default true)
- `showWind` (bool, default true)
- `windSpeedUnit` (enum: Kph, Mph, Ms, default Kph)
- `temperatureUnit` вЂ” reuse existing global setting

**Acceptance criteria:**
- [x] All current conditions display correctly
- [x] Wind direction converts degrees to 16-point compass
- [x] Sky coverage mapped to readable text
- [x] Track wetness mapped to readable levels
- [x] Temperature respects global temperature unit setting
- [x] Rows with unavailable data are hidden (not showing "вЂ”")
- [x] Works with both iRacing and LMU weather data
- [x] Mock data in edit mode

**Dependencies:** TASK-803 (WeatherData), TASK-706 (overlay registration).

---

### TASK-1103 В· Flat Track Map overlay

**Status:** `[x]`

Linearized "flat" track map вЂ” a horizontal bar representing the track, with car markers showing position.

**Window title:** `NrgOverlay вЂ” Track Map`
**Window defaults:** 400 Г— 60 px. Min 200 Г— 40. Max 800 Г— 100.

**Layout:**
```
в”Њв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”ђ
в”‚ S/F     3  7     в—Џ4    12   22  1     8    55  31          в”‚
в”‚ в•‘в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”ЂВ·в”Ђв”ЂВ·в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв—Џв”Ђв”Ђв”Ђв”Ђв”ЂВ·в”Ђв”Ђв”Ђв”ЂВ·в”Ђв”ЂВ·в”Ђв”Ђв”Ђв”Ђв”ЂВ·в”Ђв”Ђв”Ђв”ЂВ·в”Ђв”Ђв”ЂВ·в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв•‘ в”‚
в”‚        0.1 0.15  0.25  0.35 0.4 0.45 0.55 0.6 0.65        в”‚
в””в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”
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
- Handle overlap: when cars are very close, labels may collide вЂ” use position numbers (smaller) instead of car numbers, or skip labels for densely packed groups
- Mock data: ~20 cars distributed around the track

**Config fields (new):**
- `flatMapLabelMode` (enum: CarNumber, Position, None вЂ” default CarNumber)
- `playerMarkerSize` (float, default 8.0)
- `carMarkerSize` (float, default 4.0)
- `showPitCars` (bool, default true) вЂ” show dimmed markers for cars on pit road

**Acceptance criteria:**
- [x] Horizontal bar fills overlay width
- [x] Start/finish marker visible
- [x] All on-track cars shown at correct proportional positions
- [x] Player marker is visually distinct
- [x] Multi-class markers colored by class
- [x] In-pit cars visually differentiated (dimmed or smaller)
- [x] Label overlap handled gracefully (no unreadable text stacking)
- [x] Works with both iRacing and LMU track position data
- [x] Mock data in edit mode

**Dependencies:** TASK-804 (TrackMapData), TASK-705 (multi-class colors), TASK-706 (overlay registration).

---

