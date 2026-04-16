# Project Memory

This file tracks practical execution notes for contributors and agents.

## Agent run behavior (as observed)
- Full solution commands are more likely to stall in this environment than targeted project runs.
- Run tests/builds serially per project instead of broad or parallel invocation.

### Known stalled-prone commands
- `dotnet build NrgOverlay.sln`
- repo-wide `dotnet test` without a specific `.csproj`

## Recommended verification workflow
1. Build or test one project at a time.
2. Prioritize iRacing-critical paths first:
   - `src/NrgOverlay.Sim.iRacing/NrgOverlay.Sim.iRacing.csproj`
   - `tests/NrgOverlay.Sim.iRacing.Tests/NrgOverlay.Sim.iRacing.Tests.csproj`
   - `tests/NrgOverlay.App.Tests/NrgOverlay.App.Tests.csproj`
3. If a run stalls in agent environment, rerun locally and attach logs/results.
