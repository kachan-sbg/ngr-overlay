# PHASE 21 - Performance and Soak Validation (Mid Priority)

## Goal

Guarantee stable low-resource behavior on weak systems during long sessions (4-6+ hours),
not only fast microbenchmarks.

## Why this phase exists

Current BenchmarkDotNet coverage validates only small hot paths. That is useful, but it does
not prove runtime stability under real session churn (sim reconnects, SDK stalls, long uptime,
GC pressure, render load).

## Scope

1. Expand benchmark coverage beyond pure compute:
   - Shared-memory read/decode paths (iRacing and LMU)
   - Render-path hot sections (text layout + draw pass for active overlays)
   - Config save/debounce path cost under repeated updates
2. Add long-run soak test profile:
   - 30-minute smoke soak (CI-capable)
   - 4-6 hour local soak profile for release candidate checks
3. Add runtime telemetry for performance diagnostics:
   - Process memory trend (working set + managed heap)
   - GC collections by generation
   - Poll loop timing (avg/p95/max)
   - Render loop timing (avg/p95/max)
4. Add pass/fail thresholds and regression gates.

## Deliverables

- New benchmark scenarios in `tests/NrgOverlay.Benchmarks`:
  - `SharedMemoryReadBenchmarks` (iRacing/LMU replay-style synthetic snapshots)
  - `OverlayRenderBenchmarks` (representative text-heavy frames)
  - Existing benchmarks retained as baseline set
- Soak harness:
  - Repeatable runner script and instructions
  - Metrics output file (CSV or JSON) with summary table
- Docs:
  - "Performance budget" and "Soak pass criteria" section in `docs/ARCHITECTURE.md`
  - Quick start commands in `docs/README.md` or task index reference

## Acceptance Criteria

- No unbounded memory growth in 30-minute soak.
- No unhandled exceptions during soak scenarios.
- No benchmark regression beyond agreed budgets (documented thresholds).
- Soak/benchmark commands are one-command reproducible by contributors.

## Suggested command profile

```powershell
dotnet test NrgOverlay.sln -c Release /m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false
dotnet run -c Release --no-build --project tests/NrgOverlay.Benchmarks/NrgOverlay.Benchmarks.csproj -- --filter "*" --job short
```


