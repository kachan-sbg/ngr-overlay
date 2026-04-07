# Alpha Decision Log (Phases 7-12)

Full decision entries from the Alpha milestone. For the brief summary, see [DECISIONS.md](../DECISIONS.md).

---

## 2026-04-06 — Alpha roadmap: OBS Mode toggle, LMU as second sim, flat track map

**Decision:** The Alpha milestone (Phases 7-12) will:
1. Keep the single-window OBS Mode toggle (renamed from "Stream Mode") — no dual-window or web source approach.
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
- New project `SimOverlay.Sim.LMU` with rFactor 2 shared memory reader.
- All overlays must render gracefully when data fields are missing.

---

## 2026-04-06 — Config versioning with sequential migration pipeline

**Decision:** Add an `int Version` field to `AppConfig` and a `ConfigMigrator` class that runs sequential migrations (v1->v2, v2->v3, etc.) on every config load.

**Context:** Alpha will add new config fields across multiple phases (SimPriorityOrder, class colors, new overlay defaults). Existing MVP configs on disk won't have these fields — we need a reliable way to populate defaults without losing user settings.

**Rationale:**
- Sequential numbered migrations are simple, predictable, and easy to test — each migration is a pure function that mutates `AppConfig`.
- Running migrations on every load (including fresh/default configs) means `ConfigStore` always returns a config at `CurrentVersion`.
- `Version=0` (absent field in old JSON) is treated as v1 so pre-versioning MVP configs migrate correctly.

**Alternatives considered:**
- **No versioning, rely on C# default values:** Works for adding fields but can't handle renames, removals, or type changes. Rejected — too fragile for a multi-phase Alpha.
- **JSON patching / JObject manipulation:** More flexible for schema changes but adds Newtonsoft/JObject dependency and makes migrations harder to test with typed objects. Rejected.

**Consequences:**
- Every new config shape change requires bumping `ConfigMigrator.CurrentVersion` and adding a migration method.
- Later tasks (TASK-704 SimPriorityOrder, TASK-705 class colors) will add real migration logic to the v1->v2 or v2->v3 methods.

---

## 2026-04-06 — JSON round-trip for OverlayConfig deep clone

**Decision:** Use `JsonSerializer.Serialize` → `JsonSerializer.Deserialize` for `OverlayConfig.DeepClone()`, replacing the manual field-by-field `CopyConfig` in `OverlayManager`. Also fix `Resolve()` to set `StreamOverride = null` on the resolved copy (ISSUE-009).

**Context:** `OverlayManager.CopyConfig()` manually copied 24 fields but shallow-copied reference types (`ColorConfig`, `StreamOverrideConfig`). This shared mutable state between Settings ViewModel and live config. `Resolve()` also preserved a shared `StreamOverride` reference on the resolved snapshot.

**Rationale:**
- JSON round-trip covers all fields automatically — no maintenance burden as fields are added.
- `System.Text.Json` is already a dependency (used by `ConfigStore`).
- `Resolve()` returns a snapshot for rendering; it should never carry a `StreamOverride` reference since it's already the "final" effective config.

**Alternatives considered:**
- **MemberwiseClone + explicit new for reference types:** Faster, but requires manually listing every reference-type field — same maintenance problem as the old `CopyConfig`.
- **Source-generated clone:** Avoids reflection overhead but adds build complexity for a non-hot-path operation.

**Consequences:**
- `ApplyConfig` now replaces the `OverlayConfig` reference in `AppConfig.Overlays` (and updates the overlay) rather than mutating the existing reference in place.
- Slight allocation overhead from serialization, acceptable since `ApplyConfig` is user-triggered (not per-frame).

---

## 2026-04-07 — Upgrade IRSDKSharper 1.0.3→1.1.6; deterministic Win32 handle release on disconnect

**Decision:** Upgrade `IRSDKSharper` from 1.0.3 to 1.1.6 and ensure `IRacingProvider.Stop()` calls `Dispose()` on the SDK instance, releasing all Win32 handles deterministically.

**Context:** After a sim session ended and iRacing was restarted, the overlay remained in a "pending/connecting" state and never recovered. The underlying cause was that IRSDKSharper 1.0.3 held a Win32 memory-mapped file handle open after `Stop()` was called, preventing the SDK from cleanly re-initialising on the next connection attempt.

**Rationale:**
- IRSDKSharper 1.1.6 fixes the internal handle release on `Stop()`/`Dispose()` — upgrading is the minimal correct fix.
- Deterministic handle release via `Dispose()` is the right pattern regardless; do not rely on GC finalizers for Win32 resources.
- Resource lifecycle correctness is a first-class constraint: handle leaks cause in-race crashes and are treated with the same priority as rendering performance.

**Alternatives considered:**
- **Workaround in `IRacingProvider` (recreate SDK object on reconnect):** Possible but layering a workaround on top of a library bug adds complexity and masks the root cause. Rejected in favour of upgrading.

**Consequences:**
- `IRacingProvider.Stop()` must call `Dispose()` (not just `Stop()`) on the IRSDKSharper instance.
- Disconnect→reconnect cycle must be tested manually for any future change to `IRacingProvider` or `IRacingPoller` (documented in ARCHITECTURE.md §14).
- Resource lifecycle rules (event handler unsubscription, native handle ownership) formally documented in ARCHITECTURE.md §14.

---

## 2026-04-07 — SimDetector provider list sorted by `SimPriorityOrder` config (TASK-905)

**Decision:** `Program.cs` sorts the registered `ISimProvider` list by `GlobalSettings.SimPriorityOrder` at startup rather than using a fixed code order. Config migration v3→v4 appends "LMU" to `SimPriorityOrder` in existing configs. `SimDetector.Poll()` is made `internal` with `InternalsVisibleTo` so unit tests can drive it synchronously.

**Context:** TASK-905 adds LMU to the active sim pipeline. The provider priority order must be config-driven so users can prefer LMU over iRacing if they choose. Existing configs only have "iRacing" in the list.

**Rationale:**
- Sorting providers by `SimPriorityOrder` at container build time is cheap (one LINQ sort at startup) and means `SimDetector` itself stays agnostic to ordering — it simply iterates in list order.
- The migration appends rather than replaces, preserving any custom user ordering.
- Making `Poll()` internal (not public) keeps the seam minimal while enabling deterministic unit tests without timing races.

**Alternatives considered:**
- **Hard-code iRacing first, LMU second:** Simpler but doesn't honour the config. Rejected — the config field exists precisely for this purpose.
- **Config-driven ordering inside SimDetector:** SimDetector would need a dependency on `AppConfig` breaking its clean single-responsibility. Rejected — sorting at composition root is cleaner.
- **Test via timer with short interval:** Would require Thread.Sleep in tests — flaky. Rejected in favour of internal test seam.

**Consequences:**
- Adding a new sim provider requires: (a) new csproj, (b) DI registration in Program.cs, (c) entry in the provider list lambda, (d) a config migration to append the new SimId to `SimPriorityOrder`.
- `SimDetector` unit tests live in `SimOverlay.App.Tests` and cover: priority ordering, debounce (1 strike = no stop, 2 strikes = stop), no-overlap guarantee, and provider transitions.
