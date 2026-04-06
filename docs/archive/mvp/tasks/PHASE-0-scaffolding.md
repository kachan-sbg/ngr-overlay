# Phase 0 — Project Scaffolding `[x]`

> [← Index](INDEX.md)

---

**TASK-001** `[x]`
- **Title**: Create solution and project structure
- **Description**: Create the `.sln` file and all six `.csproj` files (`Core`, `Rendering`, `Sim.Contracts`, `Sim.iRacing`, `Overlays`, `App`). Set target framework to `net8.0-windows`. Add project references matching the dependency graph in ARCHITECTURE.md. Configure `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` globally in `Directory.Build.props`. Set `<PlatformTarget>x64</PlatformTarget>` globally (required for DirectX P/Invoke).
- **Acceptance Criteria**: `dotnet build SimOverlay.sln` completes with no errors. All project references are correct and no circular dependencies exist. The solution loads in Visual Studio 2022 without errors.
- **Dependencies**: None.

---

**TASK-002** `[x]`
- **Title**: Add NuGet dependencies
- **Description**: Add NuGet packages to appropriate projects. `SimOverlay.Rendering`: `Vortice.Direct2D1`, `Vortice.DirectComposition`, `Vortice.DXGI`, `Vortice.Direct3D11`. `SimOverlay.Sim.iRacing`: `IRSDKSharper`. `SimOverlay.App`: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`. `SimOverlay.Core`: `System.Text.Json` (already in .NET 8 BCL, no explicit package needed). Create `Directory.Packages.props` for central package version management.
- **Acceptance Criteria**: All packages restore successfully. No version conflicts. `dotnet restore` exits 0.
- **Dependencies**: TASK-001.

---

**TASK-003** `[x]`
- **Title**: Configure global build properties and CI
- **Description**: Create `Directory.Build.props` with shared properties: nullable, implicit usings, platform target, treat-warnings-as-errors for non-test projects. Create a `.editorconfig` with C# style rules consistent with project conventions. Add a basic `build.yml` GitHub Actions workflow that builds the solution.
- **Acceptance Criteria**: `dotnet build` with `-warnaserror` completes cleanly on a fresh clone. `.editorconfig` is recognized by the IDE.
- **Dependencies**: TASK-001.

---

**TASK-004** `[x]`
- **Title**: Create `docs/` directory with architecture documents
- **Description**: Add `README.md`, `ARCHITECTURE.md`, `OVERLAYS.md`, and `TASKS.md` to the `docs/` folder in the repository root. Commit them as the authoritative design reference.
- **Acceptance Criteria**: All four documents are present and committed.
- **Dependencies**: TASK-001.
