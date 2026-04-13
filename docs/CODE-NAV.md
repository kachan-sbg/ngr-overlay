# CODE-NAV — Feature-to-file navigation

> Use this instead of scanning multiple files. Find the task type, read only the files listed.

---

## Adding / modifying a telemetry field
1. **DTO**: `src/SimOverlay.Sim.Contracts/<Name>Data.cs` (or `<Name>Entry.cs`)
2. **iRacing source**: `src/SimOverlay.Sim.iRacing/IRacingPoller.cs` → `Publish<Name>Data()`
   - Use `SafeGetFloat` / `SafeGetInt` helpers — never call `data.GetFloat()` directly
3. **LMU source**: `src/SimOverlay.Sim.LMU/LmuPoller.cs`
4. **Render**: `src/SimOverlay.Overlays/<Name>Overlay.cs`

## Relative / standings display
| Concern | File |
|---|---|
| Gap calc, sort, garage inclusion | `src/SimOverlay.Sim.iRacing/IRacingRelativeCalculator.cs` |
| Relative render | `src/SimOverlay.Overlays/RelativeOverlay.cs` |
| Standings render | `src/SimOverlay.Overlays/StandingsOverlay.cs` |
| Relative DTO | `src/SimOverlay.Sim.Contracts/RelativeEntry.cs`, `RelativeData.cs` |
| Standings DTO | `src/SimOverlay.Sim.Contracts/StandingsEntry.cs`, `StandingsData.cs` |
| Driver session info (YAML) | `src/SimOverlay.Sim.iRacing/IRacingSessionDecoder.cs` |

## iRacing connection / reconnection
| Concern | File |
|---|---|
| Provider lifecycle (Start/Stop) | `src/SimOverlay.Sim.iRacing/IRacingProvider.cs` |
| 60 Hz poll loop + watchdog restart | `src/SimOverlay.Sim.iRacing/IRacingPoller.cs` |
| Session YAML decode → DTOs | `src/SimOverlay.Sim.iRacing/IRacingSessionDecoder.cs` |
| Fuel rolling average | `src/SimOverlay.Sim.iRacing/FuelConsumptionTracker.cs` |
| Intermediate types | `src/SimOverlay.Sim.iRacing/DriverSnapshot.cs`, `TelemetrySnapshot.cs` |

## Rendering / window
| Concern | File |
|---|---|
| Win32 HWND + ULW presentation | `src/SimOverlay.Rendering/OverlayWindow.cs` |
| 60 fps render loop base class | `src/SimOverlay.Rendering/BaseOverlay.cs` |
| Brush / font / layout cache | `src/SimOverlay.Rendering/RenderResources.cs` |
| Z-order WinEvent hook | `src/SimOverlay.Rendering/ZOrderHook.cs` |
| P/Invoke declarations | `src/SimOverlay.Rendering/Win32/NativeMethods.cs` |

⚠ **Window style rules**: `WS_EX_LAYERED` required; `WS_EX_NOREDIRECTIONBITMAP` + `WS_EX_TOOLWINDOW` must be **omitted**.

## Config / settings
| Concern | File |
|---|---|
| Root config schema | `src/SimOverlay.Core/Config/AppConfig.cs` |
| Per-overlay config + `Resolve()` | `src/SimOverlay.Core/Config/OverlayConfig.cs` |
| Load / save / atomic write | `src/SimOverlay.Core/Config/ConfigStore.cs` |
| Migration pipeline | `src/SimOverlay.Core/Config/ConfigMigrator.cs` |
| Stream override | `src/SimOverlay.Core/Config/StreamOverrideConfig.cs` |
| Settings WPF window | `src/SimOverlay.App/Settings/SettingsWindow.xaml.cs` |
| Per-overlay settings panel | `src/SimOverlay.App/Settings/OverlaySettingsPanel.xaml.cs` |

## App wiring
| Concern | File |
|---|---|
| DI composition root + entry | `src/SimOverlay.App/Program.cs` |
| Overlay lifecycle (create/destroy) | `src/SimOverlay.App/OverlayManager.cs` |
| **Add a new overlay**: write class → register | `src/SimOverlay.App/OverlayFactory.cs` |
| Sim detection (2 s poll) | `src/SimOverlay.App/SimDetector.cs` |
| Tray icon + context menu | `src/SimOverlay.App/TrayIconController.cs` |
| Pub/sub data bus | `src/SimOverlay.Core/SimDataBus.cs` |

## Overlay → file + DTOs
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

All overlay render files live in `src/SimOverlay.Overlays/`.

## Tests
| Suite | Path |
|---|---|
| Core (config, bus) | `tests/SimOverlay.Core.Tests/` |
| iRacing (calculator, decoder, fuel) | `tests/SimOverlay.Sim.iRacing.Tests/` |
| Overlay rendering | `tests/SimOverlay.Overlays.Tests/` |
| App integration | `tests/SimOverlay.App.Tests/` |
| Benchmarks (BenchmarkDotNet) | `tests/SimOverlay.Benchmarks/` — not a test runner |

## Docs
| Doc | Read when... |
|---|---|
| `docs/ARCHITECTURE.md` | Deep architecture questions — has section index at top, read targeted sections only |
| `docs/OVERLAYS.md` | Overlay column widths, colors, field layout specs |
| `docs/DECISIONS.md` | Quick decision log → full entries in `docs/decisions/alpha.md` |
| `docs/ROADMAP.md` | Phase priorities (13–20) |
| `docs/tasks/PHASE-13-data-validation.md` | Current active task details + acceptance criteria |
| `docs/archive/alpha/tasks/` | **Completed phase specs — read only for historical review** |
