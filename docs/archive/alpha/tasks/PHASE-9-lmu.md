# Phase 9 вЂ” Le Mans Ultimate (LMU) Integration

> **Goal:** Add LMU as a second sim provider to validate the multi-sim architecture.
> LMU uses the rFactor 2 shared memory plugin. Not all iRacing DTOs will have LMU equivalents вЂ”
> overlays must handle missing data gracefully. This phase surfaces all multi-sim issues early,
> before the new overlays are built (Phases 10вЂ“11).

## Status: `[x]` Complete

---

### Context

LMU (Le Mans Ultimate) is built on the rFactor 2 engine and exposes telemetry via shared memory
mapped files, similar to rF2's `$rFactor2SMMP_Scoring$` and `$rFactor2SMMP_Telemetry$` memory
sections. The data model is significantly different from iRacing:

**Key differences from iRacing:**
- No iRating, no Safety Rating, no license class system
- Different session type naming ("Practice1", "Qual", "Race")
- Lap distance is absolute meters, not percentage (0.0вЂ“1.0) вЂ” must normalize
- Weather model is different (may expose more or less than iRacing)
- Pit service model is different (tire compounds, mandatory pit rules)
- No incident system (no IncidentCount equivalent)
- Car class system exists but uses different identifiers
- Session YAML does not exist вЂ” structured binary data instead

**Available NuGet packages / libraries:**
- `CrewChiefV4.rFactor2Data` вЂ” shared memory structures
- Raw P/Invoke to `OpenFileMapping` / `MapViewOfFile` using rF2 struct definitions
- Community C# libraries for rF2 shared memory (evaluate at implementation time)

---

### TASK-901 В· LMU shared memory provider

**Status:** `[x]`

Implement `ISimProvider` for LMU using rFactor 2 shared memory.

**What to build:**
- New project: `NrgOverlay.Sim.LMU` (follows same pattern as `Sim.iRacing`)
- Dependencies: `Sim.Contracts` + `Core` (same rules as `Sim.iRacing`)
- `LmuProvider : ISimProvider`
  - `SimId = "LMU"`
  - `IsRunning()`: check for LMU process name (`Le Mans Ultimate`) or shared memory file existence
  - `Start()`: create `LmuPoller`, begin reading shared memory
  - `Stop()`: dispose poller
  - `StateChanged` event with `SimState` transitions

- `LmuPoller`
  - Opens rF2 shared memory mapped files:
    - `$rFactor2SMMP_Scoring$` вЂ” positions, gaps, session info
    - `$rFactor2SMMP_Telemetry$` вЂ” car telemetry (throttle, brake, speed, fuel, etc.)
    - `$rFactor2SMMP_Rules$` вЂ” pit rules, flags (optional вЂ” read if available)
    - `$rFactor2SMMP_Extended$` вЂ” extended data (if available in LMU variant)
  - Poll at 60 Hz (same as iRacing)
  - Map rF2 structs to C# types

**Acceptance criteria:**
- [x] `LmuProvider.IsRunning()` correctly detects LMU running/not running
- [x] `Start()` opens shared memory and begins polling
- [x] `Stop()` cleanly releases shared memory handles
- [x] `StateChanged` fires on connection/disconnection
- [x] No crashes when LMU is not running
- [x] Sim.LMU project follows dependency rules (no Rendering/Overlays dependency)

**Dependencies:** None (can be done in parallel with Phase 8).

---

### TASK-902 В· LMU session and driver data mapping

**Status:** `[x]`

Map LMU shared memory data to the existing sim-agnostic DTOs.

**What to build:**
- `LmuSessionDecoder`: extract from scoring data в†’ `SessionData`
  - Track name from scoring info
  - Session type mapping: rF2 session types в†’ `SessionType` enum
  - Time remaining/elapsed from scoring
  - Air/track temps from telemetry/scoring
  - `GameTimeOfDay` вЂ” from rF2 if available, else `TimeOfDay.Unknown`

- `LmuDriverDataMapper`: extract в†’ `DriverData`
  - Position, lap count from scoring
  - Last/best lap times from scoring
  - `LapDeltaVsBestLap` вЂ” rF2 may not have a direct equivalent; compute from `BestLapTime - LastLapTime` as approximation, or set to 0 if not computable

- `LmuRelativeCalculator`: extract в†’ `RelativeData`
  - Similar to `IRacingRelativeCalculator` but using rF2's `mLapDist` (meters) instead of `LapDistPct`
  - Normalize: `LapDistPct = mLapDist / TrackLength`
  - Gap computation from track position delta Г— estimated lap time
  - Car number, driver name from scoring

**Handling missing data (critical):**
| iRacing Field | LMU Equivalent | Fallback |
|---|---|---|
| iRating | None | Display "вЂ”" or 0 |
| LicenseClass | None | `LicenseClass.Unknown` (new enum value) |
| LicenseLevel | None | Empty string |
| IncidentCount | None | -1 (sentinel for "not available") |
| LapDeltaVsBestLap | Compute from times | Approximate or 0 |
| CarClass | rF2 vehicle class | Map directly |
| ClassPosition | Compute from scoring | Compute from class sorting |

