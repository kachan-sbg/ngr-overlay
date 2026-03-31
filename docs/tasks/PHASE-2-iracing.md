# Phase 2 — iRacing Data Provider `[~]`

> [← Index](INDEX.md)

---

**TASK-201** `[x]`
- **Title**: IRacingProvider — MMF connection lifecycle
- **Description**: Implement `IRacingProvider : ISimProvider`. `IsRunning()`: attempt to open named MMF `Local\IRSDKMemMapFileName`; return true if it succeeds. `Start()`: create `IRacingPoller`, start it, fire `StateChanged(Connected)` immediately (iRacing is known running), then let IRSDKSharper's `OnConnected` fire `StateChanged(InSession)`. `Stop()`: dispose poller, fire `StateChanged(Disconnected)`. Handle exceptions in `IsRunning()` gracefully.
- **Acceptance Criteria**: With iRacing not running: `IsRunning()` returns false. With iRacing running: returns true. `Start()`/`Stop()` cycle works without leaks. `StateChanged` fires correctly.
- **Dependencies**: TASK-103, TASK-002.

---

**TASK-202** `[x]`
- **Title**: IRacingPoller — 60 Hz telemetry polling loop
- **Description**: Implement `IRacingPoller` wrapping IRSDKSharper. `OnTelemetryData` (fires at ~60 Hz internally) reads `PlayerCarPosition`, `Lap`, `LapBestLapTime`, `LapLastLapTime`, `LapDeltaToBestLap` and publishes `DriverData`. Every 6th tick also reads `CarIdxLapDistPct[64]`, `CarIdxPosition[64]`, `CarIdxLap[64]`, builds `TelemetrySnapshot`, calls `IRacingRelativeCalculator.Compute`, and publishes `RelativeData`. `EstimatedLapTime` = `DriverCarEstLapTime` from session info, falling back to best/last lap, then 90 s.
- **Acceptance Criteria**: `DriverData` published at 58–62 Hz. `RelativeData` at 9–11 Hz. No memory leaks after 10 minutes. Polling thread CPU < 2%.
- **Dependencies**: TASK-201, TASK-101.

---

**TASK-203** `[x]`
- **Title**: IRacingSessionDecoder — YAML session string parser
- **Description**: Static `IRacingSessionDecoder.Decode(IRacingSdkData)` extracts `SessionData` from the typed YAML models exposed by IRSDKSharper: `WeekendInfo.TrackDisplayName`, temperature strings (parsed from "24.44 C" format), current `SessionNum` → `Sessions[n].SessionType/Time/Laps`, and a `List<DriverSnapshot>` from `DriverInfo.Drivers` (filtering nulls, mapping `LicString` first character to `LicenseClass`). Called from `IRacingPoller.HandleSessionInfo()`.
- **Acceptance Criteria**: `SessionData` contains correct track name, session type, temperatures from a live session. YAML parsing < 5 ms. Re-published on session change.
- **Dependencies**: TASK-201, TASK-101, TASK-103.

---

**TASK-204** `[x]`
- **Title**: IRacingRelativeCalculator — gap computation
- **Description**: `Compute(rawTelemetry, driverList)` → `RelativeData`. Algorithm: (1) Get player `LapDistPct`. (2) For each car: `delta = carPct - playerPct`, normalize to `[-0.5, 0.5]` by wrapping. (3) Convert to seconds: `gapSeconds = delta * estimatedLapTime`. (4) Sort by gap. (5) Join with driver info. (6) Select N nearest (default 15). Mark player with `IsPlayer = true`. Unit tests in `RelativeCalculatorTests.cs` cover ahead/behind gaps, both S/F wrap directions, lap differences, window selection, spectator/pace-car filtering, driver info join, and edge cases.
- **Acceptance Criteria**: Unit tests: gaps correct for ahead/behind/different-lap scenarios. Wrap-around at S/F line produces near-zero gaps. Player always included and marked. Output sorted correctly.
- **Dependencies**: TASK-203.

---

**TASK-205** `[ ]`
- **Title**: Integration test — iRacing data provider end-to-end
- **Description**: Integration test (requires iRacing in a session): start `IRacingProvider`, wait for `StateChanged(InSession)`, subscribe to `DriverData`/`RelativeData`/`SessionData`, run 5 seconds, assert ≥280 `DriverData` messages, ≥45 `RelativeData` messages, and at least one `SessionData` with non-empty `TrackName`. Marked `[Category("Integration")]`, skipped when iRacing not running.
- **Acceptance Criteria**: Test passes with iRacing in a session. Skipped (not failed) without iRacing.
- **Dependencies**: TASK-202, TASK-203, TASK-204.
