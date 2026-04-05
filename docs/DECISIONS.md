# SimOverlay — Decision Log

Chronological record of every significant design decision: what was decided, why, and what alternatives were considered. Update this file whenever a non-trivial decision is made or reversed.

Format per entry:
- **Date** — when the decision was made
- **Decision** — what was decided (one sentence)
- **Context** — what problem or question prompted this
- **Rationale** — why this option was chosen
- **Alternatives considered** — what else was evaluated and why it was rejected
- **Consequences** — what this decision constrains or enables going forward

---

## 2026-03-30 — Technology stack: Direct2D + DirectComposition for overlay rendering

**Decision:** Use Direct2D + DirectComposition (`WS_EX_NOREDIRECTIONBITMAP`, DXGI flip-model swap chain, premultiplied alpha) as the overlay rendering stack instead of WPF, Electron, or GDI.

**Context:** The primary goal of the project is minimal CPU/GPU impact. An analysis of existing apps (SimHub, RaceLab, TinyPedal) found that all use rendering approaches with known overhead problems.

**Rationale:**
- DirectComposition surfaces are shared directly with DWM — there is no CPU-side framebuffer copy, which is the main overhead source in all alternative approaches.
- Flip-model swap chains (`DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL`) minimise present latency.
- `DXGI_ALPHA_MODE_PREMULTIPLIED` provides correct per-pixel alpha blending entirely on the GPU.
- Compatible with VRR (G-Sync/FreeSync) — SimHub's WPF rendering is known to conflict with VRR when set to "all apps" mode.

**Alternatives considered:**
- **WPF** — Used by SimHub. Creates and destroys GPU textures every frame when applying shaders. Software rasterizer fallback is common. VRR conflicts documented. Rejected.
- **Electron/Chromium** — Used by RaceLab. Each overlay window is a full Chromium renderer process. 200–500 MB RAM base. Competing compositor. Rejected.
- **GDI/GDI+** — CPU-only, no hardware acceleration post-Vista. Obsolete for this use case. Rejected.
- **WS_EX_LAYERED + Direct2D (without DirectComposition)** — Still requires a CPU bus copy of the framebuffer for `UpdateLayeredWindow`. Rejected.
- **Python + Qt (PySide6)** — Used by TinyPedal. Python GIL limits true multi-threading; not suitable for sub-16ms render loops. Rejected.
- **Skia** — No DirectComposition integration on Windows. Would require pairing with a window approach that has its own overhead. No benefit over Direct2D on Windows-only target. Rejected.

**Consequences:**
- Requires Vortice.Windows (NuGet) for managed DirectX bindings, or raw P/Invoke.
- All overlay windows must use `WS_EX_NOREDIRECTIONBITMAP` — legacy OBS BitBlt capture will not work (documented limitation; modern OBS WGC works fine).
- Settings UI (non-performance-critical) may still use WPF — this is acceptable since it is not on the render path.

---

## 2026-03-30 — Language: C# / .NET 8

**Decision:** Write the application in C# targeting .NET 8.

**Context:** Need a language that can access Win32/DirectX APIs, runs efficiently on Windows, and allows reasonably fast development.

**Rationale:**
- Full access to Win32 and DirectX via P/Invoke and Vortice.Windows.
- .NET 8 supports AOT compilation and aggressive trimming, reducing JIT overhead on the hot render path.
- `GameOverlay.Net` and similar libraries exist as reference implementations.
- Faster iteration than C++ for a solo/small project while delivering sufficient performance.

**Alternatives considered:**
- **C++** — Maximum control, zero runtime. More complex build toolchain, slower development. Considered for the rendering layer only; rejected in favour of C# + Vortice bindings which are thin enough.
- **Rust** — Memory-safe, zero-overhead. `windows-rs` crate provides Direct2D/DirectComposition bindings. Ecosystem for sim data libraries (iRacing, ACC) is immature. Rejected for now; viable future rewrite candidate.

**Consequences:**
- Need to manage GC pressure on the render hot path — pre-allocate render objects, avoid LINQ and closures in `OnRender()`.
- .NET 8 runtime must be bundled in the published executable (self-contained publish).

---

## 2026-03-30 — Architecture: multi-project solution with abstraction boundary at sim-agnostic DTOs

