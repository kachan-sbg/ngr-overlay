# Field Data Status

Tracks which overlay fields are real data, approximate, unavailable, or unverified for each supported sim.
Updated as sims are tested and fixes are merged.

**Legend:**
- ✅ Real — live data from SDK/API, accurate
- ⚠️ Snapshot — real value but only refreshed on session change (goes stale mid-session)
- 🔴 Missing — field is not exposed by this sim; shows `??` / `--` sentinel in overlay
- ❓ Unverified — compiles and renders but not yet confirmed against real in-sim values
- 🐛 Bug — known incorrect value; issue noted

---

## SessionInfo overlay

| Field | iRacing | LMU | Notes |
|-------|---------|-----|-------|
| Track name | ✅ | ✅ | |
| Session type | ✅ | ✅ | LMU: mapped from int 0–13 |
| Session remaining / laps | ✅ | ⚠️ | iRacing: live countdown from `DriverData.SessionTimeRemaining` (60 Hz), smoothed locally; LMU: snapshot at session change |
| Session elapsed ("Session" row) | ✅ | ⚠️ | iRacing: live from `DriverData.SessionTimeElapsed` (60 Hz), smoothed locally (FS-002 fixed); LMU: snapshot at session change |
| Wall clock | ✅ | ✅ | `DateTime.Now` — always current |
| Game time of day | ✅ | 🔴 | iRacing: live from `DriverData.GameTimeOfDay` (60 Hz) (FS-003 fixed); LMU: not exposed → `--:-- (??)` |
| Session best lap | ✅ | 🔴 | iRacing: min of `CarIdxBestLapTime` (60 Hz); LMU: not computed |
| Personal best lap | ✅ | ✅ | |
| Air temp | ✅ | ✅ | |
| Track temp | ✅ | ✅ | |
| Humidity | ✅ | 🔴 | LMU: not exposed → row hidden |
| Track condition | ✅ | ❓ | LMU: mapped from `MaxPathWetness` (0–1) |
| Current lap | ✅ | ✅ | |
| Last lap time | ✅ | ✅ | |
| Best lap time | ✅ | ✅ | |
| Delta (vs personal best) | ✅ | ❓ | LMU: `LastLapTime - BestLapTime`; zero until 2 laps complete |

---

## DeltaBar overlay

| Field | iRacing | LMU | Notes |
|-------|---------|-----|-------|
| Delta value | ✅ | ❓ | iRacing: `LapDeltaToSessionBestLap`; LMU: falls back to `LastLapTime - BestLapTime` |
| SB / PB label | ✅ | ❓ | Shows SB when session-best delta is non-zero; LMU always PB |
| Bar fill + color | ✅ | ❓ | Green = faster, red = slower |
| Trend arrow | ✅ | ❓ | 500 ms rolling window; appears after 30 samples |

---

## Relative overlay

| Field | iRacing | LMU | Notes |
|-------|---------|-----|-------|
| Position | ✅ | ✅ | |
| Car number | ✅ | 🐛 | **LMU: shows slot ID, not car number** — `v.Id` used as fallback; actual number not in scoring struct |
| Driver name | ✅ | ❓ | Empty → shows `??`; verify LMU name encoding |
| iRating | ✅ | 🔴 | LMU: not available → `----` |
| License | ✅ | 🔴 | LMU: not available → cell hidden |
| Gap to player | ✅ | ❓ | Calculated from `LapDistPct`; verify accuracy at different track positions |
| Lap difference | ✅ | ❓ | |
| Last lap time | ✅ | ❓ | iRacing: `CarIdxLastLapTime`; LMU: from scoring struct |

---

## Standings overlay

| Field | iRacing | LMU | Notes |
|-------|---------|-----|-------|
| Position | ✅ | ✅ | |
| Class badge | ✅ | ❓ | LMU: class derived from `VehicleClass` field; color assigned round-robin |
| Car number | ✅ | 🐛 | **LMU: same slot ID issue as Relative** |
| Driver name | ✅ | ❓ | Empty → `??` |
| iRating | ✅ | 🔴 | LMU: → `----` |
| Gap to leader | ✅ | ❓ | Progress-based (`laps+pct` diff × estLapTime); leader always gets 0f, others ≥ 0.001f |
| Best lap time | ✅ | ✅ | |

---

## InputTelemetry overlay

| Field | iRacing | LMU | Notes |
|-------|---------|-----|-------|
| Gear | ✅ | ❓ | LMU: from `LmuPlayerInputs` telemetry struct; falls back to 0 (shows N) if struct unavailable |
| Speed | ✅ | ❓ | LMU: from `player.SpeedMps` in scoring (always available); verify magnitude |
| Throttle | ✅ | ❓ | LMU: from `LmuPlayerInputs`; zero if struct unavailable |
| Brake | ✅ | ❓ | Same as throttle |
| Clutch | ✅ | ❓ | Same as throttle |
| Scrolling trace | ✅ | ❓ | Derived from throttle/brake at 60 Hz |

