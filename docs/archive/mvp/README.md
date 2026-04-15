# NrgOverlay вЂ” MVP Archive

This directory contains the documentation from the MVP phase (Phases 0вЂ“6), completed 2026-04-06.

These files are preserved for historical reference. Active development documentation is in the parent `docs/` directory.

## Contents

| File | Description |
|---|---|
| `KNOWN_ISSUES.md` | All tracked issues from MVP (ISSUE-001 through ISSUE-016). Most fixed; remaining low-priority items carried forward to Alpha Phase 7. |
| `VERIFICATION.md` | Phase-by-phase verification checklists used during MVP development. |
| `PROJECT.md` | Original project overview and goals document. |
| `OVERLAYS-MVP.md` | Overlay specifications for the 3 MVP overlays (Relative, Session Info, Delta Bar). |
| `tasks/` | Per-phase task files (PHASE-0 through PHASE-6) with acceptance criteria and completion status. |
| `testing/` | Manual test checklists for overlay rendering and Settings UI. |

## MVP Phases (all complete)

| Phase | Focus | Tasks |
|---|---|---|
| 0 вЂ” Scaffolding | Solution, projects, NuGet, docs | TASK-001 to TASK-004 |
| 1 вЂ” Rendering core | SimDataBus, ConfigStore, D2D window, render loop | TASK-101 to TASK-108 |
| 2 вЂ” iRacing data | MMF connection, 60 Hz poller, session decoder, relative calc | TASK-201 to TASK-205 |
| 3 вЂ” Overlay framework | Overlay manager, position persistence, live config, sim state | TASK-301 to TASK-304 |
| 4 вЂ” MVP overlays | Relative, Session Info, Delta Bar implementations | TASK-401 to TASK-405 |
| 5 вЂ” Settings UI | WPF settings window, per-overlay panels, tray icon | TASK-501 to TASK-504 |
| 6 вЂ” Polish | SimDetector, single-instance, icon, logging, benchmarks | TASK-601 to TASK-606 |

