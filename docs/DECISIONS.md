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