**Decision:** Organise the solution as six projects with a strict dependency graph; the abstraction boundary between sim-specific and sim-agnostic code sits at `SimOverlay.Sim.Contracts` (normalized DTOs + `ISimProvider`).

**Context:** MVP is iRacing-only but the product must cleanly support ACC, AC, LMU, rF2 in future without structural changes.

**Rationale:**
- Overlay implementations (`SimOverlay.Overlays`) depend only on `Sim.Contracts` DTOs — they have zero knowledge of which sim is active.
- Adding a new sim requires only: create a new `SimOverlay.Sim.X` project, implement `ISimProvider`, map sim-specific data to the existing DTOs.
- No changes to `Rendering`, `Overlays`, or `Core` when adding a sim.

**Alternatives considered:**
- **Single project** — Simple for MVP but creates coupling that makes sim abstraction harder later. Rejected.
- **Plugin/assembly-loaded providers** — More flexible but adds complexity (assembly loading, versioning, security). Rejected for MVP; can be added later.

**Consequences:**
- If a future sim has data that doesn't map to existing DTOs, new DTO types must be added to `Sim.Contracts` — a controlled, deliberate expansion.
- `Sim.*` projects may never depend on `Rendering` or `Overlays` — enforced by the dependency graph.

---

## 2026-03-30 — Data delivery: ISimDataBus publish/subscribe

**Decision:** Use an in-process publish/subscribe bus (`ISimDataBus`) to deliver sim data from provider threads to overlay render threads, rather than direct method calls or shared mutable state.

**Context:** Data is produced on a dedicated background thread (60 Hz), consumed by multiple overlay render loops each on their own thread. Need thread-safe, decoupled delivery.

**Rationale:**
- Decouples providers from overlays — providers don't know what overlays exist; overlays don't know which provider is active.
- Overlays use a snapshot pattern: `OnDataUpdate()` atomically stores the latest value; `OnRender()` reads it without blocking.
- Prevents render thread stalls waiting for data thread.

**Alternatives considered:**
- **Shared mutable state with locks** — Simpler but introduces lock contention between data thread and potentially multiple render threads. Rejected.
- **Channels (`System.Threading.Channels`)** — Good for queuing but introduces latency (consumer must drain queue). Overlays want the *latest* value, not every intermediate value. Rejected in favour of atomic snapshot.

**Consequences:**
- `OnDataUpdate()` callbacks run on the data thread — they must be fast (atomic field write only; no rendering, no heavy computation).
- Subscribers must unsubscribe on `Dispose()` to prevent memory leaks.

---

## 2026-03-30 — Only one sim active at a time

**Decision:** Only one `ISimProvider` may be active (polling) at any time. `SimDetector` activates the first provider whose `IsRunning()` returns true and stops all others.

**Context:** User requirement. Running multiple sim pollers simultaneously wastes CPU with no benefit since a user can only be in one sim at a time.

**Rationale:** Correct by design — a user cannot physically be in two sims at once. Polling inactive sims wastes resources.

**Consequences:**
- `SimDetector` must handle the transition cleanly when one sim closes and another opens.
- `simPriorityOrder` in config determines which sim wins if two are somehow running simultaneously.

---

## 2026-03-30 — OBS compatibility: omit WS_EX_TOOLWINDOW, use stable window titles

**Decision:** Do not apply `WS_EX_TOOLWINDOW` to overlay windows. Set a fixed, stable window title on each overlay (`SimOverlay — Relative`, etc.).

**Context:** OBS integration is a core feature. `WS_EX_TOOLWINDOW` hides windows from OBS's window picker, making the overlay uncapturable via Window Capture unless the user knows to use Display Capture instead.

**Rationale:**
- Without `WS_EX_TOOLWINDOW`, windows appear in the OBS window picker.
- OBS 28+ defaults to Windows Graphics Capture (WGC) for Window Capture, which works correctly with `WS_EX_NOREDIRECTIONBITMAP` (captures at DWM compositor level).
- `DXGI_ALPHA_MODE_PREMULTIPLIED` + OBS "Allow Transparency" = correct alpha capture, no chroma key.
- Stable titles mean OBS scene sources remain valid across app restarts.

**Alternatives considered:**
- **Keep `WS_EX_TOOLWINDOW`, document Display Capture as the OBS method** — Works but requires the user to capture the entire monitor, cannot isolate individual overlays per OBS scene. Rejected.
- **Provide a separate "OBS capture" window** that composites all overlays — Adds complexity, harder for users to configure per-overlay visibility in OBS. Rejected.

