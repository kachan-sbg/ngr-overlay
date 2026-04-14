# PHASE 16 - Cross-sim Data Normalization

## Goal

Provide one normalized provider-facing model for rendering/UI so overlays do not need
per-sim field logic.

## Design Direction

Each sim adapter maps raw SDK fields into a shared normalized model:

- Input: iRacing SDK or LMU shared-memory fields
- Adapter: sim-specific mapper
- Output: common DTOs consumed by render/overlay layer

This preserves per-sim details while giving a stable UI contract.

## Priority normalization example (rating)

Introduce normalized rating fields that can render as a single "Rating" widget:

- iRacing source:
  - Skill: iRating (numeric)
  - Safety: License class + SR (for example `A 2.34`)
- LMU source:
  - Safety: `R0-R4`
  - Driver rating: `S1/S2/...` (or equivalent SDK representation)

Normalized display contract:
- `PrimaryRatingText` (for example `2345` or `S2`)
- `SafetyRatingText` (for example `A2.2` or `R2`)
- Optional display template (for example `[{Safety}] {Primary}`)

Example rendering outcomes:
- iRacing: `[A2.2] 2345`
- LMU: `[R2] S2`

## Scope

1. Define `NormalizedDriverRating` and related normalized fields in shared contracts.
2. Implement adapter mappers:
   - iRacing -> normalized
   - LMU -> normalized
3. Add fallback semantics:
   - If a source field is unavailable, show nothing (`null`/hidden), never fake `0`.
4. Add unit tests for mapping logic and display formatting.
5. Add per-overlay configuration for rating format template and visibility.

## Non-goals

- Forcing identical scoring semantics across sims.
- Replacing native values with estimated or synthetic values when API does not provide them.

## Acceptance Criteria

- Overlays read rating/safety from normalized fields only.
- iRacing and LMU both produce non-empty rating output when SDK data is available.
- Missing SDK data results in hidden/empty display, not misleading placeholder numbers.
- Mapping behavior is covered by tests with fixed example cases.

