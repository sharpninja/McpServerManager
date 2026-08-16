# McpServerManager

McpServerManager is the cross-platform management suite for MCP Server workspaces. It includes a desktop Avalonia application, the Director terminal UI/CLI, the browser and hybrid MCP Web experience, and Android support surfaces for workspace, TODO, session, template, health, auth, and triage workflows.

Repository: [sharpninja/McpServerManager](https://github.com/sharpninja/McpServerManager)

## Key Features

- **Shared UI Core**: common CQRS/ViewModel logic backs Desktop, Director, MCP Web, and Android experiences so workspace, TODO, session, template, search, health, auth, requirements, and triage behavior stays consistent.
- **Director CLI/TUI**: `director` provides command-line and Terminal.Gui access to MCP Server administration, authentication, workspace context, TODO selection, session logs, requirements, events, health, and triage.
- **MCP Web and Hybrid Host**: `mcp-web` is packaged as a global .NET tool that launches the Blazor Hybrid host while sharing the same Blazor source as the browser-hosted Web UI.
- **Workspace-Centered Navigation**: dashboards and detail pages honor the active workspace, persist compatible credentials, and reload page state when the workspace changes.
- **Triage Dashboard**: triage queues, report group queue, run history, triage-created TODOs, row selection, grouping, consolidation, resubmission, filtering, sorting, and cross-workspace TODO navigation are surfaced in MCP Web and Director.
- **Requirements and Documentation Export**: MCP-backed requirements remain the source of truth and can be exported to wiki-ready documentation artifacts.
- **Deployment Automation**: NUKE targets build, package, install, and deploy Director, Web UI, Android, desktop MSIX, desktop DEB, F-Droid artifacts, and supporting validation outputs.

## Main Applications

| Application | Project | Primary command |
| --- | --- | --- |
| Desktop | `src/McpServerManager.Desktop` | `dotnet run --project src/McpServerManager.Desktop/McpServerManager.Desktop.csproj` |
| Director | `src/McpServerManager.Director` | `director` |
| MCP Web Hybrid | `src/McpServerManager.Web.Hybrid` | `mcp-web` |
| Browser Web UI | `src/McpServerManager.Web` | `dotnet run --project src/McpServerManager.Web/McpServerManager.Web.csproj` |
| Android | `src/McpServerManager.Android` | deploy through NUKE Android targets |

## Configuration

Workspace marker files such as `AGENTS-README-FIRST.yaml` provide the MCP Server base URL, workspace path, and API key for local workspace operation. Shared authentication code can reuse cached workspace credentials when they are valid and deletes expired cached tokens before prompting for fresh authentication.

## Build and Validation

The solution targets .NET 10 and treats warnings as errors. Use focused builds and tests during development, and prefer the NUKE targets below for packaging and deployment.

```powershell
dotnet restore .\McpServerManager.sln -v minimal
dotnet build .\McpServerManager.sln --no-restore -v minimal -maxcpucount:1 /p:UseSharedCompilation=false
dotnet test .\McpServerManager.sln --no-restore -v minimal -maxcpucount:1 /p:UseSharedCompilation=false
```

## Deployment Automation

Use NUKE from the repo root as the authoritative build, package, and deploy entry point.

```powershell
.\build.ps1 --help
.\build.ps1 DeployAll
.\build.ps1 DeployAll --deploy-selection Director,WebUi,DesktopMsix
.\build.ps1 DeployAll --what-if
.\build.ps1 UpdateDirectorTool --skip-version-bump
.\build.ps1 UpdateWebUiTool --skip-version-bump
```

Current `DeployAll` selections:

- `Director`
- `WebUi`
- `AndroidPhone`
- `AndroidEmulator`
- `DesktopMsix`
- `DesktopDeb`

Behavior notes:

- `build.ps1` and `build.sh` invoke `build\Build.csproj` with the repo root wired up for NUKE.
- The wrappers treat the first bare argument as `--target`, so `.\build.ps1 DeployAll --what-if` works without spelling out `--target`.
- `UpdateDirectorTool` and `UpdateWebUiTool` create NuGet tool packages under `nupkg\` and install them globally; `WebUi` packages the hybrid app as the `mcp-web` tool.
- `DeployAll` reports unavailable targets as skipped when possible, but target failures still fail the overall run.
- `DesktopMsix` trusts the local certificate through `gsudo` when needed, then installs the package for the invoking desktop user.
- `DesktopDeb` packages through WSL and requires the selected WSL distribution to be available.
- `.github\workflows\build-android.yml` delegates versioning, Android packaging, release, F-Droid, and Pages assembly to NUKE targets.

Validation commands for deployment automation:

- `dotnet build build\Build.csproj -nologo`
- `dotnet run --project .\build\Build.csproj -- --target VersionInfo`
- `dotnet run --project .\build\Build.csproj -- --target DeployAll --what-if`

## WSL with WSLg

On WSL with WSLg enabled, the desktop app window should appear on the Windows desktop. If it does not:

1. Confirm WSLg is available on the host.
2. Run from the desktop project: `dotnet run --project src/McpServerManager.Desktop/McpServerManager.Desktop.csproj -c Debug`.
3. Use the Windows taskbar to bring the WSLg-hosted window forward if it launched behind another window.

