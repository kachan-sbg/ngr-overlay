# Alpha Decision Log (Phases 7-12)

Full decision entries from the Alpha milestone. For the brief summary, see [DECISIONS.md](../DECISIONS.md).

---

## 2026-04-12 вЂ” Proper shutdown via MessagePump.Quit() instead of Environment.Exit()

**Decision:** Replace `Environment.Exit(0)` in quit callbacks (F10 hotkey and tray icon) with `MessagePump.Quit()`, letting the message pump exit cleanly and the `using var provider` block unwind.

**Context:** After NrgOverlay was closed, iOverlay (a competing SDK client) would stop working until iRacing was restarted. Root cause: `Environment.Exit(0)` does not unwind the C# call stack, so `using var provider = services.BuildServiceProvider()` never disposed. `SimDetector`, `IRacingProvider`, `IRacingPoller` were never stopped. IRSDKSharper's background thread and Win32 handles were alive until OS cleanup вЂ” and crucially, IRSDKSharper's hidden HWND was still receiving iRacing broadcasts; with no message pump running, iRacing's `SendMessage(HWND_BROADCAST)` would block on that dead window for the hung-window timeout (~5 s), delaying SDK notifications to iOverlay.

**Rationale:** `MessagePump.Quit()` posts `WM_QUIT`, which exits `GetMessage` on the next iteration. The pump returns, the try block exits, `using var provider` disposes, `ServiceProvider.Dispose()` calls `IDisposable.Dispose()` on all registered singletons in LIFO order: `SimDetector` в†’ `IRacingProvider` в†’ `IRacingPoller` в†’ IRSDKSharper `Stop()` + `GC.Collect() + GC.WaitForPendingFinalizers()`. All Win32 handles are released deterministically before the process exits.

**Also added:** `IDisposable` on `IRacingProvider` and `LmuProvider` (DI fallback), safe `Timer.Dispose(WaitHandle)` in `LmuPoller` to wait for in-flight callbacks before releasing the `MemoryMappedViewAccessor`.

**Consequences:** Normal shutdown now takes slightly longer (~3 s max вЂ” the `_stoppedGate.Wait(3s)` in `IRacingPoller.Dispose()`). Process kills via Task Manager still skip cleanup, but that is unavoidable and Windows closes all handles anyway.

---

## 2026-04-12 вЂ” CarIdxTrackSurface filter for relative/standings/track map

**Decision:** Use `CarIdxTrackSurface >= 0` as the primary in-world filter for iRacing car slots, replacing the `LapDistPct < 0` check.

**Context:** iRacing allocates 64 car slots in telemetry arrays. Registered-but-not-connected drivers (garage or not yet spawned) have `LapDistPct == 0.0` (not -1), so `pct < 0f` passes them through. These ghost entries appeared in the relative and standings lists with position 0 and no driver name, and caused the track map to show zero cars (the filter was correct, but 0.0 is a plausible position on track, so the logic was silently including/excluding the wrong cars).

**Rationale:** `CarIdxTrackSurface == -1` (irsdk value `irsdk_NotInWorld`) is the authoritative "slot unused" signal. All other values (0=OffTrack, 1=InPitStall, 2=ApproachingPits, 3=OnTrack) mean the car is physically spawned. Added to `TelemetrySnapshot` so the stateless calculator can apply the filter without SDK dependency.

**Consequences:** `TelemetrySnapshot` gained a `TrackSurfaces` array; test and benchmark helpers updated.

---

## 2026-04-12 вЂ” Smoothed session timing with local countdown

**Decision:** `SessionInfoOverlay` maintains a local `(_syncedElapsed, _syncedRemaining, _syncWallClock)` reference that is updated from the SDK only when `SessionTimeElapsed > 0`. Between syncs, the display counts forward/backward using wall-clock delta.

**Context:** The SDK occasionally returns 0 for `SessionTime` / `SessionTimeRemain` between valid samples (likely a read-torn frame or SDK internal state). Since `DriverData` is published at 60 Hz, this caused the session elapsed and remaining displays to blink between valid values and `"--:--:--"` every few seconds.

