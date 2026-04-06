# Phase 8 — Data Pipeline Extensions

> **Goal:** Expand the iRacing data extraction to support all new Alpha overlays.
> New DTOs and poller channels — no rendering changes in this phase.
> DTOs are designed to be sim-agnostic; LMU will implement the same interfaces in Phase 9.

## Status: `[ ]` Not started

---

### TASK-801 · Telemetry DTOs and extraction

**Status:** `[ ]`

Add real-time telemetry data types for input, speed, fuel, and gear.

**What to build:**
- New DTO in `Sim.Contracts`:
  ```csharp
  public sealed record TelemetryData(
      float Throttle,          // 0.0–1.0
      float Brake,             // 0.0–1.0
      float Clutch,            // 0.0–1.0
      float SteeringAngle,     // radians, negative = left
      float SpeedMps,          // meters per second
      int Gear,                // -1=R, 0=N, 1–8
      float Rpm,               // engine RPM
      float FuelLevelLiters,   // current fuel in tank
      float FuelConsumptionPerLap,  // rolling average (green-flag laps only)
      int IncidentCount        // session incidents
  );
  ```
- `IRacingPoller`: extract these fields from IRSDKSharper telemetry data on each 60 Hz tick
- Publish `TelemetryData` on `ISimDataBus` at 60 Hz
- Fuel consumption averaging: maintain a rolling buffer of last N green-flag laps; compute average per-lap consumption; publish the average (not raw per-tick burn rate)

**Acceptance criteria:**
- [ ] `TelemetryData` published at 60 Hz when connected
- [ ] Throttle/Brake/Clutch values are 0.0–1.0 normalized
- [ ] Fuel consumption is a rolling average over green-flag laps only (not under caution/pit)
- [ ] `IncidentCount` reflects the driver's current session incident total
- [ ] Unit test: fuel consumption averaging with mixed green/caution laps

**Dependencies:** None.

---

### TASK-802 · Pit service data extraction

**Status:** `[ ]`

Extract pit-related telemetry for the Pit Helper overlay.

**What to build:**
- New DTO in `Sim.Contracts`:
  ```csharp
  public sealed record PitData(
      bool IsOnPitRoad,
      bool IsInPitStall,
      float PitLimiterSpeedMps,  // pit road speed limit
      float CurrentSpeedMps,      // redundant with TelemetryData but grouped for convenience
      bool PitLimiterActive,      // whether the sim's pit limiter is engaged
      int PitStopCount,           // number of pit stops this session
      PitServiceFlags RequestedService,  // flags: Fuel, Tires, WindshieldTearoff, FastRepair
      float FuelToAddLiters       // amount requested in pit menu
  );

  [Flags]
  public enum PitServiceFlags
  {
      None = 0,
      Fuel = 1,
      LeftFrontTire = 2,
      RightFrontTire = 4,
      LeftRearTire = 8,
      RightRearTire = 16,
      AllTires = LeftFrontTire | RightFrontTire | LeftRearTire | RightRearTire,
      WindshieldTearoff = 32,
      FastRepair = 64
  }
  ```
- `IRacingPoller`: extract from iRacing telemetry vars (`OnPitRoad`, `PitSvFlags`, `dpFuelFill`, `PitLimiterOn`, etc.)
- Publish `PitData` at 10 Hz (every 6th tick, same as RelativeData)

**Acceptance criteria:**
- [ ] `PitData` published when connected
- [ ] `PitLimiterSpeedMps` correctly reflects the track's pit road speed limit
- [ ] `PitServiceFlags` correctly maps from iRacing's `PitSvFlags` bitmask
- [ ] `FuelToAddLiters` reflects the current pit menu fuel amount
- [ ] Works correctly when not on pit road (sensible defaults)

**Dependencies:** None.

---

### TASK-803 · Weather data extraction

**Status:** `[ ]`

Extract current weather conditions for the Weather overlay. Current conditions only — no forecast complexity for Alpha.

**What to build:**
- New DTO in `Sim.Contracts`:
  ```csharp
  public sealed record WeatherData(
      float AirTempC,
      float TrackTempC,
      float WindSpeedMps,
      float WindDirectionDeg,   // 0=N, 90=E, 180=S, 270=W
      float Humidity,           // 0.0–1.0
      int SkyCoverage,          // 0=clear, 1=mostly clear, ... (iRacing enum)
      float TrackWetness,       // 0.0–1.0 (0=dry, 1=flooded)
      bool IsPrecipitating
  );
  ```
- `IRacingPoller`: extract from iRacing weather telemetry vars (`AirTemp`, `TrackTempCrew`, `WindVel`, `WindDir`, `RelativeHumidity`, `Skies`, `TrackWetness`, `WeatherType`)
- Publish `WeatherData` at 1 Hz (or on change)
- No forecast DTO — keep it simple. If iRacing exposes trivially accessible forecast vars, include them as optional fields in a future iteration.

**Acceptance criteria:**
- [ ] `WeatherData` published at ≤1 Hz
- [ ] Wind direction is in degrees (0–360)
- [ ] `TrackWetness` reflects the iRacing `TrackWetness` telemetry var
- [ ] `Humidity` correctly normalized to 0.0–1.0
- [ ] Existing `SessionData.AirTempC`/`TrackTempC` remain as-is (duplicated in WeatherData for Weather overlay convenience)

**Dependencies:** None.

---

### TASK-804 · Track position data for Flat Map

**Status:** `[ ]`

Extract per-car track position data for rendering a flat (linear) track map.

**What to build:**
- New DTO in `Sim.Contracts`:
  ```csharp
  public sealed record TrackMapData(
      float TrackLengthMeters,
      IReadOnlyList<TrackMapCarEntry> Cars
  );

  public sealed record TrackMapCarEntry(
      int CarIndex,
      string CarNumber,
      int Position,             // overall race position
      float LapDistPct,         // 0.0–1.0 position around track
      string CarClass,
      bool IsPlayer,
      bool IsInPit
  );
  ```
- `IRacingPoller`: extract `CarIdxLapDistPct` array + active car indices
- Include `CarNumber` and `Position` so the flat map can label car dots
- Publish `TrackMapData` at 10 Hz (same cadence as RelativeData)

**Acceptance criteria:**
- [ ] `TrackMapData` published at 10 Hz
- [ ] All on-track cars included (spectators and pace cars excluded)
- [ ] `LapDistPct` is 0.0–1.0 for each car
- [ ] `IsInPit` correctly reflects cars on pit road
- [ ] `CarNumber` and `Position` populated per car
- [ ] `TrackLengthMeters` matches the iRacing session data

**Dependencies:** TASK-705 (CarClass field).

---
