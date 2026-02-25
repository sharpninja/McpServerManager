# CQRS and Code-Behind Compliance Specification

Status: Active temporary architecture standard (Phase 0 freeze + refactor baseline)  
Owner: Maintainers working `MVP-APP-006`

## Purpose

This document defines the required architecture boundaries for the RequestTracker codebase while `MVP-APP-006` is in progress.

The goal is correctness and actual responsibility migration, not superficial CQRS wrappers.

## Scope

Included:

- `src/McpServerManager.Core`
- `src/McpServerManager.Desktop`
- `src/McpServerManager.Android`

Legacy scope decision:

- `src/McpServerManager` is currently excluded from the active compliance pass and marked legacy/deprecated for this epic phase.
- See `docs/architecture/compliance/LEGACY-COMPLIANCE-SCOPE.md`.

## Definitions

- `ViewModel`: UI state/projection object that exposes bindable properties and dispatches commands/queries.
- `App logic`: Orchestration, IO, parsing, process launch, watchers, timers, composition, service construction, and domain workflows.
- `CQRS handler`: The application behavior owner for a command/query. Handlers may call services and return structured results.
- `Code-behind`: Avalonia UI adapter layer. Allowed to handle UI control wiring and platform-specific view concerns only.

## Required Architecture Rules

### 1) ViewModel responsibilities (allowed)

Allowed in `ViewModels`:

- Bindable UI state and derived display properties
- UI projection/mapping from handler/query results into bindable state
- Command/query dispatch through mediator interfaces
- UI validation and field normalization that does not perform IO
- Cancellation token ownership for UI-triggered operations (when not performing app logic directly)
- UI-only helpers (selection changes, tab switches, view mode toggles)

### 2) ViewModel responsibilities (forbidden)

Forbidden in `ViewModels` unless explicitly documented and approved:

- Network access and service calls that implement application workflows
- Filesystem reads/writes/moves/deletes
- Process launch (`Process.Start`, shell-open)
- File watchers (`FileSystemWatcher`)
- Timer-based polling or watcher lifecycles for app workflows
- JSON parsing/tree building for application/domain data
- Service construction (`new Mcp*Service`, `new ...Service`) except pure UI abstractions supplied by host bootstrap
- CQRS handler registration/composition root behavior

Current known violations include:

- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs`
- `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs`
- `src/McpServerManager.Core/ViewModels/TodoListViewModel.cs`
- `src/McpServerManager.Core/ViewModels/WorkspaceViewModel.cs`

## CQRS Handler Rules

### 3) Command/query payload design (required)

Commands/queries must carry primitives and DTOs only. They must not carry `ViewModel` references.

Forbidden examples (current anti-patterns):

- `src/McpServerManager.Core/Commands/AllCommands.cs`
- `src/McpServerManager.Core/Commands/AsyncCommands.cs`
- `src/McpServerManager.Core/Commands/ChatCommands.cs`

### 4) Handler behavior ownership (required)

Handlers must own application behavior. Handlers may call services and return structured results. Handlers must not:

- call `ViewModel.*Internal(...)`
- mutate ViewModel internals as the primary implementation path
- access ViewModel private fields/services indirectly through a passed ViewModel

Compliant reference pattern in current repo:

- `src/McpServerManager.Core/Commands/TodoCommands.cs`
- `src/McpServerManager.Core/Commands/WorkspaceCommands.cs`

## Code-Behind Rules

### 5) Code-behind responsibilities (allowed)

Allowed in `.axaml.cs`:

- UI event wiring and event forwarding to commands
- Control-specific synchronization not expressible in XAML (e.g., `AvaloniaEdit` text sync)
- Splitter/layout persistence and restoration
- Platform visual behavior and focus management
- Documented view-layer exceptions (for example, known library binding incompatibilities)

### 6) Code-behind responsibilities (forbidden)

Forbidden in `.axaml.cs` unless documented and approved:

- Constructing app services for feature workflows
- Reading/writing app config for feature workflows
- Creating feature ViewModels with non-UI dependencies
- Owning application orchestration or business logic

Current extraction-required example:

- `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs:248`

## Temporary Freeze Rule (Effective Immediately)

Until `MVP-APP-006` is complete:

- Do not add new ViewModel app logic.
- Do not add commands/queries that carry `ViewModel` references.
- Do not add handlers that call `ViewModel.*Internal(...)`.
- Do not add non-UI logic to code-behind without documented exception approval.

All new work must either:

- follow this spec, or
- include a documented exception proposal with rationale and planned removal.

## Enforcement

Phase 0 introduces CI guardrails and review checklist artifacts:

- `tools/compliance/Check-CqrsBoundaries.ps1`
- `tools/compliance/Check-ViewModelBoundaries.ps1`
- `tools/compliance/Invoke-ArchitectureChecks.ps1`
- `.github/pull_request_template.md`

These checks intentionally fail when violations exist. They are not a wrapper or suppressor for existing non-compliance.

## Approved Exceptions (Current)

Documented exception references:

- Markdown binding workaround evaluation in `docs/EXCEPTION-EVALUATION.md`

No blanket exceptions are granted for ViewModel IO/process/watcher/composition logic.

