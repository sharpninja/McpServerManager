# Copilot Instructions — McpServerManager (RequestTracker)

See `AGENTS-README-FIRST.yaml` in the workspace root for MCP server connection details, session logging procedures, and agent conduct guidelines.

## Auth Architecture

- **JWT and API key are mutually exclusive.** Once a Bearer token is set on `McpClientBase`,
  API key writes are ignored and the client permanently requires the JWT.
- API keys are for **agents only** (no JWT capability).
- When a Bearer header is present, the server ignores API keys entirely for both
  auth AND workspace resolution.
- `McpClientBase.RequireBearerToken` prevents silent fallback once JWT is configured.

## Build Commands

```powershell
# Build desktop app
dotnet build src\McpServerManager.Desktop\McpServerManager.Desktop.csproj

# Build entire solution (requires android workload for Android project)
dotnet build McpServerManager.slnx

# Deploy to Android device (Motorola Edge)
dotnet build src\McpServerManager.Android\McpServerManager.Android.csproj -t:Install -f net9.0-android -c Debug -p:AdbTarget="-s ZD222QH58Q"

# Redeploy MCP Windows service (MUST use this script, never raw dotnet publish)
gsudo powershell -ExecutionPolicy Bypass -File lib\McpServer\scripts\Update-McpService.ps1
```

## Architecture

**McpServerManager** is an Avalonia cross-platform app (Desktop + Android) that connects
to an MCP Context Server for workspace management, TODO tracking, session logging, and
voice interaction.

- **McpServerManager.Core** — Shared library (net9.0): ViewModels, Services, Models
- **McpServerManager.Desktop** — Windows desktop app (WinExe, net9.0)
- **McpServerManager.Android** — Android app (net9.0-android)
- **lib/McpServer** — Git submodule (develop branch): the MCP server + client library

## Key Conventions

- Connection persistence: Android uses `SharedPreferences`, Desktop uses
  `%LOCALAPPDATA%\McpServerManager\connection.json`
- Every successful connection saves host/port — no conditional persistence flags
- Single-port model: all 6 workspaces share port 7147, resolved via `X-Workspace-Path` header
- FluentAvaloniaUI for navigation, AvaloniaEdit for text editing
- All services accept bearer token parameter and propagate it to `McpClientBase`

## MCP Service Deployment

**ALWAYS** use `lib\McpServer\scripts\Update-McpService.ps1` via gsudo.
Never use raw `dotnet publish`. The script archives config/data, publishes,
and restores preserved files. Verify all 6 workspaces are healthy after deploy.
