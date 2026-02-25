# Inventory: Core ViewModel App Logic

Status: Phase 0 inventory baseline  
Scope: `src/McpServerManager.Core/ViewModels/*.cs`

## Methodology

This inventory combines:

- a scan of `*Internal(...)` methods in Core ViewModels (36 methods found)
- a scan for high-risk APIs and composition patterns (`File`, `Directory`, `Process.Start`, `Json*Parse`, `FileSystemWatcher`, `Timer`, service construction, `_mediator.Register`)
- manual classification into domains

Command used for `*Internal` method inventory:

```powershell
rg -n "\b(?:public|private|internal|protected)\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void|bool|int|string|ICommand|ValueTask(?:<[^>]+>)?)\s+[A-Za-z0-9_]+Internal\s*\(" src/McpServerManager.Core/ViewModels -g "*.cs"
```

## Summary

- Core ViewModel files: `7`
- `*Internal(...)` methods found: `36`
- Highest-risk files:
  - `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs`
  - `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs`
  - `src/McpServerManager.Core/ViewModels/WorkspaceViewModel.cs` (timer + local composition)
  - `src/McpServerManager.Core/ViewModels/TodoListViewModel.cs` (local composition)

## Domain Taxonomy

- `UI`: state projection, selection/navigation, display-only helpers
- `Orchestration`: multi-step feature workflows and cross-service coordination
- `Filesystem`: file/directory existence, read/write/move/delete
- `Process`: shell/process launch
- `Network/Service`: external service calls / remote interactions
- `Parsing/Mapping`: JSON parse, tree building, summary/index generation
- `Watcher/Timer`: `FileSystemWatcher` or polling timer lifecycle
- `Composition`: service construction or handler registration

## File-by-File Inventory

### `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs`

#### `*Internal` methods (28)

| Method | Line | Classification | Compliance | Extraction target |
|---|---:|---|---|---|
| `PhoneNavigateSectionInternal` | 138 | UI | Allowed if kept UI-only | Stay in VM/UI helper |
| `TreeItemTappedInternal` | 438 | UI | Allowed if kept UI-only | Stay in VM/UI helper |
| `JsonNodeDoubleTappedInternal` | 483 | UI/navigation | Allowed if kept UI-only | Stay in VM/UI helper |
| `SearchRowTappedInternal` | 495 | UI | Allowed if kept UI-only | Stay in VM/UI helper |
| `SearchRowDoubleTappedInternal` | 506 | UI/navigation | Allowed if kept UI-only | Stay in VM/UI helper |
| `UpdateFilteredSearchEntriesInternal` | 1123 | UI projection/filtering | Allowed if pure | Keep or move to mapper if needed |
| `NavigateBackInternal` | 1176 | UI navigation | Allowed if pure | Stay in VM |
| `NavigateForwardInternal` | 1195 | UI navigation | Allowed if pure | Stay in VM |
| `ShowRequestDetailsInternal` | 1289 | UI projection | Allowed if pure | Stay in VM |
| `SelectSearchEntryInternal` | 1574 | UI selection | Allowed if pure | Stay in VM |
| `CloseRequestDetailsInternal` | 1583 | UI | Allowed if pure | Stay in VM |
| `NavigateToPreviousRequestInternal` | 1622 | UI navigation | Allowed if pure | Stay in VM |
| `NavigateToNextRequestInternal` | 1637 | UI navigation | Allowed if pure | Stay in VM |
| `CopyTextInternal` | 1689 | UI support / clipboard service call | Borderline (acceptable if clipboard abstraction only) | Confirm abstraction boundary |
| `CopyOriginalJsonInternal` | 1701 | UI support + data prep | Borderline | Consider query/mapper if expands |
| `ReloadFromMcpAsyncInternal` | 1937 | Orchestration + Network/Service + Parsing/Mapping | Non-compliant | Command handler + app services |
| `GenerateAndNavigateInternal` | 2099 | Orchestration + Parsing/Mapping | Non-compliant | Handler + mapper |
| `LoadMarkdownFileInternal` | 2191 | Filesystem | Non-compliant | Filesystem service + handler |
| `LoadSourceFileInternal` | 2242 | Filesystem | Non-compliant | Filesystem service + handler |
| `OpenPreviewInBrowserInternal` | 2350 | Process | Non-compliant | Shell service + handler |
| `ToggleShowRawMarkdownInternal` | 2363 | UI toggle | Allowed | Stay in VM |
| `OpenAgentConfigInternal` | 2371 | Filesystem + Process | Non-compliant | Shell/file service + handler |
| `OpenPromptTemplatesInternal` | 2380 | Filesystem + Process | Non-compliant | Shell/file service + handler |
| `ArchiveInternal` | 2436 | Filesystem | Non-compliant | Filesystem service + handler |
| `OpenTreeItemInternal` | 2473 | Filesystem + Process | Non-compliant | Filesystem/shell service + handler |
| `ArchiveTreeItemInternal` | 2484 | Filesystem | Non-compliant | Filesystem service + handler |
| `LoadJsonInternal` | 2666 | Filesystem + Parsing/Mapping | Non-compliant | Parser/file services + handler |
| `BuildUnifiedSummaryAndIndexInternal` | 3058 | Parsing/Mapping + aggregation | Non-compliant (app mapping logic) | Aggregation/mapping service |
| `BuildJsonTreeInternal` | 3117 | Parsing/Mapping + tree mapping | Non-compliant (large mapping logic in VM) | Tree mapper service |

