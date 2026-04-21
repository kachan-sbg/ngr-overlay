# Beta — Stability & Usability

Goal: make the app stable for long sessions and reliable enough for real racing use.

## Status
`[~]` In progress

## Scope
- crash prevention
- threading correctness
- overlay/window lifecycle stability
- config persistence reliability
- long-session usability
- data correctness required for confident use

## Execution order
1. Fix ISSUE-001 — multi-overlay shutdown crash risk (`PostQuitMessage` in every overlay `WM_DESTROY`)
2. Fix ISSUE-002 — cross-thread `_config` mutation in `BaseOverlay`
3. Fix ISSUE-003 — `RenderResources` thread safety
4. Fix ISSUE-004 — persist overlay position/size with debounce
5. Continue Phase 13 field validation after items 1–4 are done

## Exit criteria
- app can run through long sessions without overlay shutdown crashes
- no known unsafe shared mutable config path remains in active render/update flow
- render resource invalidation is thread-safe
- overlay move/resize persists correctly across restart
- Phase 13 validation can continue on a stable base

## Notes
Beta is a stabilization gate before more feature work. New feature phases stay queued unless they unblock Beta or are explicitly reprioritized.
