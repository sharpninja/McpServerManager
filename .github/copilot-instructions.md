# Copilot Instructions — McpServerManager (RequestTracker)

## ⚠️ PRIORITY ORDER — NON-NEGOTIABLE ⚠️

**Speed is never more important than following workspace procedures.**

Before doing ANY work on ANY user request, you MUST complete these steps in order:

3. **POST `/mcp/sessionlog`** with your session entry — do NOT proceed until this succeeds
4. **GET `/mcp/sessionlog?limit=5`** to review recent session history for context
5. **GET `/mcp/todo`** to check current tasks
6. **THEN** begin working on the user's request

On EVERY subsequent user message:
1. POST an updated session log entry BEFORE starting work
2. Complete the user's request
3. POST the final session log with results, actions taken, and files modified

**If you skip any of these steps, STOP and go back and do them before continuing.**
Session logging is not optional, not deferred, and not secondary to the task.

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
