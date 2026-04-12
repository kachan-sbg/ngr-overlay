# Phase 10 — New Overlays (Part 1)

> **Goal:** Implement the three most-requested overlay types: Input Telemetry, Fuel Calculator, and Standings.
> All overlays must handle missing data gracefully (LMU from Phase 9 may not provide all fields).

## Status: `[x]` Complete

---

### TASK-1001 · Input Telemetry overlay

**Status:** `[x]`

Real-time visualization of driver inputs: throttle, brake, clutch, steering, gear, and speed.

**Window title:** `SimOverlay — Input`
**Window defaults:** 200 × 300 px. Min 120 × 180. Max 500 × 600.

**Layout:**
```
┌────────────────────────┐
│     5   187 km/h       │  ← gear + speed
│  ┌──┐  ┌──┐  ┌──┐     │
│  │  │  │▓▓│  │  │     │  ← T / B / C vertical bars
│  │  │  │▓▓│  │  │     │
│  │▓▓│  │▓▓│  │  │     │
│  │▓▓│  │▓▓│  │  │     │
│  └──┘  └──┘  └──┘     │
│   T     B     C        │
│ ┌──────────────────┐   │
│ │ ▓▓▓▓▓▓▓▓▓░░░░░░ │   │  ← scrolling trace (optional)
│ │ ▓▓▓▓░░░░░░░░░░░ │   │
│ └──────────────────┘   │
└────────────────────────┘
```

**What to build:**
- Subscribe to `TelemetryData` from bus
- **Pedal bars:** Three vertical bars for throttle (green), brake (red), clutch (blue). Fill height proportional to 0.0–1.0 value. Bar width scales with overlay width.
- **Gear + speed:** Large gear number centered above bars. Speed in configured units (km/h or mph).
- **Scrolling trace (optional):** Horizontal time-series graph scrolling left. Throttle = green line, brake = red line. Fixed 5-second window. Ring buffer of samples at 60 Hz (300 samples).
- Mock data: smooth sine-wave throttle/brake with gear shifts
- Handle missing data: if `TelemetryData` unavailable, show "No telemetry" placeholder

**Config fields (new on OverlayConfig):**
- `showThrottle` (bool, default true)
- `showBrake` (bool, default true)
- `showClutch` (bool, default true)
- `showInputTrace` (bool, default true)
- `showGearSpeed` (bool, default true)
- `speedUnit` (enum: Kph, Mph, default Kph)
- `throttleColor`, `brakeColor`, `clutchColor` (ColorConfig)

**Acceptance criteria:**
- [x] Bars render at correct fill proportions for both iRacing and LMU
- [x] Scrolling trace shows 5 seconds of history, scrolls smoothly
- [x] Gear displays correctly (-1=R, 0=N, 1–8)
- [x] Speed unit conversion correct (m/s → km/h or mph)
- [x] All sub-components individually toggleable via config
- [x] Mock data shows realistic input pattern in edit mode
- [x] Stream override works for all new config fields

**Dependencies:** TASK-801 (TelemetryData), TASK-706 (overlay registration).

---

### TASK-1002 · Fuel Calculator overlay

**Status:** `[x]`

Read-only fuel management overlay: consumption tracking, laps remaining estimate, fuel-to-add calculation. No sim interaction — estimation only.

**Window title:** `SimOverlay — Fuel`
**Window defaults:** 240 × 200 px. Min 180 × 150. Max 500 × 400.

**Layout:**
```
┌──────────────────────────┐
│ Fuel                     │
│ ─────────────────────── │
│ Level      12.4 L        │
│ Avg/Lap     2.83 L       │
│ Laps Left   4.4          │
│ ─────────────────────── │
│ Needed     28.3 L        │  ← fuel to finish (time or laps remaining)
│ To Add     15.9 L        │  ← needed - current level
│ + Margin    2.8 L        │  ← safety margin (configurable laps)
│ ═══════════════════════ │
│ PIT ADD    18.7 L        │  ← bold: total to add at next stop
└──────────────────────────┘
```

**What to build:**
- Subscribe to `TelemetryData` (fuel level, consumption avg), `SessionData` (laps/time remaining)
- **Read-only** — no writing to the sim's pit menu. Estimation only.
- **Fuel tracking:** Display current fuel level in configured units (liters or gallons)
- **Consumption:** Rolling average of green-flag laps (from `TelemetryData.FuelConsumptionPerLap`)
- **Laps remaining:** `FuelLevel / AvgConsumption`
- **Fuel to finish:**
  - Lap-limited race: `RemainingLaps × AvgConsumption`
  - Time-limited race: `(TimeRemaining / AvgLapTime) × AvgConsumption`
  - Practice/Quali: show laps remaining only (no "fuel to finish")
