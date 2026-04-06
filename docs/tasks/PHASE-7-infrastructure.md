# Phase 7 — Infrastructure Hardening

> **Goal:** Resolve MVP technical debt and build the foundation needed before adding new overlays.
> All blocking issues from [REVIEW-MVP.md](../REVIEW-MVP.md) are addressed here.

## Status: `[ ]` Not started

---

### TASK-701 · Config versioning and migration framework

**Status:** `[ ]`

Add `Version` field to `AppConfig` and a migration pipeline in `ConfigStore.Load()`.

**What to build:**
- Add `public int Version { get; set; } = 1;` to `AppConfig`
- In `ConfigStore.Load()`, after deserialization, check `config.Version`:
  - If less than current version, run migrations sequentially (v1→v2, v2→v3, etc.)
  - Each migration is a `static void Migrate(AppConfig config)` method
- Write a v1→v2 migration that populates default values for any new Alpha fields
- Log migration steps via `AppLog`

**Acceptance criteria:**
- [ ] Existing MVP config files load correctly (version defaults to 1)
- [ ] New config files are created with the current version number
- [ ] Missing fields from old configs get sensible defaults after migration
- [ ] Unit test: v1 config → load → migrated to v2 with correct defaults
- [ ] Unit test: current version config → no migration runs

**Dependencies:** None — do this first.

---

### TASK-702 · Replace manual config cloning with deep copy

**Status:** `[ ]`

Replace `OverlayManager.CopyConfig()` with a reliable deep-clone mechanism.

**What to build:**
- Implement deep clone via JSON round-trip: `JsonSerializer.Deserialize<OverlayConfig>(JsonSerializer.Serialize(source))`
- Or use `MemberwiseClone()` + explicit `new ColorConfig(...)` / `new StreamOverrideConfig(...)` for reference-type fields
- Remove the manual field-by-field `CopyConfig` method
- Also fix ISSUE-009: `OverlayConfig.Resolve()` should deep-clone `StreamOverride` (or exclude it from the resolved copy)

**Acceptance criteria:**
- [ ] `CopyConfig()` method removed
- [ ] All Settings preview/apply flows still work identically
- [ ] Unit test: clone produces independent object (mutating clone doesn't affect original)
- [ ] Unit test: all fields survive round-trip (including nested ColorConfig, StreamOverrideConfig)
- [ ] ISSUE-009 resolved — resolved config has no shared mutable references

**Dependencies:** None.

---

### TASK-703 · Wire up dependency injection container

**Status:** `[ ]`

Replace manual service construction in `Program.cs` with `Microsoft.Extensions.DependencyInjection`.

**What to build:**
- Create `IServiceCollection` → register all services (singletons: ConfigStore, SimDataBus, SimDetector, OverlayManager, etc.)
- Use `IServiceProvider` to resolve the composition root
- Introduce `IOverlayFactory` for creating overlay instances by type/ID
- Register overlay types in a dictionary so new overlays are added by registration, not code changes in Program.cs

**Acceptance criteria:**
- [ ] `Program.cs` uses `ServiceProvider` instead of manual `new` calls
- [ ] Adding a new overlay type requires only: (1) write the class, (2) register it
- [ ] All existing functionality unchanged
- [ ] `OverlayManager` receives overlay instances via constructor injection
- [ ] No service locator anti-pattern (no `provider.GetService<T>()` outside composition root)

**Dependencies:** None. Can be done in parallel with TASK-701/702.

---

### TASK-704 · Resolve remaining MVP known issues

**Status:** `[ ]`

Fix the 4 remaining low-priority issues from MVP.

**What to fix:**
- **ISSUE-011:** Add `SimPriorityOrder` to `GlobalSettings` (`List<string>`, default `["iRacing"]`)
- **ISSUE-012:** Replace `File.AppendAllText` in `AppLog` with a persistent `StreamWriter` (auto-flush, thread-safe)
- **ISSUE-013:** Change `IRacingPoller._cachedDrivers` from `volatile List<DriverSnapshot>` to `volatile ImmutableArray<DriverSnapshot>`
- **Fix** `TemperatureUnit` not applied: `SessionInfoOverlay` should convert C→F when `config.TemperatureUnit == Fahrenheit`

**Acceptance criteria:**
- [ ] `SimPriorityOrder` serializes/deserializes correctly in config
- [ ] `AppLog` keeps file handle open; rotates cleanly at 5 MB; disposes on shutdown
- [ ] `_cachedDrivers` is `ImmutableArray<DriverSnapshot>`
- [ ] Temperature displays in Fahrenheit when configured
- [ ] Unit test for C→F conversion

**Dependencies:** TASK-701 (config migration handles new `SimPriorityOrder` field).

---

### TASK-705 · Multi-class data model

**Status:** `[ ]`

Add car class support to the data contracts so overlays can distinguish classes.

**What to build:**
- Add to `RelativeEntry`: `string CarClass` (e.g., "GTP", "LMP2", "GT3"), `int ClassPosition`, `ColorConfig ClassColor`
- Add to `SessionData`: `IReadOnlyList<CarClassInfo> CarClasses` (class name, class color, car count)
- `IRacingSessionDecoder`: extract class info from iRacing session YAML (`CarClassID`, `CarClassShortName`, `CarClassColor`)
- `IRacingRelativeCalculator`: populate `ClassPosition` from class-specific ordering
- Default `CarClass = ""` and `ClassPosition = Position` for single-class sessions

**Acceptance criteria:**
- [ ] `RelativeEntry` carries class info in multi-class sessions
- [ ] Single-class sessions work identically to MVP (class fields have sensible defaults)
- [ ] `SessionData.CarClasses` is populated from session YAML
- [ ] Unit test: multi-class relative calculation with correct class positions
- [ ] Config migration adds default class color palette

**Dependencies:** TASK-701 (config migration for class colors), TASK-801 (data pipeline).

---

### TASK-706 · Overlay registration and dynamic creation

**Status:** `[ ]`

Replace hardcoded overlay list in `OverlayManager` with a registration-based system.

**What to build:**
- `OverlayRegistry`: dictionary mapping overlay ID strings to factory delegates
- `OverlayManager` iterates registered overlay types, creates instances from config
- Config `overlays` array can contain entries for any registered overlay ID
- Default config generation: each registered overlay provides its own defaults
- Settings sidebar populated from registry (not hardcoded)

**Acceptance criteria:**
- [ ] `OverlayManager` has no hardcoded overlay type references
- [ ] Adding a new overlay = write class + register in DI — no changes to OverlayManager or SettingsWindow
- [ ] Settings sidebar shows all registered overlays dynamically
- [ ] Overlay enable/disable works for all registered overlays
- [ ] Config contains entries only for overlays the user has (no phantom entries for unregistered types)

**Dependencies:** TASK-703 (DI container).

---
