# NrgOverlay вЂ” MVP Code & Architecture Review

> Review conducted 2026-04-06 after MVP completion (Phases 0вЂ“6).

---

## Executive Summary

NrgOverlay MVP is a well-architected, production-quality overlay system. The layered project structure, thread-safe data bus, and rendering pipeline are solid foundations for Alpha development. The codebase is clean, test coverage on core algorithms is strong, and performance benchmarks are in place.

This review identifies **3 blocking issues** for Alpha, **6 improvements** to address early, and **4 architectural gaps** that need design decisions before new features land.

---

## Architecture Assessment

### What Works Well

| Area | Assessment |
|---|---|
| **Layered dependency graph** | Strictly enforced; no circular deps. Adding overlays or sims requires zero changes to Core/Rendering. |
| **SimDataBus** | Lock-free reads via ImmutableDictionary. Exception isolation per subscriber. Sub-10 ns publish (benchmarked). Zero allocations on hot path. |
| **Rendering pipeline** | Software D2D в†’ ULW avoids GPU interference with game flip chains. Solved the DComp z-order reliability problem that other overlay tools fight. |
| **Config system** | Atomic writes, debounced persistence, stream override with null-coalescing resolution. `Resolve()` returns `this` (zero alloc) when no override active. |
| **ZOrderHook** | Reactive EVENT_OBJECT_REORDER approach is correct вЂ” avoids the DWM re-composition blink that periodic BringToFront caused. |
| **RelativeCalculator** | Complex lap-distance wraparound logic is well-tested with edge cases (start/finish crossing, spectator filtering, window centering). |
| **Edit mode** | Static mock data during repositioning is the right UX call. Preview/apply split in Settings is standard and works. |

### Blocking Issues for Alpha

#### B-1: No Config Version Field (ISSUE-010)

`AppConfig` has no `Version` property. Any schema change in Alpha (new overlay types, new fields) will break existing configs with no migration path. Users will need to delete their config and start over.

**Impact:** Every Alpha feature that adds config fields.
**Fix:** Add `public int Version { get; set; } = 1;` and a migration pipeline in `ConfigStore.Load()`.

#### B-2: Manual Config Cloning in OverlayManager

`OverlayManager.CopyConfig()` manually copies 18+ fields between `OverlayConfig` instances. Every new config field added in Alpha (and there will be many вЂ” new overlays, new columns, new features) must be manually added to this method or the field silently won't persist.

**Impact:** Every task that adds overlay config properties.
**Fix:** Replace with reflection-based deep clone, `MemberwiseClone` + manual reference fixup, or convert `OverlayConfig` to use `System.Text.Json` round-trip serialization for cloning.

#### B-3: No DI Container Wiring

`Program.cs` constructs all services manually. This works for 3 overlays but Alpha targets 9+. Manual construction will become error-prone and hard to maintain.

**Impact:** Every phase that adds overlays or services.
**Fix:** Wire up `Microsoft.Extensions.DependencyInjection` (already referenced) with proper service registration.

### Improvements to Address Early

#### I-1: TemperatureUnit Configured But Not Applied

`GlobalSettings.TemperatureUnit` exists and the Settings UI exposes it, but `SessionInfoOverlay` always displays Celsius. Dead config field visible to users.

#### I-2: OverlayConfig.Resolve Copies StreamOverride Reference (ISSUE-009)

The resolved clone shares the `StreamOverride` object with the original. Currently safe because nobody mutates via the clone, but fragile as the codebase grows.

#### I-3: IRacingPoller._cachedDrivers Volatile Ref to Mutable List (ISSUE-013)

The volatile reference swap is correct, but the list is mutable. Should be `ImmutableArray<DriverSnapshot>` to make the thread-safety contract explicit.

#### I-4: GlobalSettings Missing SimPriorityOrder (ISSUE-011)

Needed before multi-sim support in Phase 12.

#### I-5: AppLog Opens/Closes File Per Write (ISSUE-012)

Acceptable at current rates but will bottleneck with more overlays and diagnostic logging. Switch to `StreamWriter` with auto-flush.

#### I-6: Win32 P/Invoke Error Checking

`RegisterHotKey` and some `CreateWindowEx` call sites don't check return values. Can silently fail on systems with hotkey conflicts.

### Architectural Gaps Needing Design Decisions

#### G-1: Stream Override Can't Achieve Dual-Display Goal