- **Fuel to add:** `max(0, FuelToFinish - CurrentFuel)`
- **Safety margin:** Configurable extra laps of fuel (default: 1.0 lap)
- **Pit add total:** `FuelToAdd + (SafetyMarginLaps × AvgConsumption)`
- Mock data: mid-race scenario with realistic fuel numbers
- Works with both iRacing and LMU (both provide fuel level)

**Config fields (new on OverlayConfig):**
- `fuelUnit` (enum: Liters, Gallons, default Liters)
- `fuelSafetyMarginLaps` (float, 0.0–5.0, default 1.0)
- `showFuelMargin` (bool, default true)

**Acceptance criteria:**
- [x] Fuel level displays in configured unit with correct L↔gal conversion
- [x] Laps remaining = level / avg consumption
- [x] Fuel to finish accounts for session type (lap-limited vs time-limited)
- [x] Safety margin adds configurable extra fuel
- [x] "PIT ADD" row is prominently styled (bold or accent color)
- [x] Shows "—" for computed fields until at least 1 green-flag lap of data
- [x] Practice/Quali mode: shows laps remaining, hides "needed to finish" section
- [x] Mock data in edit mode
- [x] Works with both iRacing and LMU fuel data

**Dependencies:** TASK-801 (TelemetryData), TASK-706 (overlay registration).

---

### TASK-1003 · Standings overlay

**Status:** `[x]`

Full-field leaderboard showing all drivers sorted by race position, with multi-class support.

**Window title:** `SimOverlay — Standings`
**Window defaults:** 520 × 500 px. Min 300 × 200. Max 1200 × 1000.

**Layout:**
```
┌─────────────────────────────────────────────────────────────┐
│  POS  CLS  CAR   DRIVER NAME         iRTG  GAP      BEST   │
│─────────────────────────────────────────────────────────────│
│   1   GTP  #91   K. Estre            8234  LEADER   1:42.3  │
│   2   GTP  #92   M. Campbell         7891  +1.234   1:42.8  │
│   3   GTP  #3    A. Garcia           7654  +4.567   1:43.1  │
│ ► 4   GTP  #4    T. Milner           6789  +8.901   1:43.5  │
│   5   GTP  #62   N. Tandy            7234  +12.34   1:43.2  │
│ ──── LMP2 ────────────────────────────────────────────────  │
│   6   LMP  #31   R. Rast             5678  +1 LAP   1:41.2  │
│   7   LMP  #22   F. Albuquerque      5432  +1 LAP   1:41.5  │
│ ──── GT3 ─────────────────────────────────────────────────  │
│   8   GT3  #77   M. Martin           4321  +2 LAPS  1:47.8  │
│   ...                                                        │
└─────────────────────────────────────────────────────────────┘
```

**What to build:**
- Subscribe to `RelativeData` (reuses same entries, just sorted differently) and `SessionData`
- Two display modes:
  - **Combined:** All cars sorted by overall position, class badge shown per row
  - **Class-grouped:** Cars grouped by class with class separator rows
- Columns: POS, CLS (class badge, colored), CAR, DRIVER NAME, iRTG, GAP, BEST
- iRTG column: auto-hidden when no driver has iRating data (LMU sessions)
- Gap display: "+X.XXX" for same-lap gaps; "+N LAP(S)" for lapped cars
- Player row highlighted (same as Relative overlay)
- Multi-class: class name shown as colored badge or separator row

**Data note:** This overlay needs data for ALL drivers, not just the ±15 around the player. Either publish a separate `StandingsData` with all drivers sorted by position, or expand the relative calculator to provide the full field for Standings.

**Config fields (new):**
- `standingsDisplayMode` (enum: Combined, ClassGrouped, default Combined)
- `showClassBadge` (bool, default true)
- `showBestLap` (bool, default true)
- `maxStandingsRows` (int, 10–60, default 30)

**Acceptance criteria:**
- [x] All drivers in session shown (not just ±15)
- [x] Sorted by overall position in Combined mode
- [x] Class separators shown in ClassGrouped mode
- [x] Class badge colored with class color from session data
- [x] Gap to leader computed correctly (lapped cars show "+N LAP(S)")
- [x] Player row highlighted
- [x] Multi-class and single-class sessions both work
- [x] iRating column auto-hidden when data unavailable (LMU)
- [x] Mock data shows a multi-class scenario
- [x] Configurable column visibility

**Dependencies:** TASK-705 (multi-class data model), TASK-706 (overlay registration). Need to either modify `IRacingRelativeCalculator` or add a separate standings data publisher.

---
