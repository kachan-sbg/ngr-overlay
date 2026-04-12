# Phase 13 — Data Validation & Correctness Audit

> **Goal:** Every field in every overlay displays a correct value or is explicitly hidden
> when the sim has no valid data.  This is the gate to sharing the app publicly.

## Status: `[ ]` Not started

---

## Context

v0.0.1 Alpha has all overlays functional but data has not been audited end-to-end.
Fields may show zero, garbage, or stale values when the sim doesn't have a valid value
for them.  Other simracers can't rely on the app until this is resolved.

**Reference sources:**
- iRacing: official IRSDK telemetry variable list (`irsdk_defines.h`) + forum SDK thread
- LMU: `InternalsPlugin.hpp` + `SharedMemoryInterface.hpp` (Studio 397 SDK, in repo at
  `G:/SteamLibrary/steamapps/common/Le Mans Ultimate/Support/SharedMemoryInterface/`)
- LmuDiag tool (`tools/LmuDiag/`) for live verification

---

### TASK-1301 · iRacing field audit

**Status:** `[ ]`

Verify every field published by `IRacingPoller` and `IRacingRelativeCalculator`.

**Fields to verify (per DTO):**

`TelemetryData`:
- Speed — correct unit (m/s from SDK, convert to km/h); verify at known speed
- Gear — -1=reverse, 0=neutral, 1-6; confirm sign
- RPM — range check at idle vs redline
- Throttle / Brake / Clutch — 0.0–1.0 (unfiltered preferred for overlay)
- Fuel — unit (litres vs gallons config), non-negative, drops during race
- FuelCapacity — static per car, reasonable range (20–120 L)
- SteeringAngle — sign convention (left=negative or positive?), units (radians → degrees)
- Incidents — increases on contact, never decreases during session
- MaxIncidents — from session info, > 0

`SessionData`:
- TrackLength — metres, plausible range (500 m – 10 000 m)
- AirTemp / TrackTemp — Celsius, plausible range (-10–60)
- SessionType — correct enum mapping for Practice / Qualify / Race / Test
- SessionTimeRemain — countdown, -1 when laps-based

`RelativeData` / `StandingsData`:
- Gap values — sign convention (positive = ahead, negative = behind?)
- ClassPosition — 1-based within class
- ClassColor — parsed as hex string, non-null

**Acceptance criteria:**
- [ ] All fields confirmed or corrected against SDK reference
- [ ] Any field that has no value in a given context (e.g. `MaxIncidents` in test session)
  returns a documented sentinel (`-1`, `TimeSpan.Zero`, etc.) and the overlay hides it
- [ ] `IRacingSessionDecoder` unit tests extended to cover all decoded fields
- [ ] No field silently shows `0` when the correct answer is "not available"

---

### TASK-1302 · LMU field audit

**Status:** `[ ]`

Verify every field published by `LmuPoller`, `LmuSessionDecoder`, and
`LmuRelativeCalculator` using `LmuDiag` against a live session.

**Fields to verify:**

`TelemetryData`:
- Speed — from `mLocalVel` vector magnitude; confirm m/s → km/h conversion
- Gear — mGear: -1=reverse, 0=neutral; confirm against in-game display
- RPM — mEngineRPM, range at idle vs redline
- Throttle/Brake/Clutch — unfiltered (`mUnfilteredThrottle` etc.), 0.0–1.0
- Fuel — mFuel litres; decreases; non-negative; compare to FuelCapacity
- FuelCapacity — mFuelCapacity, static per car
- SteeringAngle — mUnfilteredSteering, sign convention
- SpeedLimiter — mSpeedLimiterActive at offset 748 (confirmed); verify lamp matches
  pit-road entry/exit
- Incidents — not available in LMU; must publish `-1`, overlay must hide it

`SessionData`:
- TrackName — from mTrackName, non-empty in session
- TrackLength — mLapDist at finish line (max mLapDist), plausible value
- AirTemp / TrackTemp — mAmbientTemp / mTrackTemp, plausible Celsius
- CurrentET — mCurrentET, increases during session

`RelativeData`:
- Gaps computed from `mLapDist` / TrackLength; confirm positive = ahead
- Player identified by `playerVehicleIdx` from telemetry header (not scoring index)

**Acceptance criteria:**
- [ ] All fields confirmed with LmuDiag output or corrected
- [ ] `LmuSessionDecoder` and `LmuRelativeCalculator` unit tests extended
- [ ] SpeedLimiter lamp verified in pit lane
- [ ] Fuel matches in-game HUD within ±0.5 L

---

### TASK-1303 · Overlay display rules — hide unavailable data

**Status:** `[ ]`

Define and enforce per-overlay rules for when fields should be hidden.

**Rules to implement:**

| Overlay | Field | Hide when |
|---|---|---|
| Relative | iRating | sim doesn't provide it (LMU) |
| Relative | License | sim doesn't provide it (LMU) |
| Relative | Incidents | sim doesn't provide it (LMU) |
| Standings | iRating | sim doesn't provide it |
| Input Telemetry | Clutch bar | car has no clutch (auto-clutch enabled) |
| Fuel Calculator | All fields | not InSession |
| Delta Bar | All fields | not InSession or no valid lap data |
| Pit Helper | All fields | not in pit lane and no limiter active |
| Weather | Precipitation | sim doesn't provide it |

**Implementation notes:**
- Sentinel values: `IRating == 0`, `LicenseClass == Unknown`, `IncidentCount == -1`
  all indicate "not available".
- Overlays should check sentinels and render "—" or skip the row/field rather than
  showing `0` or `R 0.00`.
- Add an `ISimDataBus` helper or DTO property (e.g. `bool HasIRating`) if repeated
  null-checks in overlays become unwieldy.

**Acceptance criteria:**
- [ ] iRacing fields unavailable in LMU show "—" or are hidden across all overlays
- [ ] Fields not valid before InSession don't show garbage (e.g. fuel = 0 in garage)
- [ ] Input Telemetry clutch bar hidden when `TelemetryData.Clutch < 0` (sentinel TBD)
- [ ] All changes covered by `SimOverlay.Overlays.Tests` render tests

---

### TASK-1304 · Regression snapshot tests

**Status:** `[ ]`

Add recorded-session snapshot tests so future changes can't silently break field values.

**What to build:**
- Record a short JSON snapshot of each DTO type from a real iRacing + LMU session
  (sanitised — no personal data).
- Unit tests in `SimOverlay.Sim.iRacing.Tests` and `SimOverlay.Sim.LMU.Tests` feed
  these snapshots through the decoders and assert on key field values.
- Acts as a regression gate: if a struct offset or conversion changes, at least one
  snapshot test fails.

**Acceptance criteria:**
- [ ] At least one iRacing snapshot test per DTO type
- [ ] At least one LMU snapshot test per DTO type
- [ ] Tests run in CI with no live sim required (pure data, no MMF)
- [ ] Snapshots stored under `tests/Snapshots/`
