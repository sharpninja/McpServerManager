# Compliance Matrix (MVP-APP-006 Baseline)

Status: Phase 0 baseline matrix (tracked, actionable)  
Source audit: `HANDOFF.md` (CQRS + code-behind findings, blame map, provenance notes)

## Matrix

| ID | Violation Family | Current State | Target Design | Replacement Owner | Status | Evidence |
|---|---|---|---|---|---|---|
| CM-001 | CQRS wrapper handlers in `Core/Commands` | Commands carry `MainWindowViewModel` / `ChatWindowViewModel`; handlers call `*Internal` methods | DTO-only commands/queries + service-backed handlers returning results | `Core/Commands` + new app services | Open | `src/McpServerManager.Core/Commands/AllCommands.cs`, `src/McpServerManager.Core/Commands/AsyncCommands.cs`, `src/McpServerManager.Core/Commands/ChatCommands.cs` |
| CM-002 | `MainWindowViewModel` owns app logic | Network/file/process/parsing/watcher/timer/orchestration/composition logic in VM | VM reduced to UI state + dispatch; handlers/services own behavior | `MainWindowViewModel` + extracted services | Open | `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs` |
| CM-003 | `ChatWindowViewModel` owns app logic | Prompt/config file IO, shell-open, model discovery, send orchestration in VM | Chat app service interfaces + command/query handlers + VM projection only | `ChatWindowViewModel` + chat services | Open | `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs` |
| CM-004 | VM-local composition roots | VMs construct MCP services and register handlers directly | Central composition layer/factory injects services/handlers | `TodoListViewModel`, `WorkspaceViewModel`, `MainWindowViewModel`, `ChatWindowViewModel` | Open | `src/McpServerManager.Core/ViewModels/TodoListViewModel.cs`, `src/McpServerManager.Core/ViewModels/WorkspaceViewModel.cs` |
| CM-005 | Desktop MainWindow code-behind owns app composition | Code-behind constructs chat VM and reads/writes config model path | Code-behind requests view model/factory result; no feature composition | Desktop host/app composition layer | Open | `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs:248` |
| CM-006 | Code-behind exceptions not fully cataloged | Mixed UI-only and exceptions, no complete desktop/android inventory | Full inventory with classification and extraction backlog | Desktop + Android view inventory docs | Completed (Phase 0) | `docs/architecture/compliance/INVENTORY-CODE-BEHIND.md` |
| CM-007 | Legacy project compliance scope ambiguous | Legacy project still exists and is non-compliant; success criteria unclear | Explicit scope decision: exclude legacy from active compliance and CI checks until dedicated deprecation/refactor phase | Maintainers + epic owner | Completed (Phase 0 decision) | `docs/architecture/compliance/LEGACY-COMPLIANCE-SCOPE.md` |
| CM-008 | Architecture drift risk during refactor | New non-compliant patterns can be introduced while epic is open | Freeze rule + CI checks + PR checklist | Repo policy + CI | Completed (Phase 0) | `docs/architecture/compliance/COMPLIANCE-SPEC.md`, `.github/pull_request_template.md`, `tools/compliance/*` |

## Status Legend

- `Open`: Violation exists and refactor is not complete.
- `Completed (Phase 0)`: Baseline governance/inventory/scoping work completed.
- `In Progress`: Refactor underway with partial migration (none marked yet in this matrix).

## Mapping to `MVP-APP-006` Task Families

Phase 0 matrix-driven tasks:

- Governance/spec/freeze: tasks 1, 3
- Baseline matrix: task 2
- Inventories: tasks 4, 5, 6, 7
- Legacy scope decision: tasks 8 and (scope path of) 35
- Guardrails/checklist: tasks 36, 37, 38

## Notes

- This matrix tracks violation families and replacement owners. It is not a substitute for the line-level blame map in `HANDOFF.md`.
- Refactor-phase updates should append progress and remaining violations to `HANDOFF.md` and keep this matrix current.

