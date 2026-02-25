# Inventory: Core Handlers Coupled to ViewModels

Status: Phase 0 inventory baseline  
Scope: `src/McpServerManager.Core/Commands/*.cs`

## Objective

Identify every command-handler path that violates the rule:

- no command/query payloads carrying `ViewModel` references
- no handlers invoking `ViewModel.*Internal(...)`
- no handlers reaching through `ViewModel` to access services/mediator/internal state

## Metrics (Phase 0 scan)

Scan results across `Core/Commands`:

- `ViewModel` properties in commands: `40`
- `.ViewModel.` accesses in handlers: `35`
- `Internal(` calls from handlers: `39`

Command examples used:

```powershell
rg -n "public .*ViewModel \{ get; \}" src/McpServerManager.Core/Commands -g "*.cs"
rg -n "\.ViewModel\." src/McpServerManager.Core/Commands -g "*.cs"
rg -n "Internal\(" src/McpServerManager.Core/Commands -g "*.cs"
```

## File-by-File Inventory

| File | ViewModel props | `.ViewModel.` calls | `Internal(` calls | Classification | Replacement design |
|---|---:|---:|---:|---|---|
| `src/McpServerManager.Core/Commands/AllCommands.cs` | 22 | 22 | 21 | Non-compliant CQRS wrapper layer | Split into DTO-only commands and service-backed handlers; keep UI-only projections in VM |
| `src/McpServerManager.Core/Commands/AsyncCommands.cs` | 10 | 5 | 11 | Non-compliant CQRS wrapper + VM internals/service access | Introduce application services for async workflows and return structured results |
| `src/McpServerManager.Core/Commands/ChatCommands.cs` | 8 | 8 | 7 | Non-compliant CQRS wrapper layer | Chat service/query handlers; VM dispatch + projection only |
| `src/McpServerManager.Core/Commands/TodoCommands.cs` | 0 | 0 | 0 | Compliant service-backed pattern (baseline) | Preserve as reference pattern |
| `src/McpServerManager.Core/Commands/WorkspaceCommands.cs` | 0 | 0 | 0 | Compliant service-backed pattern (baseline) | Preserve as reference pattern |

## Representative Violations

### `AllCommands.cs`

- ViewModel payload property examples:
  - `src/McpServerManager.Core/Commands/AllCommands.cs:12`
  - `src/McpServerManager.Core/Commands/AllCommands.cs:27`
- Handler-to-VM internal call examples:
  - `src/McpServerManager.Core/Commands/AllCommands.cs:20`
  - `src/McpServerManager.Core/Commands/AllCommands.cs:73`
  - `src/McpServerManager.Core/Commands/AllCommands.cs:249`

### `AsyncCommands.cs`

- ViewModel payload property examples:
  - `src/McpServerManager.Core/Commands/AsyncCommands.cs:28`
  - `src/McpServerManager.Core/Commands/AsyncCommands.cs:188`
- VM internal call examples:
  - `src/McpServerManager.Core/Commands/AsyncCommands.cs:318`
  - `src/McpServerManager.Core/Commands/AsyncCommands.cs:335`
  - `src/McpServerManager.Core/Commands/AsyncCommands.cs:373`
- Additional VM internals/service coupling (from audit):
  - direct access to `vm._mediator` and VM-held services in async flows

### `ChatCommands.cs`

- ViewModel payload property examples:
  - `src/McpServerManager.Core/Commands/ChatCommands.cs:15`
  - `src/McpServerManager.Core/Commands/ChatCommands.cs:31`
- VM internal call examples:
  - `src/McpServerManager.Core/Commands/ChatCommands.cs:23`
  - `src/McpServerManager.Core/Commands/ChatCommands.cs:39`
  - `src/McpServerManager.Core/Commands/ChatCommands.cs:149`

## Replacement Owner Map

| Coupled handler area | New owner type |
|---|---|
| Chat prompt/config open/load/send/model workflows | Chat application services + CQRS handlers |
| MainWindow async refresh/load/archive/open/preview flows | MainWindow application services + CQRS handlers |
| MainWindow UI-only actions (selection/toggles) | ViewModel methods (no handler or DTO-only handler returning state input) |

## Exit Criteria for This Inventory

This inventory is considered resolved when:

- `AllCommands.cs`, `AsyncCommands.cs`, and `ChatCommands.cs` no longer declare `ViewModel` payload properties
- handlers in those files no longer call `*Internal(...)`
- handlers no longer depend on ViewModel private state or mediator instances