The stream override system was designed for "driver sees one appearance, OBS captures another." But with a single window, both the driver and OBS see the same thing вЂ” whichever profile is active. The original vision (driver's monitor shows minimal overlay, OBS captures rich colorful version simultaneously) requires either:

- **Option A:** Two windows per overlay type вЂ” a "driver window" and a "stream window" on different monitors or with different visibility
- **Option B:** OBS virtual camera / NDI approach вЂ” render the stream version to an off-screen buffer that OBS reads
- **Option C:** Accept current behavior вЂ” stream mode is a manual toggle before going live

**Needs user input:** Which option to pursue.

#### G-2: Overlay Registration System

MVP hardcodes 3 overlays in `OverlayManager`. Alpha adds 6+ more. Need a registration/discovery pattern вЂ” either a simple factory dictionary or a full plugin system.

#### G-3: Multi-Class Data Model

iRacing multi-class races have distinct car classes (GTP, LMP2, GT3, etc.) with class-specific positions and colors. The current `RelativeEntry` has no `CarClass` field. This affects Relative, Standings, and Track Map overlays.

#### G-4: Settings UI Scalability

The current Settings window has a sidebar with 3 overlay entries + global. With 9+ overlays, the sidebar needs reorganization (categories, scrolling, or a different layout pattern).

---

## Per-Project Code Quality

### NrgOverlay.Core вЂ” Excellent

- `SimDataBus`: Textbook pub/sub with concurrency guarantees. Well-tested.
- `ConfigStore`: Atomic writes, graceful degradation. Solid.
- `OverlayConfig.Resolve()`: Clean null-coalescing pattern. Zero-alloc common path.
- **Only concern:** Config cloning (B-2 above).

### NrgOverlay.Rendering вЂ” Excellent

- `OverlayWindow`: Sophisticated Win32 + D2D integration. Correct flag combinations.
- `BaseOverlay`: Thread-safe render loop, deferred invalidation, background pre-fill safety net.
- `RenderResources`: Lazy cache with proper invalidation.
- `ZOrderHook`: Elegant reactive z-order recovery.
- **Only concern:** Device recovery path is simple (exponential backoff) вЂ” may need enhancement if multiple overlays hit device loss simultaneously.

### NrgOverlay.Sim.iRacing вЂ” Good

- `IRacingRelativeCalculator`: Well-tested, handles edge cases.
- `IRacingSessionDecoder`: Robust parsing with fallbacks.
- `IRacingPoller`: Clean SDK integration.
- **Concerns:** Magic constant `RelativePublishInterval = 6` undocumented. `_cachedDrivers` mutability (I-3).

### NrgOverlay.Overlays вЂ” Good

- All three overlays follow consistent patterns.
- Mock data is realistic and well-structured.
- `DeltaBarOverlay` trend buffer: ring buffer push may have edge case when not full (first 500ms of a lap). Not a crash risk, just potentially noisy trend arrows at lap start.
- **Concern:** As overlay count grows, shared patterns (column rendering, data subscription) should be extracted to avoid duplication.

### NrgOverlay.App вЂ” Adequate (needs work for Alpha)

- `Program.cs`: Manual composition root will need DI (B-3).
- `OverlayManager`: Manual config cloning (B-2). Hardcoded overlay list (G-2).
- `SimDetector`: Clean debounce logic. Ready for multi-sim.
- `SingleInstanceGuard`: Solid Win32 IPC.
- `Settings/ViewModels`: Property-per-field approach won't scale to 9+ overlays. Consider generating VMs or using a more generic approach.

### Tests вЂ” Good Coverage for Core, Gaps Elsewhere

| Area | Coverage | Notes |
|---|---|---|
| SimDataBus | Excellent | Concurrency stress tests, exception isolation |
| ConfigStore | Excellent | Round-trip, corruption, atomic writes |
| OverlayConfig.Resolve | Good | All override scenarios, position exclusion |
| RelativeCalculator | Excellent | Edge cases, wraparound, filtering |
| ViewModels | Good | Round-trip conversions |
| Rendering (OverlayWindow, BaseOverlay) | None | Requires D2D context вЂ” manual testing only |
| Settings UI | None | Manual test docs exist |
| Full app lifecycle | None | Manual verification docs |
| IRacingPoller integration | None | Requires live iRacing session |

---

## Performance Assessment

Based on BenchmarkDotNet results:

| Benchmark | Result | Verdict |
|---|---|---|
| SimDataBus.Publish (1 subscriber) | 9.3 ns, 0 B alloc | Excellent |
| ConfigResolve (no override) | 0 B alloc (returns `this`) | Excellent |
| ConfigResolve (with override) | < 500 B alloc | Acceptable at 60 Hz |
| RelativeCalculator (40 cars) | < 50 Вµs | Good for 10 Hz |

Render-path performance is not benchmarkable without D2D. Software D2D for text + rectangles at ~1вЂ“3% of one core (3 overlays @ 60 fps) is acceptable. Will need re-evaluation at 9+ overlays.

---

## Documentation Assessment

### Strengths
- Comprehensive decision log with alternatives and consequences
- Per-phase task files with acceptance criteria
- Manual test checklists for UI features
- Known issues tracker with status

### Issues
- `README.md` line 12 still references "Direct2D + DirectComposition" вЂ” the pipeline was changed to ULW + software DCRenderTarget
- `ARCHITECTURE.md` В§4 "Rendering Pipeline" is accurate for current stack, but В§11 "OBS Capture Compatibility" still mentions `WS_EX_NOREDIRECTIONBITMAP` as if it's used
- `README.md` Phase 2 still shows "In progress" вЂ” should be "Done"
- `ARCHITECTURE.md` В§10 says "DI packages are referenced but the container is not used" вЂ” accurate but should be addressed in Alpha
- `OVERLAYS.md` describes only 3 MVP overlays вЂ” needs expansion for Alpha

---

## Recommendations Summary

| Priority | Item | When |
|---|---|---|
| **Block** | B-1: Config version + migration | Phase 7 (first) |
| **Block** | B-2: Config deep clone | Phase 7 |
| **Block** | B-3: DI container wiring | Phase 7 |
| **High** | I-1: Temperature unit conversion | Phase 7 |
| **High** | G-2: Overlay registration system | Phase 7 (before new overlays) |
| **High** | G-3: Multi-class data model | Phase 8 (data pipeline) |
| **Medium** | I-2 through I-6: Remaining known issues | Phase 7 |
| **Medium** | G-1: Dual-window stream architecture | Phase 11 (needs design) |
| **Medium** | G-4: Settings UI scalability | Phase 12 |
| **Low** | DeltaBarOverlay trend buffer edge case | Opportunistic |

