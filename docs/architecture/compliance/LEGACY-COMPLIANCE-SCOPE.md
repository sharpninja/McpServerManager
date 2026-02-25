# Legacy Compliance Scope Decision (`src/McpServerManager`)

Status: Approved Phase 0 decision for `MVP-APP-006`

## Decision

The legacy project `src/McpServerManager` is **excluded from the active compliance scope** for `MVP-APP-006` implementation phases in Core/Desktop/Android.

It remains:

- tracked for audit visibility
- documented as non-compliant
- subject to a later dedicated deprecation or refactor decision

This decision makes the success criteria for the active epic unambiguous.

## Why This Decision Was Required

The user asked for an audit of "all code" and later enforced strict correctness/adherence. Without an explicit legacy scope decision:

- the epic can never report a clear pass/fail state for the actively maintained app stack
- CI guardrails would either fail immediately on legacy debt or require silent masking
- refactor progress in `Core/Desktop/Android` would be obscured by unrelated legacy backlog

## Legacy Inventory (Phase 0)

Legacy project files relevant to compliance:

### Legacy ViewModels (`3`)

- `src/McpServerManager/ViewModels/ViewModelBase.cs`
- `src/McpServerManager/ViewModels/MainWindowViewModel.cs`
- `src/McpServerManager/ViewModels/ChatWindowViewModel.cs`

### Legacy Code-Behind (`4`)

- `src/McpServerManager/App.axaml.cs`
- `src/McpServerManager/Views/MainWindow.axaml.cs`
- `src/McpServerManager/Views/ChatWindow.axaml.cs`
- `src/McpServerManager/Views/RequestDetailsView.axaml.cs`

## Phase 0 Evidence of Legacy Non-Compliance

Representative legacy ViewModel violations:

- Service construction in VM:
  - `src/McpServerManager/ViewModels/MainWindowViewModel.cs:191`
- Filesystem IO in VM:
  - `src/McpServerManager/ViewModels/MainWindowViewModel.cs:550`
  - `src/McpServerManager/ViewModels/MainWindowViewModel.cs:1783`
- Process launch in VM:
  - `src/McpServerManager/ViewModels/MainWindowViewModel.cs:1481`
  - `src/McpServerManager/ViewModels/MainWindowViewModel.cs:1546`
- JSON parsing in VM:
  - `src/McpServerManager/ViewModels/MainWindowViewModel.cs:1784`
  - `src/McpServerManager/ViewModels/MainWindowViewModel.cs:1918`
- Chat process/file logic in VM:
  - `src/McpServerManager/ViewModels/ChatWindowViewModel.cs:69`
  - `src/McpServerManager/ViewModels/ChatWindowViewModel.cs:76`

Representative legacy code-behind extraction target:

- chat VM/config composition in `src/McpServerManager/Views/MainWindow.axaml.cs:528`
- chat VM/config composition in `src/McpServerManager/Views/MainWindow.axaml.cs:529`

## CI and Guardrail Implications

For Phase 0 guardrails:

- CI checks run against the active compliance scope (`Core`, `Desktop`, `Android`) and intentionally exclude the legacy project path.
- This is a scope decision, not a claim that legacy code is compliant.

## Re-entry Criteria (if legacy returns to scope)

Legacy may re-enter compliance scope only when one of these is explicitly chosen:

1. Full refactor plan and owner are assigned for legacy ViewModels/code-behind, or
2. Legacy project is formally deprecated and excluded from shipping/CI paths

## Related Tasks Satisfied by This Decision

- `MVP-APP-006` task 7 (inventory legacy violations + decide refactor/deprecate path)
- `MVP-APP-006` task 8 (document final compliance-scope decision for legacy)
- `MVP-APP-006` task 35 (legacy excluded path in docs/CI for current phase)

