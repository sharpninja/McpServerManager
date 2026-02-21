# Session Handoff — 2026-02-20 (Session 2)

## Project Overview
**McpServerManager** is an Avalonia UI application (.NET 9) for viewing and analyzing AI agent session logs. It connects to an MCP server (FunWasHad project at `localhost:7147`) to fetch session data and TODO items, displaying them in a JSON tree, search index grid, structured details view, and a full TODO management tab with an integrated YAML editor. Includes an integrated AI chat window (Ollama-backed, Desktop only). Runs on Desktop (Windows/Linux) and Android (tablet + phone).

## Repository Structure
- `src/McpServerManager.Core/` — Shared library (net9.0): ViewModels, Models, Services, Commands, CQRS
- `src/McpServerManager.Desktop/` — Desktop app (net9.0 WinExe, Avalonia)
- `src/McpServerManager.Android/` — Android app (net9.0-android, Avalonia)
- `src/McpServerManager/` — Legacy standalone desktop app (pre-refactor, still builds)
- `lib/Markdown.Avalonia/` — Git submodule for markdown rendering
- `docs/` — Documentation: toc.yml, todo.md, EXCEPTION-EVALUATION.md, fdroid/
- `todo.yaml` — Project backlog in YAML format

## Key Architecture
- **MVVM pattern** with `[ObservableProperty]` and `[RelayCommand]` source generators (CommunityToolkit.Mvvm 8.2.1)
- **CQRS** via project's own `Mediator` class with `ICommand<T>`/`IQuery<T>` + handlers
- **3-project architecture**: Core (shared lib) → Desktop + Android. Views in platform projects, ViewModels in Core.
- **TabControl shell**: Desktop `MainWindow` and Android `TabletMainView` use TabControl with 3 tabs: "Request Tracker", "Todos", "Logs"
- **Status bar**: Lives in `MainWindow.axaml` (Desktop) and `TabletMainView.axaml` (Android) — below the TabControl, not inside individual views
- **MCP server scope**: The MCP at `localhost:7147` belongs to FunWasHad, not this project. McpServerManager is a read/write client.
- **Config**: `appsettings.config` (JSON) for `Mcp.BaseUrl`, `Paths.SessionsRootPath`, `Paths.HtmlCacheDirectory`. Android has no appsettings.config — uses `ConnectionDialogView` to get MCP URL at startup (default `10.0.2.2:7147`).
- **Layout persistence**: `LayoutSettingsIo` saves/restores window size, position, splitter heights, chat window state
- **Logging**: `AppLogService` singleton implements `ILoggerFactory`/`ILoggerProvider`. All logging uses `ILogger` via `AppLogService.Instance.CreateLogger("Category")`. Logs feed `LogViewModel` for the Logs tab.
- **ConfigureAwait(true)**: Used everywhere (not false). This is a UI app — always continue on captured context.
- **SelectableTextBlock**: All `TextBlock` controls have been replaced with `SelectableTextBlock` across all 3 platforms.

## Changes Made This Session (Session 2)

### Commits (oldest → newest)
1. **75edd98** — `feat: add ILogger infrastructure, Logs tab, replace all Console/Debug logging`
2. **249c71e** — `feat: move status bar to main view, replace TextBlock with SelectableTextBlock, add Copilot CLI menu`
3. **f20c34f** — `fix: Android TODO loading - use stored mcpBaseUrl for TodoViewModel, reduce timeout`
4. **74e099e** — `refactor: replace all ConfigureAwait(false) with ConfigureAwait(true)`
5. **69d4a96** — `refactor: demote IsBusy logging to Debug, default log filter to Information`
6. **2a721c7** — `feat: update global status bar on todo load/open/save via GlobalStatusChanged event`

### Major Features Added

#### ILogger Infrastructure (`Core/Services/AppLogService.cs`)
- `AppLogService` singleton implements `ILoggerFactory` and `ILoggerProvider`
- `AppLogger` (per-category) and `AppLogger<T>` (generic) classes
- `LogEntry` model with `Display` property formatting `[HH:mm:ss.fff] [Level] [Source] Message`
- `NewLogEntry` event fires for each log entry → consumed by `LogViewModel`
- Added `Microsoft.Extensions.Logging.Abstractions v9.0.3` to Core .csproj

#### Logs Tab (`Core/ViewModels/LogViewModel.cs`, Desktop+Android `Views/LogView.axaml`)
- Full log viewer with level filter dropdown (default: Information)
- Pause/Resume toggle — paused entries buffer in `_pauseBuffer`, flushed on resume
- Auto-select newest entry, auto-scroll to bottom
- Context menu: Copy, Copy All, Clear (auto-pauses while context menu open)
- Monospace font (Cascadia Code, 14px), line-by-line display
- Created on both Desktop and Android platforms

#### Status Bar Moved to Main View
- **Desktop**: `MainWindow.axaml` wraps TabControl in `Grid RowDefinitions="*,Auto"`, status bar in row 1
- **Android**: `TabletMainView.axaml` same pattern with `AnimatedStatusBar`
- Removed from `McpServerManagerView.axaml` and `McpServerManagerTabletView.axaml`
- Updated `SaveCurrentLayoutToSettings` row count checks (portrait ≥5, landscape ≥3)

