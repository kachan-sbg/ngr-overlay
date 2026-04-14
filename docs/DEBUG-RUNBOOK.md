# Debug Runbook

## Purpose

Repeatable steps for diagnosing startup hangs/crashes and iRacing SDK interaction issues.

## Primary logs

- SimOverlay: `%APPDATA%\SimOverlay\sim-overlay.log`
- iRacing: `C:\Users\<user>\Documents\iRacing\iRacingSim64DX11__*.log`

## Quick clean start

1. Stop SimOverlay.
2. Delete `%APPDATA%\SimOverlay\sim-overlay.log` and `.bak`.
3. Start SimOverlay.
4. Reproduce once with a single scenario.

## iRacing lifecycle debug flags

Set in the shell before launching SimOverlay:

```powershell
$env:SIMOVERLAY_DEBUG_TRACE_IRACING_LIFECYCLE='1'
$env:SIMOVERLAY_DEBUG_DISABLE_IRACING_WATCHDOG_RESTART='1'  # optional isolation
$env:SIMOVERLAY_DEBUG_DISABLE_IRACING_FORCED_GC='1'         # optional isolation
```

Recommended production-like diagnostics:
- Enable only `SIMOVERLAY_DEBUG_TRACE_IRACING_LIFECYCLE=1`
- Leave other two flags unset

## Scenario matrix

Run each scenario separately and keep logs for each run:

1. SimOverlay starts first, then iRacing starts.
2. iRacing starts first, then SimOverlay starts.
3. iRacing closes while SimOverlay stays open.
4. iRacing restarts while SimOverlay stays open.
5. SimOverlay closes while iRacing is running.
6. SimOverlay + iRacing + iOverlay all running together (contention case).

## iOverlay coexistence note

iOverlay and SimOverlay both consume iRacing SDK/shared-memory resources. This should be supported, but it increases timing contention risk during startup/shutdown. For this case:

- Prefer production-like trace-only mode first.
- Compare with iOverlay disabled to isolate interaction effects.
- Correlate timestamps across all logs for the same 30-60 second window.

## What to look for in SimOverlay log

- `SimDetector: ... -> Available / Disconnecting / confirmed disconnected`
- `IRacingProvider starting/stopping`
- `IRacingPoller debug toggles: ...`
- `IRacingPoller lifecycle: Dispose stop wait ...`
- `IRacingPoller lifecycle: ... forced GC handle release ...`
- any `ERROR`/`UNHANDLED EXCEPTION` lines

## Pass criteria for a healthy cycle

- No unhandled exceptions.
- No repeated watchdog restart storms.
- Clean attach (`Available -> activating -> InSession`) and clean detach (`Connected -> Disconnecting -> Disconnected`).
- Driver/session fields populate within a few seconds after attach.
