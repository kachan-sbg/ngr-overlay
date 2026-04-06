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