**Rationale:** Simple monotonic-ish tracking: accept the SDK value if it is non-zero and not more than 5 s behind the current local estimate (tolerates brief backward jumps but rejects 0-blips). Between SDK syncs, `DateTime.UtcNow - _syncWallClock` is added to the last accepted value. This gives a smooth, stable display with effectively no computational cost and no additional dependencies.

**Consequences:** Session elapsed no longer reflects the raw SDK value in real time вЂ” it may drift by up to ~60 ms per second between syncs. At 60 Hz syncs this is negligible. Reset on `SessionData` change to handle new sessions correctly.

---

## 2026-04-06 вЂ” Alpha roadmap: OBS Mode toggle, LMU as second sim, flat track map

**Decision:** The Alpha milestone (Phases 7-12) will:
1. Keep the single-window OBS Mode toggle (renamed from "Stream Mode") вЂ” no dual-window or web source approach.
2. Add LMU (Le Mans Ultimate) as a second sim provider in a dedicated phase before new overlays, to surface multi-sim DTO gaps early.
3. Use a flat/linear track map (horizontal bar) instead of a 2D overhead map.
4. Implement a read-only fuel calculator (estimation only, no writing to the sim's pit menu).
5. Show current weather conditions only (no forecast section).

**Context:** Post-MVP planning session. The stream override system works but can't display different views to the driver and OBS simultaneously. New overlays need to support multiple sims with missing data fields.

**Rationale:**
- **OBS Mode toggle:** 90% of the value for 10% of the complexity. Most competing apps (SimHub, iOverlay) use the same approach. True simultaneous dual-view (web source or dual windows) is a post-Alpha consideration.
- **LMU before overlays:** LMU uses rFactor 2 shared memory with a very different data model (no iRating, no license class, no incidents, absolute distance instead of percentage). Building overlays after LMU integration ensures they handle missing data gracefully from day one.
- **Flat track map:** Simpler to implement, no track shape database needed, still shows relative car positions clearly.
- **Read-only fuel:** Lower risk, no sim interaction bugs. Users can read the "PIT ADD" value and enter it manually.
- **Weather:** iRacing forecast data availability is inconsistent across session types. Current conditions are reliably available.

**Alternatives considered:**
- **Dual windows for OBS:** Creates two HWNDs per overlay type. Achieves true simultaneous different views but doubles window management complexity and requires multi-monitor setup. Deferred.
- **Web source for OBS:** HTTP/WebSocket server + HTML/CSS/JS renderer as OBS Browser Source. Cleanest separation but requires a parallel rendering stack. Deferred.
- **2D overhead track map:** More visually appealing but requires bundled track coordinates or telemetry calibration lap. Deferred to post-Alpha or as a TASK-1103 follow-up.
- **Fuel auto-set:** Writing `dpFuelFill` via iRacing broadcast messages. Adds sim interaction risk. Deferred.

**Consequences:**
- `StreamOverrideConfig` -> rename fields/UI to "OBS Profile" in Phase 12.
- `Sim.Contracts` DTOs must define sentinel values for unavailable data (e.g., iRating = 0, LicenseClass.Unknown).
- New project `NrgOverlay.Sim.LMU` with rFactor 2 shared memory reader.
- All overlays must render gracefully when data fields are missing.

---

## 2026-04-06 вЂ” Config versioning with sequential migration pipeline

**Decision:** Add an `int Version` field to `AppConfig` and a `ConfigMigrator` class that runs sequential migrations (v1->v2, v2->v3, etc.) on every config load.

**Context:** Alpha will add new config fields across multiple phases (SimPriorityOrder, class colors, new overlay defaults). Existing MVP configs on disk won't have these fields вЂ” we need a reliable way to populate defaults without losing user settings.

**Rationale:**
- Sequential numbered migrations are simple, predictable, and easy to test вЂ” each migration is a pure function that mutates `AppConfig`.
- Running migrations on every load (including fresh/default configs) means `ConfigStore` always returns a config at `CurrentVersion`.
- `Version=0` (absent field in old JSON) is treated as v1 so pre-versioning MVP configs migrate correctly.

**Alternatives considered:**
- **No versioning, rely on C# default values:** Works for adding fields but can't handle renames, removals, or type changes. Rejected вЂ” too fragile for a multi-phase Alpha.
- **JSON patching / JObject manipulation:** More flexible for schema changes but adds Newtonsoft/JObject dependency and makes migrations harder to test with typed objects. Rejected.

**Consequences:**
- Every new config shape change requires bumping `ConfigMigrator.CurrentVersion` and adding a migration method.
- Later tasks (TASK-704 SimPriorityOrder, TASK-705 class colors) will add real migration logic to the v1->v2 or v2->v3 methods.

---

## 2026-04-06 вЂ” JSON round-trip for OverlayConfig deep clone

**Decision:** Use `JsonSerializer.Serialize` в†’ `JsonSerializer.Deserialize` for `OverlayConfig.DeepClone()`, replacing the manual field-by-field `CopyConfig` in `OverlayManager`. Also fix `Resolve()` to set `StreamOverride = null` on the resolved copy (ISSUE-009).

**Context:** `OverlayManager.CopyConfig()` manually copied 24 fields but shallow-copied reference types (`ColorConfig`, `StreamOverrideConfig`). This shared mutable state between Settings ViewModel and live config. `Resolve()` also preserved a shared `StreamOverride` reference on the resolved snapshot.

**Rationale:**
- JSON round-trip covers all fields automatically вЂ” no maintenance burden as fields are added.
- `System.Text.Json` is already a dependency (used by `ConfigStore`).
- `Resolve()` returns a snapshot for rendering; it should never carry a `StreamOverride` reference since it's already the "final" effective config.

**Alternatives considered:**
- **MemberwiseClone + explicit new for reference types:** Faster, but requires manually listing every reference-type field вЂ” same maintenance problem as the old `CopyConfig`.
- **Source-generated clone:** Avoids reflection overhead but adds build complexity for a non-hot-path operation.

**Consequences:**
- `ApplyConfig` now replaces the `OverlayConfig` reference in `AppConfig.Overlays` (and updates the overlay) rather than mutating the existing reference in place.
- Slight allocation overhead from serialization, acceptable since `ApplyConfig` is user-triggered (not per-frame).

---

## 2026-04-07 вЂ” Upgrade IRSDKSharper 1.0.3в†’1.1.6; deterministic Win32 handle release on disconnect

**Decision:** Upgrade `IRSDKSharper` from 1.0.3 to 1.1.6 and ensure `IRacingProvider.Stop()` calls `Dispose()` on the SDK instance, releasing all Win32 handles deterministically.

**Context:** After a sim session ended and iRacing was restarted, the overlay remained in a "pending/connecting" state and never recovered. The underlying cause was that IRSDKSharper 1.0.3 held a Win32 memory-mapped file handle open after `Stop()` was called, preventing the SDK from cleanly re-initialising on the next connection attempt.

**Rationale:**
- IRSDKSharper 1.1.6 fixes the internal handle release on `Stop()`/`Dispose()` вЂ” upgrading is the minimal correct fix.
- Deterministic handle release via `Dispose()` is the right pattern regardless; do not rely on GC finalizers for Win32 resources.
- Resource lifecycle correctness is a first-class constraint: handle leaks cause in-race crashes and are treated with the same priority as rendering performance.

**Alternatives considered:**
- **Workaround in `IRacingProvider` (recreate SDK object on reconnect):** Possible but layering a workaround on top of a library bug adds complexity and masks the root cause. Rejected in favour of upgrading.

**Consequences:**
- `IRacingProvider.Stop()` must call `Dispose()` (not just `Stop()`) on the IRSDKSharper instance.
- Disconnectв†’reconnect cycle must be tested manually for any future change to `IRacingProvider` or `IRacingPoller` (documented in ARCHITECTURE.md В§14).
- Resource lifecycle rules (event handler unsubscription, native handle ownership) formally documented in ARCHITECTURE.md В§14.

---

## 2026-04-07 вЂ” SimDetector provider list sorted by `SimPriorityOrder` config (TASK-905)

**Decision:** `Program.cs` sorts the registered `ISimProvider` list by `GlobalSettings.SimPriorityOrder` at startup rather than using a fixed code order. Config migration v3в†’v4 appends "LMU" to `SimPriorityOrder` in existing configs. `SimDetector.Poll()` is made `internal` with `InternalsVisibleTo` so unit tests can drive it synchronously.

**Context:** TASK-905 adds LMU to the active sim pipeline. The provider priority order must be config-driven so users can prefer LMU over iRacing if they choose. Existing configs only have "iRacing" in the list.

**Rationale:**
- Sorting providers by `SimPriorityOrder` at container build time is cheap (one LINQ sort at startup) and means `SimDetector` itself stays agnostic to ordering вЂ” it simply iterates in list order.
- The migration appends rather than replaces, preserving any custom user ordering.
- Making `Poll()` internal (not public) keeps the seam minimal while enabling deterministic unit tests without timing races.

**Alternatives considered:**
- **Hard-code iRacing first, LMU second:** Simpler but doesn't honour the config. Rejected вЂ” the config field exists precisely for this purpose.
- **Config-driven ordering inside SimDetector:** SimDetector would need a dependency on `AppConfig` breaking its clean single-responsibility. Rejected вЂ” sorting at composition root is cleaner.
- **Test via timer with short interval:** Would require Thread.Sleep in tests вЂ” flaky. Rejected in favour of internal test seam.

**Consequences:**
- Adding a new sim provider requires: (a) new csproj, (b) DI registration in Program.cs, (c) entry in the provider list lambda, (d) a config migration to append the new SimId to `SimPriorityOrder`.
- `SimDetector` unit tests live in `NrgOverlay.App.Tests` and cover: priority ordering, debounce (1 strike = no stop, 2 strikes = stop), no-overlap guarantee, and provider transitions.

---

## 2026-04-14 вЂ” EMA smoothing on gap/interval values; IRacingRelativeCalculator made stateful

**Decision:** Convert `IRacingRelativeCalculator` from a `static` pure function to an instance class (`sealed class`) that holds per-car `EmaFilter[64]` arrays for `GapToPlayerSeconds` (relative) and `GapToLeaderSeconds` (standings). Smoothing constants live in `EmaConstants` in `NrgOverlay.Core` (`GapAlpha = 0.15f`, `IntervalAlpha = 0.15f`). `IRacingPoller` holds the single calculator instance and calls `Reset()` on connect and disconnect.

**Context:** Gap and interval values computed from `(pct - playerPct) Г— estLapTime` are inherently noisy at 10 Hz вЂ” small fluctuations in lap-distance percentage and estimated lap time cause visible jitter in the relative and standings overlays. Values need smoothing without adding perceptible lag.

**Rationale:**
- Exponential moving average is zero-allocation when held as a struct field and requires no history buffer. О±=0.15 gives a time constant of ~0.67 s at 10 Hz вЂ” smooth enough to eliminate jitter, responsive enough for race events (overtakes, pit entries).
- Converting to an instance class is the minimal change that enables stateful filtering without mutating shared module-level state. The calculator remains fully testable: tests instantiate `new IRacingRelativeCalculator()` per test case; first-call EMA pass-through ensures existing gap-equality assertions still hold.
- Garage sentinel gaps (99 999 f) bypass the filter so filter state is never poisoned by off-track placeholder values.
- `Interval` inherits smoothing automatically since it is derived from the already-smoothed `gapToLeader` value.
- Constants are in one findable place with a comment noting the intended config range (0.05вЂ“0.40), ready for a future settings hook.

**Alternatives considered:**
- **Simple rolling average (N samples):** Requires a circular buffer per car вЂ” more allocation, no benefit over EMA.
- **Keep static, pass filter arrays as parameters:** Caller (poller) must own the arrays, making the API awkward and the relationship harder to follow.

**Consequences:**
- `IRacingRelativeCalculator` is no longer callable as a static method. Tests and benchmark updated to instantiate the class.
- Benchmark result (40 cars, ShortRun): **18.1 Вµs** mean вЂ” well within the 50 Вµs budget. EMA filter updates add negligible overhead.
- Alpha smoothing constants will move to `AppConfig` / settings UI in a future phase. Range should be restricted (e.g. 0.05вЂ“0.40) to prevent values that feel either laggy or still jittery.

---

## 2026-04-14 вЂ” Real-time race positions via laps + lapDistPct ranking

**Decision:** In race sessions, `IRacingRelativeCalculator` computes real-time positions by ranking all on-track cars by `laps[i] + max(0, lapDistPct[i])` descending before either display pass. Both `RelativeEntry.Position` and `StandingsEntry.Position` use this ranking. `CarIdxPosition` from the SDK is still used to detect that a race session is active (any value > 0), but no longer used as the display position.

**Context:** iRacing's `CarIdxPosition` field only updates when a car crosses the start/finish line. Mid-race it reflects grid order for the first lap and only updates to reflect an overtake after the cars next cross the line. The relative and standings overlays showed stale positions that could be 60+ seconds behind reality during long stints.

**Rationale:**
- `snapshot.Laps[i]` (completed laps) + `snapshot.LapDistPcts[i]` (current progress 0вЂ“1) is the natural measure of how far a car has travelled in the race. Ranking descending gives an instantaneous position that updates within one telemetry frame (~100 ms at 10 Hz) of an overtake.
- The computation happens once per `Compute()` call and the results are shared across both the relative pass and the standings pass вЂ” no duplication.
- Practice and qualify are unaffected: their `CarIdxPosition` values are all 0, so the rt-position path is not entered.

**Alternatives considered:**
- **Use `CarIdxF2Time` (iRacing's internal "time behind leader"):** Available and accurate, but expressed in time not position вЂ” requires a sort anyway, and doesn't map cleanly to an integer rank.
- **Per-overlay re-computation:** Each overlay could independently sort by progress. Rejected вЂ” duplicate computation with potential consistency divergence between relative and standings.

**Consequences:**
- Position can change by more than one place in a single update frame (e.g. after a pit stop sequence), which is correct вЂ” the SDK's finish-line position could not do this at all.
- `PositionsGained` (standings) is now computed against the rt position rather than the SDK position, which makes it more accurate.
- In the unlikely case of a car reversing on track (negative pct motion), the progress score decreases naturally and the car moves down the order вЂ” correct behaviour.

---

## 2026-04-14 вЂ” Flag emoji for driver country via Unicode regional indicators

**Decision:** `RelativeOverlay.ClubToCode()` (shared with `StandingsOverlay`) maps iRacing club names to ISO 3166-1 alpha-2 codes as before, then converts each code to a Unicode flag emoji by combining two regional indicator symbols (U+1F1E6 + offset for each letter). Unknown clubs fall back to the 2-letter club name truncation. No new DTO fields were added вЂ” `ClubName` (already populated from iRacing session YAML) feeds the mapping.

**Context:** iRacing's SDK exposes `ClubName` per driver (e.g. "Germany", "USA - Southeast", "Great Britain"). The field was already in `RelativeEntry` and `StandingsEntry` and already being rendered as a 2-letter ISO code. Users expect to see a flag in the country column, consistent with competing overlays.

**Rationale:**
- Unicode regional indicator symbols (рџ‡¦вЂ“рџ‡ї, U+1F1E6вЂ“U+1F1FF) form flag emoji when two are placed in sequence. Every ISO 3166-1 alpha-2 code has a corresponding flag this way. The conversion is a deterministic 2-character в†’ 4-UTF16-unit string with no lookup table required.
- The existing `ClubToCode` ISO mapping was already correct and comprehensive; only the output step changes.
- Rendering depends on the IDWriteTextFormat font stack including Segoe UI Emoji. Direct2D does not composite emoji fonts automatically; if the current monospaced format does not fall back to an emoji font, the result will show surrogate-pair boxes and the user may want to revert to ISO codes.

**Alternatives considered:**
- **Store ISO code in DTO, convert to emoji in overlay:** Cleaner separation, but the ISO code was never in the DTO вЂ” it was always derived in the overlay. The mapping already lives in the overlay; adding it to the DTO would need a new field and a migration.
- **Separate mapping file / resource:** Unnecessary for ~40 club entries.

**Consequences:**
- If Segoe UI Emoji is not in the font fallback chain for the overlay text format, emoji will not render. The fix is to use `IDWriteFontFallback` or change the column to a separate text format that includes an emoji font. This is a rendering concern separate from the data mapping.
- iRacing's US regional clubs ("USA - Northeast", "USA - Southeast", etc.) all map to рџ‡єрџ‡ё. The `UK and I` club maps to рџ‡¬рџ‡§. These are the expected approximations given the club structure.

