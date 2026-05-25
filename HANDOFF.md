# Handoff - 2026-05-23

## Session Context
- Active agent: openCode (deepseek-v4-flash-free)
- MCP session: `Claude-20260523T074755Z-start` (session log ID 196)
- Workspace: McpServerManager (`F:\GitHub\McpServerManager`)
- MCP server: `http://PAYTON-LEGION2:7147` (marker signature verified, health nonce echoed)

## Rule Change
- TDD Lessons Learned added to `AGENTS.md` (4 entries covering: test defines correct behavior, validate mocks refine understanding, requirements first, assertions verify intent).

## Completed

### ISSUE-APP-001 (Slice 1) — Fix `CaptureDispatchLog` `IndexOutOfRangeException`
- **Root cause**: `CaptureDispatchLog` in `Dispatcher.cs` assumed 2-part format string but received a single-token input on line 206.
- **Fix**: Wrapped `CaptureDispatchLog` in try-catch (line 158) and snap `ConcurrentBag` to `ToList()` (line 211) to prevent collection-modification race on enumeration.
- **Tests**: 3 Cqrs tests added; 33/33 pass.
- **TODO**: Closed via MCP.

### ISSUE-TODO-001 (Slice 2) — Consecutive editor create fails
- **Root cause**: `NewTodo()` reset host-level `EditorText`/`EditorTitle` but did not reset `_detailVm`. When the user typed a custom ID (e.g., `TEST-TEST-002`) in the YAML frontmatter, `SaveEditorAsync()` set `isNew = false` (ID was not `"NEW-TODO"`), dispatched an **Update** command, and the server returned `"Item with id 'TEST-TEST-002' not found."`.
- **Fix**: Added `_detailVm.BeginNewDraft()` to `NewTodo()` at `src/McpServerManager.UI.Core/ViewModels/TodoListHostViewModel.cs:385`. This sets `_detailVm.IsNewDraft = true`, causing `TodoDetailViewModel.SaveAsync()` to route to `CreateAsync()` instead of `UpdateAsync()`.
- **Tests**: 3 new tests added (`SaveNewTodoAsync_ConsecutiveCreatesBothSucceed`, `SaveEditorAsync_ConsecutiveNewTodosViaEditorBothSucceed`, `SaveEditorAsync_NewAfterExistingCreateWithCustomId_CreatesNotUpdates`). 286/286 UI Core tests pass. 77/77 Web tests pass.
- **Verification grep**: No stale patterns found.
- **TODO**: Fixed but NOT YET CLOSED via MCP API (PATCH failed due to using raw curl instead of plugin).

## Outstanding TODOs (from MCP server, all `done: false`)

| ID | Title | Priority |
|---|---|---|
| ISSUE-SESSION-001 | Session Logs Must be Workspace Constrained | High |
| ISS-DIRECTOR-001 | 10.0.204 ERROR director add-workspace marker signature failure | High |
| ISS-DESKTOP-001 | Scroll TODO Editor to first line when loading a different TODO | High |
| ARCH-WORKSPACEEDIT-001 | Allow editing workspaces in Director and Web-UI | High |
| PLAN-WORKSPACEEDIT-001 | Detailed implementation plan for editable workspaces | High |
| ISSUE-TODO-001 | Can only add one new TODO in a workspace (FIXED, not closed) | High |
| PLAN-REQSDESKTOP-001 | Add Requirements Management to Desktop | Medium |
| UI-TODO-001 | Create form for YAML portion of TODO template | Medium |
| TEST-TEST-001 | Test | Low |

## Files Modified
- `F:\GitHub\McpServerManager\src\McpServerManager.UI.Core\ViewModels\TodoListHostViewModel.cs` — Added `_detailVm.BeginNewDraft()` to `NewTodo()`.
- `F:\GitHub\McpServerManager\tests\McpServerManager.UI.Core.Tests\TodoViewModelTests.cs` — Added 3 new test methods and exposed `SaveNewTodoForTestAsync()`, `NewTodoForTest()`, `SaveEditorForTestAsync()` on tracking subclass.
- `F:\GitHub\McpServerManager\AGENTS.md` — Added TDD Lessons Learned section.
- `F:\GitHub\McpServerManager\HANDOFF.md` — This file.

## Next Steps
1. Close ISSUE-TODO-001 via plugin (`Complete-McpTodo -Id "ISSUE-TODO-001" -DoneSummary "..."`) to mark it done since the fix is verified.
2. Triage remaining TODOs: ISSUE-SESSION-001, ISS-DIRECTOR-001, and ISS-DESKTOP-001 are the highest priority.
3. Follow Byrd Process for each: surface requirements, write tests, validate with success mocks, implement, all tests green, close TODO.
