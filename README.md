# McpServerManager

McpServerManager is an Avalonia UI desktop application designed to visualize and analyze session logs and request data from AI coding assistants like **GitHub Copilot** and **Cursor**. It provides a unified view of interactions, enabling developers to review prompt history, context usage, and automated actions taken by these tools.

Repository: [sharpninja/McpServerManager](https://github.com/sharpninja/McpServerManager)

## Key Features

*   **Unified Dashboard**: Aggregates logs from different AI providers (Copilot, Cursor) into a single, searchable interface.
*   **Tree Navigation**: filesystem watcher monitors a target directory for log files, automatically indexing them into a navigation tree.
*   **Detailed Request Analysis**:
    *   **Interpretation & Metadata**: Displays extracted metadata, user intent interpretation, and key decisions.
    *   **Context Inspection**: View the exact context (code snippets, files) sent to the LLM.
    *   **Action Tracking**: visualizes automated actions (file creation, edits, command execution) in a structured grid.
    *   **JSON Inspector**: Built-in JSON viewer to inspect the raw underlying data for deep debugging.
*   **Markdown Rendering**: Integrated `markdig`-based markdown viewer for rendering log content and associated documentation.
*   **Responsive UI**: Modern, light-theme interface with collapsible sections and persistent window state.

## Data Ingestion

The application monitors a specified directory (e.g., `docs/requests`) for JSON and Markdown log files. It supports custom schema formats used to track AI interactions:

*   **Copilot Logs**: JSON-based session logs containing request/response cycles, token usage metrics, and workspace context.
*   **Cursor Logs**: JSON/Markdown hybrid logs capturing "Tab" requests, diffs, and chat history.

## Usage

1.  **Configure Directory**: Point the application to the root folder containing your request logs.
2.  **Browse Sessions**: Use the tree view on the left to navigate through sessions organized by date or folder structure.
3.  **Inspect Requests**: Click on a specific request to view its details in the main panel.
    *   Expand the **Actions** grid to see what file changes were performed.
    *   Check **Interpretation** to understand how the AI understood the task.
    *   Use the **Original JSON** expander to see the raw data fields.

## Configuration

Edit `src/McpServerManager/appsettings.config` (copied next to the executable) to set `Mcp.BaseUrl`, `Paths.SessionsRootPath`, `Paths.HtmlCacheDirectory`, and `Paths.CssFallbackPath`. Session logs are loaded from `Mcp.BaseUrl` (`/mcpserver/sessionlog`), while `Paths.*` is still used for documents/source context and markdown CSS fallback. `Paths.HtmlCacheDirectory` supports environment variables such as `%TEMP%`.

## Tech Stack

*   **Framework**: Avalonia UI (Cross-platform .NET XAML framework)
*   **Language**: C# / .NET 8
*   **Parsing**: `System.Text.Json` with custom robust parsing for flexible schemas.
*   **Markdown**: `Markdig.Avalonia` for rendering formatted text.

## Build and run

```bash
# From repo root
cd src/McpServerManager
dotnet run
```

## Deployment automation

Use NUKE from the repo root as the authoritative build/deploy entry point.

```powershell
.\build.ps1
.\build.ps1 --target DeployAll
.\build.ps1 --target DeployAll --deploy-selection Director,WebUi,DesktopMsix
.\build.ps1 --target DeployAll --what-if
.\build.ps1 --target DeployAll --configuration Debug --deploy-selection Director,WebUi
.\build.ps1 BuildAndInstallVsix --what-if
```

Current target names:
- `Director`
- `WebUi`
- `AndroidPhone`
- `AndroidEmulator`
- `DesktopMsix`
- `DesktopDeb`

Behavior notes:
- `build.ps1` and `build.sh` are the primary entry points; they invoke `build\Build.csproj` with the repo root wired up for NUKE.
- When invoked with no arguments, the root wrappers forward `--help` so you see NUKE help instead of accidentally running a default target.
- For convenience, the wrappers treat the first bare argument as `--target`, so commands like `.\build.ps1 BuildAndInstallVsix --what-if` work without spelling out `--target`.
- The build is best-effort for deploy-all: unavailable targets are skipped and reported in the final summary.
- `--what-if` is the standard dry-run mechanism for NUKE-backed targets.
- `DesktopMsix` deployment auto-elevates only the certificate trust/install step through `gsudo`, avoiding elevated NUKE re-entry log-file conflicts; otherwise the build fails with guidance to install `gsudo` or rerun from an elevated PowerShell session.
- `DesktopDeb` installation on Windows launches an interactive WSL `sudo` prompt so the user can enter their password when package installation is requested.
- Legacy files under `scripts\` now act as compatibility wrappers so existing commands continue to work while NUKE owns the orchestration logic.
- For independent target execution, import `scripts\DeployAllTargets.psm1` and call the exported compatibility functions directly, for example `Invoke-DeployDirectorTool -Configuration Debug -WhatIf`.

### WSL with WSLg

On WSL with WSLg enabled (Windows 11), the app window should appear on the Windows desktop. If it doesn’t:

1. **Check WSLg**: Ensure you’re on Windows 11 with WSLg (no separate X server needed).
2. **Run from project**: `cd src/McpServerManager && dotnet run -c Debug`
3. **Or use the script**: From repo root, `chmod +x run-wslg.sh && ./run-wslg.sh`
4. **Taskbar**: The window may show in the Windows taskbar; click it to bring it to front.

