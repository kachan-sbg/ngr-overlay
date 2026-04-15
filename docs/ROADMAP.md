# NrgOverlay вЂ” Roadmap

> Last updated: 2026-04-12

---

## Current state вЂ” v0.0.1 Alpha

NrgOverlay is a working alpha.  All core machinery is in place and usable in real sessions.

### What works

| Area | Status |
|---|---|
| Transparent, always-on-top overlays (Direct2D + UpdateLayeredWindow) | Working |
| OBS Window Capture with "Allow Transparency" | Working |
| iRacing integration (full telemetry via IRSDKSharper) | Working |
| LMU (Le Mans Ultimate) integration (native LMU_Data shared memory) | Working |
| Automatic sim detection вЂ” starts/stops providers as sims appear | Working |
| Settings UI вЂ” per-overlay position, size, colors, fonts | Working |
| Stream Override вЂ” separate OBS profile per overlay | Working |
| Config persistence (`%APPDATA%\NrgOverlay\config.json`, atomic, versioned) | Working |

### Overlays shipping in v0.0.1

| Overlay | Description |
|---|---|
| Relative | Nearby drivers with gap, class, iRating, license |
| Session Info | Track, session type, weather, time of day |
| Delta Bar | Lap-delta visualization vs best lap |
| Input Telemetry | Throttle/brake/clutch bars, gear+speed, scrolling trace |
| Fuel Calculator | Fuel level, avg/lap, laps remaining, pit add |
| Pit Helper | Pit road state, limiter indicator, service flags |
| Standings | Full-field leaderboard with Combined and ClassGrouped modes |
| Weather | Air/track temp, wind, humidity, precipitation |
| Flat Track Map | Car positions on a simplified oval/road map |

### Known gaps in v0.0.1

- Data not validated end-to-end against reference sources вЂ” some fields may display
  wrong values or show data when the sim has no valid value for them.
- No cross-sim normalization for rating/safety fields (LMU shows "вЂ”" where iRacing
  shows an iRating and licence class).
- Stream Override not fully wired for all six Alpha overlays.
- No field visibility control or column reordering in overlay UI.
- Single sim at a time; no ACC, AMS2, or other sims.

---

## Roadmap

Phases are listed in **priority order** as agreed.  Phase 12 (OBS Mode) is the legacy
label from the alpha plan; the actual next-priority phases are 13 onward.

---

### Phase 13 вЂ” Data Validation & Correctness Audit в¬… NEXT

**Goal:** Ship data users can trust.  Every field in every overlay should either display
a correct value or be hidden/greyed when the sim has no valid data for it.

**Scope:**
- Cross-reference every field against the iRacing SDK telemetry variable list and the
  LMU SDK header, confirming units, range, and availability per session type.
- Fields that are unavailable in a given sim show "вЂ”" or are hidden, never show `0` or
  garbage.
- LMU-specific: validate struct offsets with LmuDiag against a live session, confirm
  speed, gear, fuel, pit state, delta, and relative gaps are all correct.
- iRacing-specific: confirm delta sign (positive = ahead of best), fuel units (litres vs
  gallons), tyre temps, and incident count vs limit.
- Regression test: extend existing unit tests to cover field-level assertions using
  recorded session snapshots where possible.

**Why first:** Other simracers can't use the app in alpha mode if they see wrong numbers.
This phase is the gate to sharing the app publicly.

---

### Phase 14 вЂ” WebSocket Server

**Goal:** Let anyone build a custom widget using HTML/CSS/JS without touching C#.

**Scope:**
- Embedded HTTP/WebSocket server (ASP.NET Core Kestrel or a lightweight alternative)
  that serves live telemetry as JSON over WebSocket.
- All DTO types (`TelemetryData`, `SessionData`, `RelativeData`, etc.) serialised to JSON
  and pushed to connected clients at their natural rates.
- A minimal example HTML page bundled in the repo demonstrating a working widget.
- Config option to enable/disable the server and set the port.

**Why second:** Gives an open customisation surface without requiring contributors to know
Direct2D or C#.  Unlocks a whole ecosystem of community widgets.

---

### Phase 15 вЂ” Field Visibility & Layout Config

**Goal:** Users can show, hide, and reorder fields within each overlay.

**Scope:**
- Per-overlay config: `columnOrder: string[]` listing field IDs in display order.
  Fields absent from the list are hidden.