**Consequences:**
- Overlay windows may briefly appear in the Windows taskbar (acceptable — one-time OBS setup).
- Legacy OBS BitBlt capture does not work with `WS_EX_NOREDIRECTIONBITMAP` — documented limitation, users on OBS <28 or Windows <1903 must upgrade or use Display Capture.

---

## 2026-03-30 — Stream override: dual-profile config per overlay

**Decision:** Each overlay has a base config (driver's screen view) and an optional `StreamOverrideConfig` with all visual fields nullable. A global `streamModeActive` flag resolves which profile is used. Position (X/Y) is never part of the override.

**Context:** Users want overlays that look different on their own screen vs what OBS captures for viewers. Streamers want minimal overlays while racing but rich, colourful layouts for their audience.

**Rationale:**
- Single window — no overlay multiplication. The same window simply changes its resolved config when stream mode is toggled.
- Nullable override fields with `null` = inherit means users only configure the properties that actually differ from their screen layout. Most properties can be left as inherit.
- Persisting `streamModeActive` means streamers who always want the stream layout don't have to toggle every session.
- Keeping position out of the override avoids the window jumping when switching modes, which would be jarring.
- Resizing in stream mode writes to `StreamOverride.Width/Height` specifically, so each profile has its own dimensions.

**Alternatives considered:**
- **Two separate overlay instances per overlay type** — Doubles the overlay count. More complex to manage in OBS (two sources per overlay). User explicitly rejected this. Rejected.
- **Named config presets (more than two)** — More flexible but significantly more complex settings UI and config schema. The two-profile (screen/stream) model covers the real use case. Can be extended later.
- **Position as part of the override** — Would allow the stream layout to be in a different position. Rejected because it causes jarring window movement on mode toggle, and OBS sources would need to be repositioned. Position is managed in OBS, not in the overlay.

**Consequences:**
- `OverlayConfig.Resolve(bool streamModeActive)` is called at the start of every render frame to get the effective config — must be a cheap operation (no allocation if no override active, or a simple struct copy).
- Settings UI needs two tabs per overlay (Screen / Stream Override). Override tab needs "Custom" checkbox per field to indicate inherit vs override.
- Edit mode + stream mode interaction: drag saves to base X/Y always; resize saves to override dimensions if in stream mode with override enabled.

---

## 2026-04-04 — Omit `WS_EX_LAYERED` from overlay window styles *(superseded 2026-04-05 — see below)*

**Decision:** Do not apply `WS_EX_LAYERED` to overlay windows. Use `WS_EX_NOREDIRECTIONBITMAP` + DirectComposition premultiplied alpha for transparency instead.

**Context:** During Phase 1 implementation, overlay windows were initially created with `WS_EX_LAYERED`. After adding resize support, resizing the window beyond its original size stopped generating mouse events in the expanded area — the interactive region stayed permanently at the original 300×100 pixels.

**Rationale:**
- When `WS_EX_LAYERED` is present, Windows caches the DWM/DComp alpha hit-test mask from the first presented frame. The interactive area is computed once and never updated, even after the window resizes or new frames are presented.
- `WS_EX_NOREDIRECTIONBITMAP` alone (without `WS_EX_LAYERED`) does not suffer this caching. The hit-test region tracks the actual window rectangle, which updates correctly on resize.
- DComp premultiplied alpha provides correct per-pixel transparency without `WS_EX_LAYERED`. No `UpdateLayeredWindow` is needed.

**Superseded by:** 2026-04-05 — Switch to `UpdateLayeredWindow` rendering. The hit-test caching concern no longer applies because `WS_EX_LAYERED` + ULW does not use DComp's alpha mask caching. `WM_NCHITTEST` handles hit-testing independently of the layered window bitmap, so resize works correctly.

---

## 2026-04-05 — BaseOverlay stores the backing config; resolves per-frame for stream mode

**Decision:** `BaseOverlay._config` holds the raw (non-resolved) `OverlayConfig` from `AppConfig.Overlays`. The stream-mode-resolved config is computed by calling `_config.Resolve(streamModeActive)` on every render frame rather than being cached.

**Context:** TASK-302 requires that resizing in stream mode writes to `StreamOverride.Width/Height` rather than the base config. This requires the backing config to be accessible at `WM_SIZE` time. If `_config` held the resolved copy, the `StreamOverride` object would be unreachable.

**Rationale:**
- Keeping the backing config in `_config` makes `OnSize` trivially correct: write to `_config.StreamOverride` or `_config` depending on mode.
- `Resolve()` returns `this` (same reference) when stream mode is off, so there is zero allocation cost in the common case.
- When stream mode is active, `Resolve()` allocates one small POCO per frame (~180 objects/sec across 3 overlays). GC gen-0 cost is negligible at 60 fps.
- Per-frame resolution means stream mode toggles are reflected in the next frame without any explicit cache invalidation.

**Alternatives considered:**
- **Cache the resolved config, invalidate on `StreamModeChangedEvent`**: Adds a dirty flag and an extra code path. Correct but complex; the allocation saving is not measurable.
- **Pass resolved config as a separate field**: Two configs in `BaseOverlay` is confusing and requires discipline to keep in sync.

**Consequences:**
- `UpdateConfig()` must receive the backing config (not a resolved copy) or persistence breaks.
- `OverlayWindow` receives the stream-mode-resolved dimensions for initial window sizing so the correct profile is used at startup.

---

## 2026-04-05 — Deferred RenderResources.Invalidate() to render thread (TASK-303)

**Decision:** `UpdateConfig()` sets a `volatile bool _pendingInvalidate` flag rather than calling `_resources.Invalidate()` directly. The render thread checks and applies the invalidation at the top of each frame.

**Context:** `RenderResources.Invalidate()` disposes cached `ID2D1SolidColorBrush` and `IDWriteTextFormat` COM objects. The render thread may have received a brush reference from `GetBrush()` and be mid-draw when `Invalidate()` is called from a settings thread, causing use-after-dispose.

**Rationale:**
- Brush objects are used outside the `RenderResources` internal lock. Disposing from another thread is unsafe regardless.
- Deferring to the render thread (the only thread that calls `GetBrush`/draws with results) eliminates the race entirely.
- A single `volatile bool` is sufficient — successive `UpdateConfig()` calls before the render thread wakes still result in exactly one `Invalidate()`.

**Alternatives considered:**
- **Lock around all brush usage in `OnRender`**: Invasive; changes every overlay's draw code and adds lock contention.
- **Copy brushes before drawing**: Direct2D COM objects are not cheaply copyable.

**Consequences:**
- Config changes take effect within one render frame (~16 ms) — meets TASK-303 acceptance criteria.
- `StreamModeChangedEvent` also sets `_pendingInvalidate` so appearance changes from stream mode toggle apply on the next frame.

---

## 2026-04-05 — SimDetector bridges ISimProvider.StateChanged to ISimDataBus

**Decision:** `SimDetector` subscribes to `ISimProvider.StateChanged` and re-publishes each state as a `SimStateChangedEvent` on `ISimDataBus`. Overlays subscribe to the bus event, not to the provider directly.

**Context:** `BaseOverlay` lives in `SimOverlay.Rendering`, which must not depend on `SimOverlay.Sim.Contracts`. Overlays cannot subscribe to `ISimProvider.StateChanged` directly without violating the dependency graph.

**Rationale:**
- `ISimDataBus` and `SimStateChangedEvent` live in `SimOverlay.Core` — safe for every layer to reference.
- `SimDetector` in `App` already owns the provider lifecycle, making it the natural bridging point.
- Keeps `Rendering` and `Overlays` fully decoupled from sim-specific contracts.

**Alternatives considered:**
- **Move `SimStateChangedEvent` to `Sim.Contracts`**: Overlays then depend on `Sim.Contracts`, violating current layering rules.
- **Add a separate `ISimStateSource` interface in Core**: More indirection than needed for one event type.

**Consequences:**
- All sim-state consumers go through the bus. Adding a second provider later requires no changes to overlays or rendering.

---

## 2026-04-05 — Replace DirectComposition + GPU D2D with UpdateLayeredWindow + software DCRenderTarget

**Decision:** Replace the Phase 1 rendering stack (D3D11 + `ID2D1DeviceContext` + DXGI swap chain + DirectComposition) with `ID2D1DCRenderTarget` (software CPU rendering) into a GDI DIB, presented each frame via `UpdateLayeredWindow(ULW_ALPHA)`. Window style changes from `WS_EX_NOREDIRECTIONBITMAP` (no `WS_EX_LAYERED`) to `WS_EX_LAYERED` (no `WS_EX_NOREDIRECTIONBITMAP`).

**Context:** During Phase 3 debugging, overlay visibility was investigated because overlays appeared invisible while iRacing was running. The rendering architecture was overhauled under the (incorrect) hypothesis that the DComp swap chain was being composited behind iRacing's DXGI flip chain. The actual root cause turned out to be unrelated (see next entry). However, analysing three candidate approaches revealed that the intermediate approach tried — GPU D3D11 render texture + staging-texture CPU readback + ULW — was objectively the most expensive and was immediately discarded. The resulting comparison made the ULW + software approach the clear winner.

**Rationale:**
- **DComp z-order reliability**: With `WS_EX_NOREDIRECTIONBITMAP + DComp`, the DComp visual tree has its own DWM composition tier. `SetWindowPos(HWND_TOPMOST)` controls the input routing z-order but not always the visual render order in DWM's DComp layer. `WS_EX_LAYERED + ULW` windows participate in DWM's standard window compositor where HWND z-order fully controls render order. Production overlay tools (SimHub, Crew Chief, iOverlay) all use the layered window approach.
- **Resource cost comparison at 60 fps for simple overlay content:**
  - DComp + GPU D2D: ~0.1 ms CPU/frame, ~0.5 ms GPU/frame, no data transfer — most efficient overall, but z-order concerns above.
  - ULW + GPU staging readback: ~3–5 ms CPU/frame (Map/Unmap stall + 760 KB/frame Marshal.Copy), GPU memory — **worst option**; discarded immediately.
  - ULW + software DCRenderTarget: ~1–3 ms CPU/frame, no GPU, no data transfer — acceptable for simple 2D content; chosen.
- The software DCRenderTarget renders directly into the DIB in CPU memory. No staging texture, no GPU memory, no CPU–GPU transfer. For text + rectangles the CPU cost is low (~1–3% of one core across all three overlays at 60 fps).
- Eliminates the `Vortice.Direct3D11` dependency from `SimOverlay.Rendering`.

**Alternatives considered:**
- **Keep DComp**: Most CPU-efficient but introduces potential z-order reliability risk. Revisit if Phase 4 rendering performance becomes measurable.
- **ULW + GPU staging readback**: Tried briefly. Every frame required `CopyResource` + `Map/Unmap` (CPU stall waiting for GPU) + Marshal.Copy of ~760 KB. Higher CPU and memory overhead than either alternative. Rejected.

**Consequences:**
- `OverlayWindow` no longer holds D3D11/DXGI/DComp resources — much simpler device recovery path.
- `RenderResources` and all `OnRender` signatures changed from `ID2D1DeviceContext` to `ID2D1RenderTarget` (the common base of both `ID2D1DeviceContext` and `ID2D1DCRenderTarget`).
- If Phase 4 rendering requires complex effects not available on `ID2D1RenderTarget`, the factory and render target can be upgraded to `ID2D1Factory1` + `ID2D1DeviceContext` without changing `RenderResources` or overlay code.
- `WS_EX_LAYERED` is now required. Do not remove it.
- `WS_EX_NOREDIRECTIONBITMAP` must not be re-added — it tells DWM to ignore the ULW bitmap, making the window invisible.

---

## 2026-04-05 — BaseOverlay always draws background before delegating to OnRender

**Decision:** `BaseOverlay.OnRender` unconditionally fills the overlay rectangle with `OverlayConfig.BackgroundColor` before calling the abstract `OnRender(context, config)` (or the sim-state placeholder), in all sim states.

**Context:** During Phase 3 debugging, overlays became invisible the moment iRacing entered a session (`SimState == InSession`). Root-cause analysis of the logs showed the transition was exact: every `SimStateChangedEvent → InSession` entry was immediately followed by invisible overlays. The concrete overlay implementations (`RelativeOverlay`, `SessionInfoOverlay`, `DeltaBarOverlay`) are Phase 3 stubs with empty `OnRender` bodies — they draw nothing. When `InSession`, `BaseOverlay` called the empty stub, resulting in `Clear(transparent)` + nothing = fully transparent window. This was masked for a long time by the parallel rendering pipeline investigation, which kept changing infrastructure while the content was always empty.

**Rationale:**
- A filled background guarantees the overlay is always visible as long as the window exists and is shown — regardless of whether the concrete `OnRender` draws anything.
- Phase 4 overlay implementations will draw their own content on top of the background; the background pre-fill costs one `FillRectangle` call and is negligible.
- The sim-state placeholder already drew a background fill, so the non-`InSession` path is unaffected. Only the `InSession` path needed the guard.
- Serves as a permanent safety net: future overlays that accidentally draw nothing (e.g., during early Phase 4 development) will show a coloured rectangle rather than invisibly disappearing.

**Alternatives considered:**
- **Draw the background only in the InSession branch**: Equivalent in current code, but less robust — a future refactor that adds another branch could accidentally omit the background.
- **Require concrete overlays to always draw a background**: Puts the burden on every subclass; fragile.

**Consequences:**
- Phase 4 overlay implementations should use the config's background color (same source), so there is no visual difference between the pre-filled background and what the overlay would draw anyway.
- `RenderSimStatePlaceholder` still draws its own background fill — this is now redundant but harmless (overdraws the same pixels).

## 2026-04-05 — Remove periodic BringToFront from render loop

**Decision:** Removed the 120-frame (~2 s) `BringToFront()` call from `BaseOverlay.RenderLoop`. Z-order is maintained solely by `ZOrderHook` (EVENT_OBJECT_REORDER).

**Context:** After Phase 4 smoke testing, overlays blinked visibly every ~2 seconds even with no sim running. The blink was initially misattributed to `SimDetector` polling, but persisted after the SimDetector debounce fix. Inspection of the render loop revealed `BringToFront()` being called every 120 frames as a fallback z-order mechanism. `BringToFront` calls `SetWindowPos(HWND_TOPMOST)`, which causes DWM to re-composite the layered window — briefly producing a transparent frame visible as a blink.

**Rationale:** `ZOrderHook` fires `EVENT_OBJECT_REORDER` immediately whenever any TOPMOST window changes z-order, and calls `BringAllToFront()` on the overlay manager. This is the correct reactive mechanism. The periodic fallback in the render loop was added as belt-and-suspenders, but its side effect (DWM re-composition causing blink) outweighs any benefit.

**Alternatives considered:**
- **Reduce interval (e.g. 600 frames / 10 s)**: Still blinks, just less frequently.
- **Keep fallback, suppress DWM re-composition**: No known API to do this with ULW layered windows.

**Consequences:** If `ZOrderHook` misses an event (e.g. a sim that uses a non-standard z-order API), overlays may end up under the sim window and only recover on the next hook event. Acceptable trade-off given no blink is the primary UX requirement.

## 2026-04-05 — Edit mode shows static mock data; Settings uses preview/apply split

**Decision:**
1. In edit mode (`!IsLocked`), `BaseOverlay` always calls `OnRender` regardless of `SimState`, and each overlay substitutes its live data snapshot with a static `MockData` object. Real sim data is never shown in edit mode.
2. `OverlayManager.PreviewConfig` pushes config changes to the live overlay without saving (called on `LostFocus` in Settings). `ApplyConfig` additionally calls `SetPosition`/`SetSize` and persists to disk (called on Apply button).

**Context:** User positioning/resizing overlays in edit mode needs realistic-looking content so layout decisions can be made accurately. Showing live sim data during repositioning is distracting and unstable. Settings fields should give instant visual feedback on blur without committing to disk until the user explicitly applies.

**Rationale:**
- Static mock data is stable — no flicker or value changes while the user adjusts overlay bounds.
- The `IsLocked ? _latest : MockData` pattern keeps mock logic entirely in each overlay with no coupling to `BaseOverlay`.
- Preview/Apply split is the standard settings UX: blur = "see what it looks like", Apply = "I'm done".
- `SetPosition`/`SetSize` on `OverlayWindow` route through `WM_MOVE`/`WM_SIZE` so the existing `OnMove`/`OnSize` → debounce path is reused; no duplication of config-update logic.

**Alternatives considered:**
- Show real data in edit mode: rejected — live data moves while user is repositioning, making it hard to judge layout.
- Live-update on every keystroke: rejected — causes continuous `SetWindowPos` calls while typing; blur is the natural "commit" moment for a text field.