#### Additional non-`Internal` app-logic hotspots

| Pattern | Line(s) | Classification | Compliance |
|---|---|---|---|
| Service construction (`new McpWorkspaceService`, `new McpSessionLogService`, `new McpTodoService`) | 314, 340, 341, 343, 580, 1036 | Composition | Non-compliant |
| Handler registration (`_mediator.Register(...)`) | 356-430 | Composition | Non-compliant |
| Timers (`_mcpAutoRefreshTimer`, `_workspaceHealthTimer`) | 93, 95, 614, 1881 | Watcher/Timer | Non-compliant |
| `FileSystemWatcher` for AGENTS file | 98, 697, 702 | Watcher/Timer + filesystem | Non-compliant |
| Filesystem reads/writes/moves | many, e.g. 785, 1247, 1451, 2261, 2449, 2499, 2600, 2649, 2674 | Filesystem | Non-compliant |
| Process launch | 2327, 2338, 2400, 2410, 2560 | Process | Non-compliant |
| JSON parsing (`JsonNode.Parse`, `JsonDocument.Parse`) | 2675, 2809, 2870, 2949 | Parsing | Non-compliant |

### `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs`

#### `*Internal` methods (8)

| Method | Line | Classification | Compliance | Extraction target |
|---|---:|---|---|---|
| `OpenAgentConfigInternal` | 72 | Filesystem + Process | Non-compliant | Shell/file service + handler |
| `OpenPromptTemplatesInternal` | 81 | Filesystem + Process | Non-compliant | Shell/file service + handler |
| `LoadPromptsInternal` | 124 | Filesystem + parsing | Non-compliant | Query handler + prompt service |
| `PopulatePromptInternal` | 145 | UI projection/editor state | Allowed if kept projection-only | Keep in VM |
| `SubmitPromptAsyncInternal` | 154 | Orchestration | Non-compliant | Command handler/app service |
| `LoadModelsAsyncInternal` | 170 | Network/Service | Non-compliant | Query handler + model service |
| `SendAsyncInternal` | 207 | Orchestration + Network/Service | Non-compliant | Command handler/app service |

#### Additional non-`Internal` app-logic hotspots

| Pattern | Line(s) | Classification | Compliance |
|---|---|---|---|
| Default service construction (`new OllamaLogAgentService()`) | 55 | Composition | Non-compliant |
| Handler registration (`_mediator.Register(...)`) | 59-66 | Composition | Non-compliant |
| Filesystem check | 89 | Filesystem | Non-compliant |
| Process launch | 96, 106 | Process | Non-compliant |

Note: `PopulatePromptInternal` appears in `Core/Commands/ChatCommands.cs` wrapper flow today, but the method itself is UI projection and may remain in the VM after command refactor.

### `src/McpServerManager.Core/ViewModels/TodoListViewModel.cs`

No `*Internal` methods were detected by the scan, but the file still contains app-layer composition:

| Pattern | Line(s) | Classification | Compliance |
|---|---|---|---|
| `RegisterCqrsHandlers(new McpTodoService(mcpBaseUrl))` | 96 | Composition | Non-compliant |
| `_mediator.Register*` / `_mediator.RegisterQuery` | 86-91 | Composition | Non-compliant |

### `src/McpServerManager.Core/ViewModels/WorkspaceViewModel.cs`

No `*Internal` methods were detected by the scan, but the file still contains app-layer composition and polling:

| Pattern | Line(s) | Classification | Compliance |
|---|---|---|---|
| `RegisterCqrsHandlers(new McpWorkspaceService(mcpBaseUrl))` | 113 | Composition | Non-compliant |
| `_mediator.Register*` / `_mediator.RegisterQuery` | 94-105 | Composition | Non-compliant |
| `_healthTimer` field and `new Timer(...)` | 25, 774-789 | Watcher/Timer | Non-compliant |

### `src/McpServerManager.Core/ViewModels/ConnectionViewModel.cs`

No high-risk app-logic indicators found in the Phase 0 scan.

### `src/McpServerManager.Core/ViewModels/LogViewModel.cs`

No high-risk app-logic indicators found in the Phase 0 scan (log presentation concerns only in this pass).

### `src/McpServerManager.Core/ViewModels/ViewModelBase.cs`

Base view-model infrastructure only; no app-logic extraction work identified in this inventory.

## Immediate Refactor Priorities Derived from This Inventory

1. Extract `MainWindowViewModel` orchestration/IO/watcher/timer logic (largest risk surface).
2. Replace `ChatWindowViewModel` file/process/model/send logic with handler/service-backed flows.
3. Remove VM-local composition roots from `TodoListViewModel` and `WorkspaceViewModel`.