- Settings UI: checklist + up/down arrows per overlay.
- Overlays auto-adapt column widths when fields are hidden.
- Remove the second "alternate layout" tab from all overlay settings panels (or
  keep the code but comment it out from the UI until the feature is properly defined).
  The current dual-tab approach adds UI noise without a clear UX contract.

---

### Phase 16 вЂ” Cross-sim Data Normalization

**Goal:** Fields that represent the same real-world concept across sims show a consistent
value вЂ” never just "вЂ”" when equivalent data exists under a different name.

**Examples:**
- **iRating equivalent:** iRacing has `IRating`; LMU has a driver rating; ACC has a
  rating too.  All should display in a `Rating` column with a sim-appropriate label.
- **Safety rating / licence equivalent:** iRacing has A/B/C/D/R license; LMU and ACC
  have similar tiering.  Map to a common `LicenseClass` with per-sim suffixes if needed.
- **Session types:** map each sim's session-type vocabulary to the shared `SessionType`
  enum so overlays don't need per-sim logic.

---

### Phase 17 вЂ” Radar Overlay

**Goal:** Classic racing radar showing nearby cars as a top-down proximity indicator.

**Scope:**
- Circular display, player car at centre.
- Nearby cars projected onto the radar using heading + distance.
- Configurable range and scale.
- Class colour coded blips.

**Note:** Requires accurate heading and relative bearing data вЂ” verify availability in
both sims before planning implementation details.

---

### Phase 18 вЂ” Map Overlay

**Goal:** Show car positions on a track map.

**Approach (two-stage):**
1. **Bundled SVG maps** for the most popular circuits in iRacing and LMU.  SVG gives
   crisp scaling and can be rendered as a Direct2D geometry.
2. **Auto-trace:** after the user completes a valid out-lap, the app builds a simplified
   spline from `LapDistPct` + heading samples and exports it as an SVG path.  Community
   can submit traced maps back to the repo.

---

### Phase 19 вЂ” ACC Integration

**Goal:** Add Assetto Corsa Competizione as a third supported sim.

**Approach:** ACC exposes a shared memory API similar to LMU/rF2.  A new
`NrgOverlay.Sim.ACC` project follows the same pattern as `NrgOverlay.Sim.LMU`.

**Notes:**
- ACC has its own rating system (SA вЂ” Safety Rating, PC вЂ” Personal Rating).
  Map to the normalized rating fields from Phase 16.
- ACC supports multiclass (GT3 + GT4 in Balance of Performance), so Relative and
  Standings overlays should already work via the existing class colour system.

---

### Phase 20 вЂ” Session-State-Aware Visibility

**Goal:** Overlays appear/disappear automatically based on what's happening in the sim.

**Scope:**
- Define a common `SessionPhase` abstraction: `Idle`, `Garage`, `Outlap`, `Race`,
  `Caution`, `Replay`, etc.  Map each sim's own phase vocabulary to this.
- Per-overlay config: `visibleInPhases: string[]`.
- Overlays that have nothing useful to show (e.g. Delta Bar in the garage) auto-hide
  rather than showing zero/garbage data.

---

### Phase 12 вЂ” OBS Mode & Enhanced UX (deferred)

The alpha stream-override system already covers the primary use case (toggle between
driver view and OBS view).  This phase вЂ” renaming "Stream Mode" to "OBS Mode", adding
a hotkey, session-type profiles, buddy highlighting, positions-gained indicator, and
multi-class colour configuration вЂ” is fully specced in
[`docs/tasks/PHASE-12-obs-and-ux.md`](tasks/PHASE-12-obs-and-ux.md) and will be
scheduled when priorities allow.

### Reliability (Current)

Long-session crash/hang/data-integrity risks are tracked in
[`docs/CRITICAL-ISSUES.md`](CRITICAL-ISSUES.md).

### Planning Updates (2026-04-14)

- Cross-sim normalization now has a dedicated task spec:
  [`docs/tasks/PHASE-16-cross-sim-normalization.md`](tasks/PHASE-16-cross-sim-normalization.md).
- Rating display format target (configurable template) includes examples:
  iRacing `[A2.2] 2345`, LMU `[R2] S2`, default template `[{Safety}] {Primary}`.
- New mid-priority performance phase added:
  [`docs/tasks/PHASE-21-performance-and-soak.md`](tasks/PHASE-21-performance-and-soak.md).

