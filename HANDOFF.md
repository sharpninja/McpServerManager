# Session Handoff — 2026-02-20

## Project Overview
**RequestTracker** is an Avalonia UI application (.NET 9) for viewing and analyzing AI agent session logs. It connects to an MCP server (FunWasHad project at `localhost:7147`) to fetch session data and TODO items, displaying them in a JSON tree, search index grid, structured details view, and a full TODO management tab. Includes an integrated AI chat window (Ollama-backed). Runs on Desktop (Windows/Linux) and Android (tablet + phone).

## Repository Structure
- `src/RequestTracker.Core/` — Shared library (net9.0): ViewModels, Models, Services, Commands, CQRS
- `src/RequestTracker.Desktop/` — Desktop app (net9.0 WinExe, Avalonia)
- `src/RequestTracker.Android/` — Android app (net9.0-android, Avalonia)
- `src/RequestTracker/` — Legacy standalone desktop app (pre-refactor, still builds)
- `lib/Markdown.Avalonia/` — Git submodule for markdown rendering
- `docs/` — Documentation: toc.yml, todo.md, EXCEPTION-EVALUATION.md, fdroid/
- `todo.yaml` — Project backlog in YAML format

## Key Architecture
- **MVVM pattern** with `[ObservableProperty]` and `[RelayCommand]` source generators (CommunityToolkit.Mvvm 8.2.1)
- **CQRS** via project's own `Mediator` class with `ICommand<T>`/`IQuery<T>` + handlers
- **3-project architecture**: Core (shared lib) → Desktop + Android. Views in platform projects, ViewModels in Core.
- **ViewLocator convention**: `RequestTracker.Core.ViewModels.XxxViewModel` → `RequestTracker.Desktop.Views.XxxView`
- **TabControl shell**: Desktop MainWindow and Android TabletMainView use TabControl with "Request Tracker" and "Todos" tabs
- **MCP server scope**: The MCP at `localhost:7147` belongs to FunWasHad, not this project. RequestTracker is a read/write client.
- **Config**: `appsettings.config` (JSON) for `Mcp.BaseUrl`, `Paths.SessionsRootPath`, `Paths.HtmlCacheDirectory`
- **Layout persistence**: `LayoutSettingsIo` saves/restores window size, position, splitter heights, chat window state

## Changes Made This Session

### 1. Todo Entity Models (`Core/Models/McpTodoContracts.cs`)
- 7 classes matching MCP Todo API swagger: McpTodoQueryResult, McpTodoFlatItem, McpTodoFlatTask, McpTodoCreateRequest, McpTodoUpdateRequest, McpTodoMutationResult, McpRequirementsAnalysisResult

### 2. Todo HTTP Service (`Core/Services/McpTodoService.cs`)
- Full HTTP client for all 6 MCP `/mcp/todo` endpoints (list, get, create, update, delete, analyze requirements)

### 3. Todo CQRS Commands (`Core/Commands/TodoCommands.cs`)
- 2 queries (QueryTodos, GetTodoById) + 4 commands (Create, Update, Delete, AnalyzeRequirements), each with handlers

### 4. Todo ViewModel (`Core/ViewModels/TodoListViewModel.cs`)
- Full MVVM ViewModel with observable properties, CQRS dispatch, filtering, priority grouping
- New-todo inline form, editor support (open/save/refresh/close), font zoom
- AI Chat integration: `OpenAiChatRequested` event, `GetTodoContextForAgent()` builds context with current editor content + full todo list summary

### 5. Todo YAML Serialization (`Core/Services/TodoMarkdown.cs`)
- Ported from VS2026 extension (`McpServer.VsExtension.McpTodo.Vsix/TodoMarkdown.cs`)
- YAML front matter + markdown body round-trip: `ToMarkdown(McpTodoFlatItem)` ↔ `FromMarkdown(string)` → `McpTodoUpdateRequest`

### 6. Desktop TodoListView (`Desktop/Views/TodoListView.axaml` + `.axaml.cs`)
- Toolbar: New, Refresh, Copy ID, Stop
- Filter bar: priority combo, scope combo, text filter
- Grouped list with priority headers, double-tap to open editor
- AvaloniaEdit-based editor with toolbar: Save, Refresh, Cut, Copy, Paste, Zoom In/Out, **AI Chat**, Close
- Orientation-aware layout (landscape=side-by-side, portrait=stacked) with GridSplitter persistence
- AI button raises `OpenAiChatRequested` → MainWindow opens chat with todo context

### 7. Desktop TabControl Refactor (`Desktop/Views/MainWindow.axaml`)
- Extracted MainWindow content → `RequestTrackerView.axaml` UserControl
- MainWindow is now a TabControl shell with 2 tabs
- `MainWindow.axaml.cs` wires layout delegation, settings persistence, chat window, todo AI chat event

### 8. Android Todo UI (`Android/Views/TodoListView.axaml` + `.axaml.cs`)
- Mirrors Desktop with DynamicResource brushes and larger fonts
- `TabletMainView.axaml` wrapped in TabControl (phone UI unchanged)

### 9. Project Backlog (`todo.yaml`)
- Created project todo tracking file using FunWasHad's YAML format
- Open: RT-001 (unify views), RT-002 (Pandoc handling), RT-003 (auto-load), RT-004 (new-todo editor flow)
- Completed: RT-100–RT-104 (historical items migrated from ANALYSIS.md)

### 10. Documentation
- Deleted `ANALYSIS.md` (items migrated to todo.yaml as completed entries)
- Created `docs/toc.yml` — documentation table of contents
- Created `docs/todo.md` — docfx-compatible page rendering the todo backlog

## Build Notes
- **Solution**: `dotnet build RequestTracker.slnx` (full), or per-project
- **Desktop**: `dotnet build src\RequestTracker.Desktop\RequestTracker.Desktop.csproj`
- **Android**: `dotnet build src\RequestTracker.Android\RequestTracker.Android.csproj` (requires `android` workload)
- **Running instance locks DLLs** — kill the RequestTracker process before rebuilding
- Avalonia 11.3.12, FluentAvaloniaUI 2.4.1, AvaloniaEdit 11.0.0, CommunityToolkit.Mvvm 8.2.1
- Markdown.Avalonia submodule needs Linux CI patching (`.props` only defines PackageTargetFrameworks for Windows_NT)

## Current State
- Desktop build succeeds with 0 errors
- No uncommitted changes beyond this session's work
- PhoneMainView NOT modified (per user request — no tabs on phone)
- AI chat button wired on Desktop only (Android does not have ChatWindow)

## Known Issues / Next Steps
- Todo tab does not auto-load when first selected (RT-003)
- ListBox selection within grouped ItemsControl is per-group, not cross-group (RT-003)
- New Todo uses inline form, not YAML editor flow (RT-004)
- Pandoc "not found" not handled gracefully (RT-002)
- View unification across Desktop/Android not started (RT-001)
