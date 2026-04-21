# NrgOverlay — Task Index

> Navigation: [CODE-NAV.md](../CODE-NAV.md) · [ARCHITECTURE.md](../ARCHITECTURE.md) · [ROADMAP.md](../ROADMAP.md)
> AI docs policy: [AI-DOCS-GUIDE.md](../AI-DOCS-GUIDE.md)

## 🚀 Execution Queue (read first)

1. Beta — fix ISSUE-001 (multi-overlay shutdown crash)
2. Beta — fix ISSUE-002 (config race)
3. Beta — fix ISSUE-003 (RenderResources thread safety)
4. Beta — fix ISSUE-004 (config persistence)
5. Phase 13 — continue data validation

## Status legend
`[ ]` Not started · `[~]` In progress · `[x]` Done

---

## MVP (Phases 0–6) — Complete
All archived to `docs/archive/mvp/tasks/`.

## Alpha — Phases 7–11 — Complete
All archived to [`docs/archive/alpha/tasks/`](../archive/alpha/tasks/README.md).

---

## Active

| Phase | Status | File | Summary |
|---|---|---|---|
| Beta — Stability & Usability | `[~]` | [PHASE-BETA-stability-and-usability.md](PHASE-BETA-stability-and-usability.md) | Stabilization for long sessions and real usage |
| 13 — Data Validation & Audit | `[~]` | [PHASE-13-data-validation.md](PHASE-13-data-validation.md) | Audit every field in every overlay against iRacing + LMU SDK; fix wrong values |

## Upcoming

| Phase | Status | Summary |
|---|---|---|
| 12 — OBS Mode & Enhanced UX | `[ ]` | Deferred — spec in [PHASE-12-obs-and-ux.md](PHASE-12-obs-and-ux.md) |
| 14 — WebSocket Server | `[ ]` | Live telemetry over WebSocket for custom HTML widgets |
| 15 — Field Visibility & Layout | `[ ]` | Per-overlay column show/hide/reorder |
| 16 - Cross-sim Normalization | `[ ]` | Spec: [PHASE-16-cross-sim-normalization.md](PHASE-16-cross-sim-normalization.md) |
| 21 - Performance & Soak Validation | `[ ]` | Mid priority: [PHASE-21-performance-and-soak.md](PHASE-21-performance-and-soak.md) |
| 17 — Radar Overlay | `[ ]` | Top-down proximity radar |
| 18 — Map Overlay | `[ ]` | SVG track map + auto-trace |
| 19 — ACC Integration | `[ ]` | Assetto Corsa Competizione sim provider |
| 20 — Session-State Visibility | `[ ]` | Auto show/hide overlays by session phase |