**Acceptance criteria:**
- [x] `SessionData` published with correct LMU session info
- [x] `DriverData` published with position, lap, lap times
- [x] `RelativeData` published with correct gap calculations
- [x] Missing fields (iRating, license, incidents) use defined fallback values
- [x] `LicenseClass` enum gains an `Unknown` value that overlays handle gracefully
- [x] Delta approximation is reasonable (not wildly wrong)
- [x] Unit test: rF2 scoring data в†’ RelativeData conversion
- [x] Unit test: lap distance meters в†’ LapDistPct normalization

**Dependencies:** TASK-901 (LMU provider running).

---

### TASK-903 В· LMU telemetry and pit data mapping

**Status:** `[x]`

Map LMU telemetry to the expanded Alpha DTOs (TelemetryData, PitData, WeatherData, TrackMapData).

**What to build:**
- `TelemetryData` mapping:
  - Throttle, brake, clutch: from rF2 telemetry (normalized 0вЂ“1)
  - Steering: from rF2 telemetry (radians)
  - Speed: from rF2 velocity vector magnitude
  - Gear, RPM: from rF2 telemetry
  - Fuel level: from rF2 telemetry (`mFuel` in liters)
  - Fuel consumption: rolling average (same logic as iRacing, green-flag only)
  - IncidentCount: -1 (not available in LMU)

- `PitData` mapping:
  - `IsOnPitRoad`: from rF2 `mInPits` flag
  - `PitLimiterSpeedMps`: from rF2 rules or hardcoded per-track if not exposed
  - `PitLimiterActive`: from rF2 telemetry if available
  - `PitStopCount`: count from scoring
  - `RequestedService`: rF2 pit menu has different structure вЂ” map what's available
  - `FuelToAddLiters`: from rF2 pit menu if accessible

- `WeatherData` mapping:
  - Air/track temp from rF2 scoring
  - Wind: from rF2 if available, else 0
  - Humidity, sky coverage, wetness: from rF2 weather data
  - Precipitation: from rF2 rain flag

- `TrackMapData` mapping:
  - `TrackLengthMeters`: from rF2 scoring
  - Car entries: `mLapDist / TrackLength` for normalized LapDistPct

**Acceptance criteria:**
- [x] All 4 expanded DTOs publish with LMU data where available
- [x] Missing telemetry fields use sensible defaults (0, false, -1)
- [x] Fuel consumption averaging works correctly with LMU fuel data
- [x] TrackMapData positions normalized correctly from meters to 0вЂ“1
- [x] No crashes when specific rF2 shared memory sections are unavailable

**Dependencies:** TASK-901, TASK-902, Phase 8 DTOs.

---

### TASK-904 В· Graceful degradation in overlays

**Status:** `[x]`

Ensure all existing and planned overlays handle missing/unavailable data cleanly.

**What to build:**
- Define "unavailable" sentinel values in `Sim.Contracts`:
  - `int UnavailableInt = -1`
  - `float UnavailableFloat = float.NaN`
  - `string UnavailableString = ""`
  - `LicenseClass.Unknown` (new enum member)
- Update overlays to check for unavailable values before rendering:
  - iRating column: show "вЂ”" when value is 0 or unavailable
  - License column: hide or show blank when `LicenseClass.Unknown`
  - Incident count: hide when -1
  - Delta: show "вЂ”" when NaN
- `RelativeOverlay`: `showIRating` and `showLicense` columns hide automatically when all entries have unavailable values (not just configurable toggle)
- `SessionInfoOverlay`: incident row hidden when not available
- `DeltaBarOverlay`: show "No delta data" placeholder when delta is unavailable

**Acceptance criteria:**
- [x] Every overlay renders correctly with iRacing data (no regression)
- [x] Every overlay renders correctly with LMU data (missing fields show "вЂ”" or are hidden)
- [x] No crashes, no `NaN` rendered as text, no `-1` shown as a value
- [x] `LicenseClass.Unknown` handled in all license color mappings (default grey)
- [ ] Unit test: overlay mock data with unavailable fields (defer to Overlays.Tests)

**Dependencies:** TASK-902, TASK-903.

---

### TASK-905 В· SimDetector multi-sim switching

**Status:** `[x]`

Verify and harden `SimDetector` for real multi-sim usage.

**What to build:**
- Register `LmuProvider` in the provider list alongside `IRacingProvider`
- `SimDetector` iterates providers in `SimPriorityOrder` config order (TASK-704)
- Test transitions:
  - iRacing running в†’ close iRacing в†’ start LMU в†’ overlays switch to LMU data
  - LMU running в†’ close LMU в†’ overlays show "Sim not detected"
  - Both running simultaneously в†’ higher-priority sim wins
- Debounce logic (from ISSUE-014) works for LMU's process lifecycle
- Log provider transitions clearly

**Acceptance criteria:**
- [x] `SimDetector` activates LMU provider when LMU is detected and iRacing is not running
- [x] Priority order from config is respected
- [x] Clean transition: overlays show correct data within 2 seconds of sim switch
- [x] No overlapping providers (only one active at a time)
- [x] Debounce prevents flicker on LMU startup/shutdown
- [x] Log entries for each provider transition

**Dependencies:** TASK-901, TASK-704 (SimPriorityOrder).

---