> **LMU telemetry struct availability**: `LmuPlayerInputs` is read from a separate memory region. If it reads as null (LMU didn't expose it yet or struct layout mismatch), all inputs show 0. The overlay will display `No telemetry` if `_telemetry` is null, but since `TelemetryData` is always published (with zeros as fallback), this sentinel won't trigger in practice — inputs will silently be all-zero. This needs a proper "inputs unavailable" flag.

---

## FuelCalculator overlay

| Field | iRacing | LMU | Notes |
|-------|---------|-----|-------|
| Fuel level | ✅ | ❓ | LMU: prefers `FuelLiters` from telemetry struct; fallback `FuelFraction/255 × capacity` (capacity comes from same struct so fallback may be 0 if struct unavailable) |
| Avg/Lap | ✅ | ❓ | 5-lap rolling average; both sims exclude yellow-flag laps |
| Laps left | ✅ | ❓ | Derived; `—` until avg available |
| Fuel needed | ✅ | ❓ | Race sessions only; lap-limited or time-limited |
| Pit add | ✅ | ❓ | Includes safety margin |

---

## Weather overlay

| Field | iRacing | LMU | Notes |
|-------|---------|-----|-------|
| Air temp | ✅ | ✅ | |
| Track temp | ✅ | ✅ | |
| Wind speed + direction | ✅ | ❓ | Verify LMU `WindDirectionDeg` is degrees, not radians |
| Humidity | ✅ | 🔴 | LMU: not exposed → row hidden |
| Sky | ✅ | 🔴 | LMU: not exposed → `??` |
| Track condition | ✅ | ❓ | LMU: `MaxPathWetness` 0–1 mapped to Dry/Damp/Wet/Very Wet/Flooded |

---

## PitHelper overlay

| Field | iRacing | LMU | Notes |
|-------|---------|-----|-------|
| On-pit-road detection | ✅ | ❓ | LMU: `PitState in {2,3,4}`; verify triggers at pit lane entry |
| Speed limit | ✅ | 🔴 | LMU: not exposed → `Limit: ??` |
| Current speed | ✅ | ✅ | LMU: from `player.SpeedMps` |
| Speed compliance (✓/✗) | ✅ | 🔴 | Cannot show without limit |
| Pit limiter active | ✅ | ❓ | LMU: from `SpeedLimiterActive` or `PitState` fallback |
| Service flags | ✅ | 🔴 | LMU: not exposed → section hidden |
| Fuel to add | ✅ | 🔴 | LMU: not exposed |
| Pit stop count | ✅ | ✅ | |
| Next stop estimate | ✅ | ❓ | Derived from fuel level / avg consumption |

---

## FlatTrackMap overlay

| Field | iRacing | LMU | Notes |
|-------|---------|-----|-------|
| Car positions (LapDistPct) | ✅ | ❓ | iRacing: filtered by `CarIdxTrackSurface >= 0` (excludes registered-not-connected garage slots); LMU: `LapDist / TrackLengthMeters` |
| Car number labels | ✅ | 🐛 | **LMU: same slot ID bug** |
| Player marker | ✅ | ✅ | |
| Pit cars | ✅ | ❓ | LMU: `PitState != 0` |
| Class colors | ✅ | ❓ | LMU: assigned round-robin fallback colors |

---

## Known bugs (open)

| ID | Sim | Overlay | Field | Description |
|----|-----|---------|-------|-------------|
| FS-001 | LMU | Relative, Standings, TrackMap | Car number | `v.Id` (slot index) shown instead of actual car number — `VehicleScoring` struct has no dedicated car number field |
| FS-004 | LMU | InputTelemetry | Throttle/Brake/Clutch | Silent zeros when `LmuPlayerInputs` struct is unavailable; no "inputs unavailable" indicator |
| FS-005 | LMU | FuelCalculator | Fuel level | Falls back to `FuelFraction/255 × capacity`; if telemetry struct is null, capacity is 0 → fuel shows 0.0 |

## Fixed bugs

| ID | Fixed | Description |
|----|-------|-------------|
| FS-002 | 2026-04-12 | Session elapsed now live from `DriverData.SessionTimeElapsed` (60 Hz) with local smoothing — SDK blips (0/-1) are filtered, display never blinks |
| FS-003 | 2026-04-12 | Game time of day now live from `DriverData.GameTimeOfDay` (60 Hz) |
