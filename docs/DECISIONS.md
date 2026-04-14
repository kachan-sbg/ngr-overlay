# SimOverlay — Decision Log

Brief summary of every significant design decision. Full entries with rationale, alternatives, and consequences are in the era-specific files:
- [MVP decisions](decisions/mvp.md) (Phases 0-6)
- [Alpha decisions](decisions/alpha.md) (Phases 7-12)

## MVP (2026-03-30 to 2026-04-05)

| Date | Decision | Why |
|---|---|---|
| 03-30 | Direct2D + DComp rendering stack | Minimal CPU/GPU impact; avoids WPF/Electron overhead |
| 03-30 | C# / .NET 8 | Win32/DirectX access via Vortice; fast iteration vs C++ |
| 03-30 | Multi-project solution, DTOs as abstraction boundary | Clean sim-agnostic overlay layer; new sims need only a Sim.X project |
| 03-30 | ISimDataBus pub/sub for data delivery | Decouples providers from overlays; snapshot pattern avoids render stalls |
| 03-30 | One sim active at a time | Users can't be in two sims; saves CPU |
| 03-30 | Omit WS_EX_TOOLWINDOW, stable window titles | OBS WGC needs to see windows in its picker |
| 03-30 | Dual-profile config (base + stream override) | Single window switches appearance; nullable fields = inherit from base; X/Y never overridden |
| 04-04 | Omit WS_EX_LAYERED *(superseded 04-05)* | DComp alpha hit-test caching broke resize — later moot after ULW switch |
| 04-05 | BaseOverlay stores backing config, resolves per-frame | Needed for stream-mode resize to write correct config; zero-alloc when no override |
| 04-05 | Deferred RenderResources.Invalidate() via volatile flag | Prevents cross-thread COM dispose race between settings and render thread |
| 04-05 | SimDetector bridges StateChanged to ISimDataBus | Rendering/Overlays can't depend on Sim.Contracts; bus event stays in Core |
| 04-05 | **ULW + software DCRenderTarget** replaces DComp+GPU | DComp z-order unreliable under fullscreen sims; ULW is what SimHub/iOverlay use |
| 04-05 | BaseOverlay always draws background first | Empty OnRender stubs caused invisible windows; background is a permanent safety net |
| 04-05 | Remove periodic BringToFront from render loop | Caused 2s blink; ZOrderHook is the sole z-order mechanism |
| 04-05 | Edit mode = static mock data; Settings = preview/apply | Stable content while repositioning; blur = preview, Apply = persist |
| 04-05 | OverlayManager as single coordinator for modes | Single source of truth for edit/stream state; all UI surfaces go through it |
| 04-05 | Single-instance via named Mutex + hidden HWND IPC | Second launch posts WM_APP to open Settings in first instance |
| 04-05 | App icon via pre-build generator tool | Binary ICO can't be authored in text; generator uses System.Drawing |
| 04-05 | Hidden owner HWND suppresses taskbar buttons | Win32 owned popups skip taskbar; OBS WGC still enumerates them |
| 04-05 | Settings ShowInTaskbar=True, X hides to tray | Users couldn't find the window with ShowInTaskbar=False |
| 04-05 | BenchmarkDotNet for hot-path regression detection | Reproducible micro-benchmarks; measures allocations; no sim/GPU required |

## Alpha (2026-04-06 onwards)

| Date | Decision | Why |
|---|---|---|
| 04-06 | OBS Mode toggle (not dual-window), LMU before overlays, flat track map, read-only fuel, current weather only | 90/10 pragmatism; LMU surfaces DTO gaps early; flat map avoids track DB |
| 04-06 | Config versioning with sequential migration pipeline | Alpha adds fields across phases; numbered migrations are simple and testable |
| 04-06 | JSON round-trip for OverlayConfig deep clone | Covers all fields automatically; no manual field list to maintain as fields are added |
| 04-07 | Upgrade IRSDKSharper 1.0.3→1.1.6; deterministic Win32 handle release on disconnect | 1.0.3 left the MMF handle open after `Stop()`, causing "pending" state on sim restart |
| 04-07 | LMU: raw P/Invoke structs (Pack=4) instead of CrewChiefV4.rFactor2Data NuGet | No external dependency; structs match 64-bit rF2 layout exactly; telemetry stride derived from file size at runtime for plugin-version robustness |
| 04-07 | LMU: `LicenseClass.Unknown` sentinel + empty `LicenseLevel` to hide LIC column | Overlays need a defined "not available" state; `Unknown` renders grey; empty level string skips the LIC cell draw |
| 04-07 | Multi-class: CarClasses empty in single-class sessions; class fields blank/white | Avoids overlay clutter when multiclass info is irrelevant; overlays check `CarClasses.Count > 0` |
| 04-07 | SimDetector provider list sorted by `SimPriorityOrder` config at startup; config v3→v4 appends "LMU" | User-adjustable priority without code change; migration preserves existing order |
| 04-12 | Shutdown via `MessagePump.Quit()` not `Environment.Exit()` | `Environment.Exit` bypassed `using var provider` disposal; IRSDKSharper's HWND stayed alive, causing iOverlay to lose SDK events until iRacing restarted |
| 04-12 | `CarIdxTrackSurface >= 0` as the in-world filter for iRacing car slots | Garage/registered-not-spawned drivers have `LapDistPct == 0.0` (not -1); the old `pct < 0f` filter passed them through as ghost entries |
| 04-12 | Session timing smoothed locally (sync from SDK, count down with wall clock) | SDK occasionally returns 0 for `SessionTime` between valid samples; display blinked every few seconds; simple `> 0` guard + local delta eliminates the blink |
| 04-14 | `IRacingRelativeCalculator` converted to stateful instance; EMA smoothing on gap/interval values | Static pure function can't hold per-car filter state; EMA (α=0.15) eliminates 10 Hz jitter in gap/interval displays |
| 04-14 | Real-time race positions via `laps + lapDistPct` ranking | `CarIdxPosition` only updates at the finish line; progress-based ranking is immediate when cars pass each other; computed once in calculator, used by both relative and standings |
| 04-14 | Flag emoji for country/club display via Unicode regional indicators | Converts ISO 3166-1 alpha-2 code to U+1F1E6-based surrogate pairs; ISO 2-letter fallback for unmapped clubs |
