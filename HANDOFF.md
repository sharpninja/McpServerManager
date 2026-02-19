# Session Handoff — 2026-02-19

## Project Overview
**RequestTracker** is an Avalonia UI desktop app (.NET 9) for viewing and analyzing AI agent session logs. It connects to an MCP server to fetch session data and displays it in a JSON tree, search index grid, and structured details view. Includes an integrated AI chat window (Ollama-backed).

## Repository Structure
- `src/RequestTracker/` — Main app (Avalonia, MVVM with CommunityToolkit.Mvvm)
  - `Views/` — AXAML views + code-behind (`MainWindow`, `ChatWindow`, `RequestDetailsView`)
  - `ViewModels/` — `MainWindowViewModel` (2700+ lines, primary logic), `ChatWindowViewModel`
  - `Models/Json/` — `UnifiedJsonModel.cs` (data model for entries, actions, processing dialog)
  - `Services/` — `McpSessionLogService`, `OllamaLogAgentService`, `BooleanSearchParser`
  - `Converters/` — XAML value converters
- `lib/Markdown.Avalonia/` — Git submodule for markdown rendering
- `publish/win-x64/` — Published binaries

## Key Architecture
- **MVVM pattern** with `[ObservableProperty]` and `[RelayCommand]` source generators
- **Three view modes** controlled by `IsJsonVisible`, `IsMarkdownVisible`, `IsRequestDetailsVisible` boolean flags (not tabs)
- **MCP auto-refresh**: 10-second timer refreshes MCP nodes via `GenerateAndNavigate()`
- **Config**: `appsettings.config` (JSON) for paths and MCP base URL
- **Layout persistence**: `LayoutSettingsIo` saves/restores window size, position, splitter heights

## Changes Made This Session

### 1. Chat Auto-Scroll (`ChatWindow.axaml.cs`)
- Subscribes to `Messages.CollectionChanged` on window open
- Listens to `PropertyChanged` on assistant messages for streaming updates
- `ScrollToEnd()` posts scroll via `Dispatcher.UIThread` at `Background` priority
- Unsubscribes on window close

### 2. Preserve Details View on Refresh (`MainWindowViewModel.cs`)
- `GenerateAndNavigate()` uses `preserveDetailsView` flag: only resets `IsRequestDetailsVisible` when navigating to a *different* node
- When preserving, skips setting `IsJsonVisible = true` so the details panel stays visible
- Notifies nav commands (`NavigateToPrevious/NextRequestCommand`) on refresh

### 3. Nav Button State Fix (`MainWindowViewModel.cs`)
- `GetCurrentRequestIndexInFilteredList()` now falls back to `RequestId` string match when reference equality fails (entries are rebuilt on refresh)
- `UpdateFilteredSearchEntries()` re-binds `SelectedUnifiedRequest` to the matching new instance after data reload

### 4. Timestamp Formatting (`UnifiedJsonModel.cs`, `RequestDetailsView.axaml`)
- Added `TimestampDisplay` property to `UnifiedProcessingDialogItem` — parses raw string and formats with `"g"` (short date/time, local time)
- Widened timestamp column from 130 → 160
- Bound to `TimestampDisplay` instead of raw `Timestamp`

### 5. Boolean Search (`BooleanSearchParser.cs`, `MainWindowViewModel.cs`)
- Recursive descent parser supporting `||` (OR), `&&` (AND), `!` (NOT), parentheses, quoted strings
- Returns `Func<string, bool>` predicate; applied to all searchable fields per entry
- Plain text without operators works as simple substring match (backward compatible)

### 6. Details View Layout (`RequestDetailsView.axaml`)
- All 8 expanders set to `IsExpanded="True"`
- Section order: Query → Interpretation → Response → **Processing Dialog** → Actions → Context → Tags & Notes → Original JSON

## Build Notes
- **Solution**: `RequestTracker.slnx`
- **Build**: `dotnet build` from repo root
- **Running instance locks build outputs** — kill the `RequestTracker` process before rebuilding
- File-lock errors (MSB3027/MSB3021) are not compilation errors; check for `error CS` to confirm compile success
- The `lib/Markdown.Avalonia` submodule targets net6 but is consumed by the net9 app

## Current State
- All changes committed and pushed to `main` (HEAD: `b878e51`)
- No uncommitted changes
- Build succeeds with 0 warnings, 0 errors (when no running instance locks files)