#### Todo Global Status Events
- `TodoListViewModel.GlobalStatusChanged` event fires on load/open/save with descriptive messages
- `MainWindowViewModel` subscribes in `CreateTodoViewModel()` factory, forwards to `StatusMessage`

#### Copilot CLI Commands (`Core/ViewModels/TodoListViewModel.cs`)
- 3 new `[RelayCommand]` methods: `CopilotStatusAsync`, `CopilotPlanAsync`, `CopilotImplementAsync`
- `RunCopilotCommandAsync` helper streams output line-by-line into editor via `CopilotCliService`
- `IsCopilotRunning` observable property for UI state
- Context menu items added to Desktop `TodoListView.axaml`

#### SelectableTextBlock Replacement
- All `TextBlock` controls replaced with `SelectableTextBlock` across 16 `.axaml` files on all 3 platforms

#### Android Fixes
- **TODO loading**: `TodoViewModel` was using `AppSettings.ResolveMcpBaseUrl()` which throws on Android (no appsettings.config). Fixed to use `_mcpBaseUrl` stored from constructor parameter.
- **McpTodoService timeout**: Reduced from 30s to 5s to prevent ANR on connection failure
- **Ollama guard**: `TryStartOllamaIfNeeded()` in `AsyncCommands.cs` now checks OS platform (Windows/Linux/macOS only)
- **TODO auto-load timing**: Added `OnDataContextChanged` fallback in Android `TodoListView.axaml.cs` for when `Loaded` fires before `DataContext` is set
- **Editor always visible**: Removed `IsEditorVisible` toggling — editor panel and toolbar always shown

#### Log Level Audit (multiple passes)
- IsBusy state changes → Debug
- Window layout, persisting/reading data, AI messages → Information
- Navigation failures → Warning
- Non-fatal settings issues → Warning (demoted from Error)
- Default log viewer filter → Information

#### Other Changes
- All `ConfigureAwait(false)` → `ConfigureAwait(true)` across 12 files (53 occurrences)
- Graceful Pandoc handling: `IsPandocAvailable()` static check with caching
- Removed manual `InitializeComponent()` from TodoListView, LogView, and McpServerManagerView (was shadowing Avalonia's source-generated version)
- Todo list item template: ID prominent (SemiBold, larger), no checkboxes or priority display
- Log font size increased from 12 to 14 on both Desktop and Android

## Build Notes
- **Desktop**: `dotnet build src\McpServerManager.Desktop\McpServerManager.Desktop.csproj`
- **Android (emulator x64)**: `dotnet build src\McpServerManager.Android\McpServerManager.Android.csproj -t:Install -f net9.0-android -c Debug -p:AdbTarget="-s emulator-5554" -p:RuntimeIdentifier=android-x64`
- **Android launch**: `adb -s emulator-5554 shell am force-stop ninja.thesharp.mcpservermanager && adb -s emulator-5554 shell am start -n ninja.thesharp.mcpservermanager/crc64f9c6b05aaee59f0e.MainActivity`
- **CRITICAL**: The Android emulator (emulator-5554) is **x86_64**. Must use `-p:RuntimeIdentifier=android-x64` or Fast Deployment puts assemblies in `arm64-v8a` which the emulator can't find → instant crash.
- **Running instance locks DLLs** — kill the McpServerManager process before rebuilding (`Stop-Process -Id <PID>`)
- Avalonia 11.3.12, FluentAvaloniaUI 2.4.1 (2.5.0 needs .NET 10), AvaloniaEdit 11.0.0, CommunityToolkit.Mvvm 8.2.1
- Markdown.Avalonia submodule needs Linux CI patching (`.props` only defines PackageTargetFrameworks for Windows_NT)

## Current State
- Desktop and Android builds succeed with 0 errors
- All 6 commits from this session are on `main` branch (not pushed to origin)
- Android app deployed and running on emulator-5554
- PhoneMainView NOT modified (no tabs on phone)
- AI chat button wired on Desktop only (Android does not have ChatWindow)
- Submodule `lib/Markdown.Avalonia` shows as modified (dirty) — not a real change

## Known Issues / Next Steps
- **ANR on Android startup**: Initial MCP session loading (213 requests / 37 sessions) saturates the UI thread during tree building. The HTTP fetching is async/background, but `Dispatcher.UIThread.InvokeAsync` for the tree node population blocks. Consider batching tree updates or deferring until the tab is visible.
- **View unification** (RT-001): Desktop and Android views are separate — could share more XAML
- **Copilot CLI context menu** only on Desktop — not wired on Android
- **Android editor**: Always visible but `OnGroupListBoxSelectionChanged` calls `OpenSelectedTodoCommand` which fetches from MCP. Verify this works reliably on slower connections.
- **Push to origin**: 6 commits on `main` not yet pushed

## Connected Devices
- `emulator-5554` — Android tablet emulator (x86_64, 1600x2560)
- `ZD222QH58Q` — Physical Android device (not used this session)
