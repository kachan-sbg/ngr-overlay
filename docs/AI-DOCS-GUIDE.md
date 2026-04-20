# AI Docs Loading Guide

This guide defines how AI tools should load project documentation with minimal context cost.

## Execution Priority (read first)

1. `docs/tasks/INDEX.md`
2. Process "Execution Queue" (top → bottom)
3. Then continue normal loading

Rules:
- Execution Queue overrides phase order
- Critical bugfixes come before feature work

## Goal

Load the fewest files needed to answer the current task correctly.

## Default Loading Order

1. `docs/README.md` for top-level routing.
2. One task file from `docs/tasks/` when the request is phase-specific.
3. One focused spec file (`OVERLAYS.md`, `ARCHITECTURE.md`, `CRITICAL-ISSUES.md`, or `FIELD-STATUS.md`) only if needed.

## Archive Policy

- `docs/archive/` is reference-only and not part of everyday context.
- Do not load archived docs for normal implementation, bugfix, or QA requests.
- Load archived docs only for historical review, migration/backport work, or full-project audits.

## File Design Rules

- Keep active docs small and topic-scoped.
- Use one canonical file per topic; avoid duplicating the same details across many files.
- Link to related docs instead of copying long sections.
- Move completed phase details to `docs/archive/` so active docs stay lean.

## Routing Rule

If the request can be answered from active docs, do not open archive files.
