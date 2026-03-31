# Phase 2 — iRacing Data Provider `[ ]`

> [← Index](INDEX.md)

---

**TASK-201** `[ ]`
- **Title**: IRacingProvider — MMF connection lifecycle
- **Description**: Implement `IRacingProvider : ISimProvider`. `IsRunning()`: attempt to open named MMF `Local\IRSDKMemMapFileName`; return true if it succeeds. `Start()`: open MMF, create view accessor, fire `StateChanged(Connected)`. `Stop()`: dispose accessor/MMF, fire `StateChanged(Disconnected)`. Handle `FileNotFoundException` gracefully.
- **Acceptance Criteria**: With iRacing not running: `IsRunning()` returns false. With iRacing running: returns true. `Start()`/`Stop()` cycle works without leaks. `StateChanged` fires correctly.
- **Dependencies**: TASK-103, TASK-002.

---

**TASK-202** `[ ]`
- **Title**: IRacingPoller — 60 Hz telemetry polling loop
- **Description**: Implement `IRacingPoller` on a dedicated background thread with `Stopwatch`-based ~16.67 ms tick timing. Each tick: read iRacing SDK header for validity, read variable offsets (`PlayerCarIdx`, `CarIdxLapDistPct`, `CarIdxPosition`, `CarIdxLap`, `LapBestLapTime`, `LapLastLapTime`, `LapDeltaToBestLap`), publish `DriverData`. Every 6th tick (10 Hz): compute and publish `RelativeData`.
- **Acceptance Criteria**: `DriverData` published at 58–62 Hz. `RelativeData` at 9–11 Hz. No memory leaks after 10 minutes. Polling thread CPU < 2%.
- **Dependencies**: TASK-201, TASK-101.

---

**TASK-203** `[ ]`
- **Title**: IRacingSessionDecoder — YAML session string parser
- **Description**: Parse the iRacing SDK YAML session string when `sessionInfoUpdate` counter changes. Extract into `SessionData`: `TrackDisplayName`, `SessionType`, `SessionTime`/`SessionLaps`, `TrackSurfaceTemp`, `TrackAirTemp`, and driver list (`UserName`, `CarNumber`, `IRating`, `LicString`). Publish `SessionData` when updated.
- **Acceptance Criteria**: `SessionData` contains correct track name, session type, temperatures from a live session. YAML parsing < 5 ms. Re-published on session change.
- **Dependencies**: TASK-201, TASK-101, TASK-103.

---

**TASK-204** `[ ]`
- **Title**: IRacingRelativeCalculator — gap computation
- **Description**: `Compute(rawTelemetry, driverList)` → `RelativeData`. Algorithm: (1) Get player `LapDistPct`. (2) For each car: `delta = carPct - playerPct`, normalize to `[-0.5, 0.5]` by wrapping. (3) Convert to seconds: `gapSeconds = delta * estimatedLapTime`. (4) Sort by gap. (5) Join with driver info. (6) Select N nearest (default 15). Mark player with `IsPlayer = true`.
- **Acceptance Criteria**: Unit tests: gaps correct for ahead/behind/different-lap scenarios. Wrap-around at S/F line produces near-zero gaps. Player always included and marked. Output sorted correctly.
- **Dependencies**: TASK-203.

---

**TASK-205** `[ ]`
- **Title**: Integration test — iRacing data provider end-to-end
- **Description**: Integration test (requires iRacing in a session): start `IRacingProvider`, wait for `StateChanged(InSession)`, subscribe to `DriverData`/`RelativeData`/`SessionData`, run 5 seconds, assert ≥280 `DriverData` messages, ≥45 `RelativeData` messages, and at least one `SessionData` with non-empty `TrackName`. Marked `[Category("Integration")]`, skipped when iRacing not running.
- **Acceptance Criteria**: Test passes with iRacing in a session. Skipped (not failed) without iRacing.
- **Dependencies**: TASK-202, TASK-203, TASK-204.
