# Debug Runbook

## Purpose

Repeatable steps for diagnosing startup hangs/crashes and iRacing SDK interaction issues.

## Primary logs

- NrgOverlay: `%APPDATA%\NrgOverlay\sim-overlay.log`
- iRacing: `C:\Users\<user>\Documents\iRacing\iRacingSim64DX11__*.log`

## Quick clean start

1. Stop NrgOverlay.
2. Delete `%APPDATA%\NrgOverlay\sim-overlay.log` and `.bak`.
3. Start NrgOverlay.
4. Reproduce once with a single scenario.

## iRacing lifecycle debug flags

Set in the shell before launching NrgOverlay:

```powershell
$env:NRGOVERLAY_DEBUG_TRACE_IRACING_LIFECYCLE='1'
$env:NRGOVERLAY_DEBUG_DISABLE_IRACING_WATCHDOG_RESTART='1'  # optional isolation
$env:NRGOVERLAY_DEBUG_DISABLE_IRACING_FORCED_GC='1'         # optional isolation
```

Recommended production-like diagnostics:
- Enable only `NRGOVERLAY_DEBUG_TRACE_IRACING_LIFECYCLE=1`
- Leave other two flags unset

## Scenario matrix

Run each scenario separately and keep logs for each run:

1. NrgOverlay starts first, then iRacing starts.
2. iRacing starts first, then NrgOverlay starts.
3. iRacing closes while NrgOverlay stays open.
4. iRacing restarts while NrgOverlay stays open.
5. NrgOverlay closes while iRacing is running.
6. NrgOverlay + iRacing + iOverlay all running together (contention case).

## iOverlay coexistence note

iOverlay and NrgOverlay both consume iRacing SDK/shared-memory resources. This should be supported, but it increases timing contention risk during startup/shutdown. For this case:

- Prefer production-like trace-only mode first.
- Compare with iOverlay disabled to isolate interaction effects.
- Correlate timestamps across all logs for the same 30-60 second window.

## What to look for in NrgOverlay log

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

