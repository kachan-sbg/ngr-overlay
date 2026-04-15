# CODE-NAV вЂ” Feature-to-file navigation

> Use this instead of scanning multiple files. Find the task type, read only the files listed.

---

## Adding / modifying a telemetry field
1. **DTO**: `src/NrgOverlay.Sim.Contracts/<Name>Data.cs` (or `<Name>Entry.cs`)
2. **iRacing source**: `src/NrgOverlay.Sim.iRacing/IRacingPoller.cs` в†’ `Publish<Name>Data()`
   - Use `SafeGetFloat` / `SafeGetInt` helpers вЂ” never call `data.GetFloat()` directly
3. **LMU source**: `src/NrgOverlay.Sim.LMU/LmuPoller.cs`
4. **Render**: `src/NrgOverlay.Overlays/<Name>Overlay.cs`

## Relative / standings display
| Concern | File |
|---|---|
| Gap calc, sort, garage inclusion | `src/NrgOverlay.Sim.iRacing/IRacingRelativeCalculator.cs` |
| Relative render | `src/NrgOverlay.Overlays/RelativeOverlay.cs` |
| Standings render | `src/NrgOverlay.Overlays/StandingsOverlay.cs` |
| Relative DTO | `src/NrgOverlay.Sim.Contracts/RelativeEntry.cs`, `RelativeData.cs` |
| Standings DTO | `src/NrgOverlay.Sim.Contracts/StandingsEntry.cs`, `StandingsData.cs` |
| Driver session info (YAML) | `src/NrgOverlay.Sim.iRacing/IRacingSessionDecoder.cs` |

## iRacing connection / reconnection
| Concern | File |
|---|---|
| Provider lifecycle (Start/Stop) | `src/NrgOverlay.Sim.iRacing/IRacingProvider.cs` |
| 60 Hz poll loop + watchdog restart | `src/NrgOverlay.Sim.iRacing/IRacingPoller.cs` |
| Session YAML decode в†’ DTOs | `src/NrgOverlay.Sim.iRacing/IRacingSessionDecoder.cs` |
| Fuel rolling average | `src/NrgOverlay.Sim.iRacing/FuelConsumptionTracker.cs` |
| Intermediate types | `src/NrgOverlay.Sim.iRacing/DriverSnapshot.cs`, `TelemetrySnapshot.cs` |

## Rendering / window
| Concern | File |
|---|---|
| Win32 HWND + ULW presentation | `src/NrgOverlay.Rendering/OverlayWindow.cs` |
| 60 fps render loop base class | `src/NrgOverlay.Rendering/BaseOverlay.cs` |
| Brush / font / layout cache | `src/NrgOverlay.Rendering/RenderResources.cs` |
| Z-order WinEvent hook | `src/NrgOverlay.Rendering/ZOrderHook.cs` |
| P/Invoke declarations | `src/NrgOverlay.Rendering/Win32/NativeMethods.cs` |

вљ  **Window style rules**: `WS_EX_LAYERED` required; `WS_EX_NOREDIRECTIONBITMAP` + `WS_EX_TOOLWINDOW` must be **omitted**.

## Config / settings
| Concern | File |
|---|---|
| Root config schema | `src/NrgOverlay.Core/Config/AppConfig.cs` |
| Per-overlay config + `Resolve()` | `src/NrgOverlay.Core/Config/OverlayConfig.cs` |
| Load / save / atomic write | `src/NrgOverlay.Core/Config/ConfigStore.cs` |
| Migration pipeline | `src/NrgOverlay.Core/Config/ConfigMigrator.cs` |
| Stream override | `src/NrgOverlay.Core/Config/StreamOverrideConfig.cs` |
| Settings WPF window | `src/NrgOverlay.App/Settings/SettingsWindow.xaml.cs` |
| Per-overlay settings panel | `src/NrgOverlay.App/Settings/OverlaySettingsPanel.xaml.cs` |

## App wiring
| Concern | File |
|---|---|
| DI composition root + entry | `src/NrgOverlay.App/Program.cs` |
| Overlay lifecycle (create/destroy) | `src/NrgOverlay.App/OverlayManager.cs` |
| **Add a new overlay**: write class в†’ register | `src/NrgOverlay.App/OverlayFactory.cs` |
| Sim detection (2 s poll) | `src/NrgOverlay.App/SimDetector.cs` |
| Tray icon + context menu | `src/NrgOverlay.App/TrayIconController.cs` |
| Pub/sub data bus | `src/NrgOverlay.Core/SimDataBus.cs` |

## Overlay в†’ file + DTOs
| Overlay | Render file | Primary DTOs |
|---|---|---|
| Relative | `RelativeOverlay.cs` | `RelativeData`, `RelativeEntry` |
| Standings | `StandingsOverlay.cs` | `StandingsData`, `StandingsEntry` |
| Session Info | `SessionInfoOverlay.cs` | `SessionData`, `WeatherData` |
| Delta Bar | `DeltaBarOverlay.cs` | `TelemetryData`, `DriverData` |
| Input Telemetry | `InputTelemetryOverlay.cs` | `TelemetryData` |
| Fuel Calculator | `FuelCalculatorOverlay.cs` | `TelemetryData`, `PitData` |
| Weather | `WeatherOverlay.cs` | `WeatherData` |
| Track Map | `TrackMapOverlay.cs` | `TrackMapData` |

All overlay render files live in `src/NrgOverlay.Overlays/`.

## Tests
| Suite | Path |
|---|---|
| Core (config, bus) | `tests/NrgOverlay.Core.Tests/` |
| iRacing (calculator, decoder, fuel) | `tests/NrgOverlay.Sim.iRacing.Tests/` |
| Overlay rendering | `tests/NrgOverlay.Overlays.Tests/` |
| App integration | `tests/NrgOverlay.App.Tests/` |
| Benchmarks (BenchmarkDotNet) | `tests/NrgOverlay.Benchmarks/` вЂ” not a test runner |

## Docs
| Doc | Read when... |
|---|---|
| `docs/ARCHITECTURE.md` | Deep architecture questions вЂ” has section index at top, read targeted sections only |
| `docs/OVERLAYS.md` | Overlay column widths, colors, field layout specs |
| `docs/DECISIONS.md` | Quick decision log в†’ full entries in `docs/decisions/alpha.md` |
| `docs/ROADMAP.md` | Phase priorities (13вЂ“20) |
| `docs/tasks/PHASE-13-data-validation.md` | Current active task details + acceptance criteria |
| `docs/archive/alpha/tasks/` | **Completed phase specs вЂ” read only for historical review** |

