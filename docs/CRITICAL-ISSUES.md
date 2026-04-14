# Critical Reliability Issues

This document tracks issues that can cause crashes, hangs, silent data corruption, or long-session instability.

## 2026-04-13 Reliability Hardening

| ID | Area | Severity | Status | Summary |
|---|---|---|---|---|
| CRIT-001 | SimDetector concurrency | P1 | Fixed | Added synchronization for provider state snapshots and active-provider access; prevented concurrent poll re-entry. |
| CRIT-002 | LMU poll timer reentry | P1 | Fixed | Added non-reentrant poll guard so 16 ms timer callbacks cannot overlap and race shared state. |
| CRIT-003 | Debounced config saves | P1 | Fixed | Wrapped timer save callbacks and `ConfigStore.Save` in exception boundaries; failed writes are logged, not process-fatal. |
| CRIT-004 | LMU fuel fallback | P2 | Fixed | Replaced null-capacity fallback bug (`0` liters) with last-known-capacity/last-known-fuel fallback logic. |
| CRIT-005 | Single-instance IPC window init | P2 | Fixed | Added Win32 return-value checks for `RegisterClassEx` and `CreateWindowEx` with explicit startup errors. |
| CRIT-006 | Tray fallback icon resource leak | P2 | Fixed | Fixed GDI handle leak (`DestroyIcon`) and undisposed font allocation in fallback icon path. |
| CRIT-007 | Hotkey registration observability | P3 | Fixed | Added hotkey registration failure warnings and guarded unregister calls. |
| CRIT-008 | Track map label-collision logic | P3 | Fixed | Corrected dead condition in label overlap logic; now tracks collision per row. |
| CRIT-009 | LMU shared-memory stale handle lifecycle | P1 | Fixed | LMU reader now periodically rebinds and closes-on-read-fault so stale self-held MMF handles cannot mask disconnects after LMU exit. |

## Reliability Gate

For public alpha sharing, the project must satisfy:

- No known unhandled-exception paths on background timers/poll loops.
- No known persistent native handle leaks in normal startup/shutdown loops.
- No known reentrant timer races on mutable runtime state.
- No known silent fallback paths that publish clearly wrong telemetry values.
