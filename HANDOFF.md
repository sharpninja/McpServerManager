# Session Handoff — 2026-02-23 (Session 3)

## Context / User Directive (New, strict)
- The user clarified the architecture rule during audit:
  - `ViewModels should NOT have app logic. They should delegate all app logic to CQRS handlers.`
  - `Code-behind should remain UI-only`, except explicitly documented exceptions.
- Additional directive (must guide all future work):
  - Design and implement for `CORRECTNESS`, `COMPLETENESS`, and `ADHERENCE`.
  - Do not mask broken standards with compliant wrappers.
  - Prefer correct refactors over surface-level compliance.

## Compliance Audit (CQRS + Code-Behind) — Detailed Findings

### Audit Scope
- Included `src/McpServerManager.Core/` (shared ViewModels, Commands, CQRS)
- Included `src/McpServerManager.Desktop/` and `src/McpServerManager.Android/` code-behind (`*.axaml.cs`)
- Included legacy `src/McpServerManager/` because the request said "all code"
- Reference points used:
  - `HANDOFF.md` architecture notes (CQRS/MVVM/3-project architecture)
  - `docs/EXCEPTION-EVALUATION.md` documented Markdown.Avalonia code-behind exception

### Architectural Rule Interpretation Used for Audit
- **Compliant ViewModel behavior**:
  - UI state + projection + dispatching commands/queries to mediator
  - Minimal UI-only transformations are acceptable (e.g., local formatting of already-loaded state)
- **Non-compliant ViewModel behavior**:
  - Network calls, filesystem IO, process launching, file watchers, JSON parsing/aggregation, service construction/composition
- **Compliant CQRS handler behavior**:
  - Operates on command/query DTOs and dependencies (services), not on concrete ViewModel internals
- **Non-compliant CQRS handler behavior**:
  - Accepts `ViewModel` instance in command payload and calls `ViewModel.*Internal(...)`
- **Compliant code-behind behavior**:
  - UI event wiring, control sync, view-only layout persistence
- **Allowed exception**:
  - Markdown viewer code-behind binding workaround due `Markdown.Avalonia` compatibility issue (documented)

### Quantitative Signals (used to establish systemic vs isolated issues)
- `24` `*.axaml.cs` files under `src/`
- `32` command-handler calls in `Core/Commands` that invoke `ViewModel.*Internal(...)`
- `36` `*Internal(...)` methods declared in `Core/ViewModels`
- `6` code-behind hits for chat-window composition/config operations (`OllamaLogAgentService`, `AgentConfigIo`, `new ChatWindowViewModel(...)`) across current + legacy desktop shells

### CQRS Compliance Result (Current State)
- **Result: FAIL (systemic)**
- The repo contains a real CQRS foundation (`Mediator`, handler interfaces, service-based handlers in some modules), but a large portion of the command layer acts as a wrapper over `ViewModel` internals rather than owning the application behavior.

### Code-Behind Compliance Result (Current State)
- **Result: PARTIAL**
- Most code-behind in Desktop/Android is UI-oriented and likely compliant.
- Desktop shell (`MainWindow.axaml.cs`) and legacy shell still contain application composition / config IO / viewmodel creation logic.
- Markdown viewer workaround in legacy code-behind is explicitly documented and treated as allowed.

## Detailed Audit Evidence (Representative, not exhaustive)

### 1) CQRS wrapper anti-pattern in `Core/Commands` (systemic)
Many commands carry a concrete ViewModel instance and handlers call `ViewModel.*Internal(...)`, which means the app logic remains in the ViewModel and the handler is only a pass-through.

Examples:
- `src/McpServerManager.Core/Commands/AllCommands.cs`
  - `NavigateBackCommand` stores `MainWindowViewModel`
  - `NavigateBackHandler` calls `command.ViewModel.NavigateBackInternal()`
  - Same pattern repeated for selection/navigation/archive/open actions
- `src/McpServerManager.Core/Commands/ChatCommands.cs`
  - `ChatSendMessageCommand` stores `ChatWindowViewModel`
  - `ChatSendMessageHandler` calls `command.ViewModel.SendAsyncInternal()`
  - Similar for models/prompts/config commands
- `src/McpServerManager.Core/Commands/AsyncCommands.cs`
  - Commands store `MainWindowViewModel`
  - Handlers read `vm._mediator`, `vm.McpSessionService`, and invoke multiple `*Internal(...)` methods

Why this fails the rule:
- CQRS handlers are not the application logic owners.
- ViewModel internals become a required handler API (`*Internal`) and effectively form an application service surface.
- The command layer depends directly on UI state containers, reversing the intended dependency direction.

### 2) `MainWindowViewModel` contains substantial app logic (`Core`)
`MainWindowViewModel` currently owns responsibilities that should live in handlers/services:

- **Service composition / endpoint switching**
  - Creates `McpWorkspaceService`, `McpTodoService`, `McpSessionLogService`
  - Rebinds handlers on endpoint change
- **Workspace health checks**
  - Direct network service creation and `GetHealthAsync()` calls
- **Workspace catalog loading**
  - Direct `_workspaceCatalogService.QueryAsync()`
- **AGENTS file watcher**
  - `FileSystemWatcher` lifecycle, debounce scheduling, reload versioning
- **AGENTS file IO**
  - File existence checks, timestamp reads, file content loading, status updates
- **Session loading/parsing**
  - Direct session service fetches
  - tree building and JSON processing orchestration
- **Local file/archive operations**
  - `File.Move`, `File.ReadAllText`, parsing, open-in-browser/process start behaviors

This violates the clarified rule because the ViewModel remains the application coordinator and executor, not a thin state adapter.

### 3) `AsyncCommands` handlers still depend on VM internals (worse than simple wrappers)
`AsyncCommands` are more tightly coupled than `AllCommands`/`ChatCommands`:
- They access `vm._mediator.TrackBackgroundWork(...)`
- They call `vm.McpSessionService.GetAllSessionsAsync(...)`
- They call `vm.BuildUnifiedSummaryAndIndexInternal(...)`, `vm.BuildJsonTreeInternal(...)`, `vm.UpdateFilteredSearchEntriesInternal()`
- They mix app flow control, domain transformations, and UI update dispatch through the ViewModel

This means:
- The handler layer is not independent
- The ViewModel and handlers are mutually entangled
- Unit-level testing of handlers without ViewModel internals is difficult/impossible

### 4) `ChatWindowViewModel` retains app logic (Core)
`ChatWindowViewModel` still performs:
- File/process launching for config/prompt files
- Prompt template file IO
- Ollama model discovery (`OllamaLogAgentService.GetAvailableModelsAsync`)
- Send pipeline orchestration including background agent call and reply/error handling

Even when invoked through the mediator, this remains non-compliant because handlers currently delegate back to the ViewModel (`ChatCommands` wrapper pattern).

### 5) `TodoListViewModel` and `WorkspaceViewModel` act as composition roots for their command handlers
These viewmodels currently create services and register mediator handlers themselves:
- `TodoListViewModel.RegisterCqrsHandlers(McpTodoService service)` and `SetMcpBaseUrl(...)`
- `WorkspaceViewModel.RegisterCqrsHandlers(McpWorkspaceService service)` and `SetMcpBaseUrl(...)`

This is better than the `MainWindowViewModel` wrapper pattern in some respects (handlers are service-based), but still not ideal under the strict rule because:
- ViewModels perform application service composition and lifecycle management
- Handler registration/service replacement is tied to UI state objects

### 6) Desktop `MainWindow` code-behind contains application composition logic
`src/McpServerManager.Desktop/Views/MainWindow.axaml.cs` currently:
- instantiates `OllamaLogAgentService`
- reads config via `AgentConfigIo.GetModelFromConfig()`
- constructs `ChatWindowViewModel(...)`
- writes persisted chat-window state via `LayoutSettingsIo` in chat close path

This is beyond UI-only code-behind. The window should host UI and delegate composition/orchestration to a higher-level application layer (or request it from a ViewModel/CQRS flow).

### 7) Legacy project (`src/McpServerManager`) remains broadly non-compliant (expected, but real)
The legacy standalone project predates the refactor and still contains:
- ViewModel app logic and direct service calls
- code-behind chat composition logic
- direct file/process/network orchestration in viewmodels

This matters because the user requested an audit of "all code". If legacy code remains in active scope, overall compliance is not met.

## Blame Attribution for Non-Compliant Code (line-level snapshot)

### Methodology / caveats
- Blame was captured against the **current working tree** (`git blame --line-porcelain` on the exact line refs cited in the audit).
- This repo is currently dirty, so some lines correctly blame to:
  - `0000000000000000000000000000000000000000` / `Not Committed Yet`
- Blame identifies the **most recent commit touching the specific line**, not necessarily the original architectural decision.

### Commit summary rollup (from audited non-compliant lines only)
- `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (9 lines) — `2026-02-19 21:29:39 -0600` — `sharpninja` — `refactor: complete CQRS mediator pattern for all ViewModels`
- `d0b0a6ba09321101374f46d0278a27eb3555d74f` (6 lines) — `2026-02-19 15:43:12 -0600` — `sharpninja` — `Add Android project, CQRS infrastructure, and three-project architecture`
- `74e099eee0ac7b45e9975b8260e681a82b918083` (6 lines) — `2026-02-20 12:25:19 -0600` — `sharpninja` — `refactor: replace all ConfigureAwait(false) with ConfigureAwait(true)`
- `0e77cf3adbfc89355989de630e24a2b219bc4895` (6 lines) — `2026-02-21 01:37:48 -0600` — `sharpninja` — `feat: add workspace management UI and connection switching`
- `0000000000000000000000000000000000000000` (4 lines) — `Not Committed Yet` — current working tree edits in `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs` (workspace health + AGENTS watcher/load)
- `f5eac34af57dc4e1511731f27203faeea4b3cb9d` (4 lines) — `2026-02-03 14:19:03 -0600` — `sharpninja` — `Agent config, prompts, chat, and UX improvements`
- `caa421ceefe7fc609d3f1d8b323a6a222034fd2d` (3 lines) — `2026-02-21 02:14:46 -0600` — `sharpninja` — `refactor`
- `2987350134f3b5c925fe58f8f1913671838e29ea` (2 lines) — `2026-02-19 22:03:29 -0600` — `sharpninja` — `fix: status bar animation stays active until all commands complete`
- `23d7595cf9bc99b23470e49bfd17ccd9291de4ab` (1 line) — `2026-02-20 13:41:23 -0600` — `sharpninja` — `perf: fix Android ANR - eliminate double MCP fetch and heavy JSON tree on startup`
- `7864087e2f44de148b99001faed98d2fef0e5d6b` (1 line) — `2026-02-20 10:19:50 -0600` — `sharpninja` — `feat: add Todo management tab with CQRS, MCP integration, and Desktop+Android views`
- `f542e27696e10eb9816cb8a1622f3ec0e57307c5` (1 line) — `2026-02-19 12:20:36 -0600` — `sharpninja` — `Refresh MCP session data on All JSON node clicks`
- `f3cf9ee417f15d0bcb6b4d16624fab6beb20ceb0` (1 line) — `2026-02-03 17:19:38 -0600` — `sharpninja` — `Archive JSON, tree context menu, chat layout, details UI, search timestamp`

### Agent / model provenance determination for blamed commits

#### Method used
- Inspected each blamed commit with:
  - `git show -s --show-notes --format=... <commit>`
- Looked for explicit provenance markers in commit body/trailers (e.g., `Co-authored-by`, `Generated with`, `Model:`).
- Also spot-checked history for AI-related trailers/markers.

#### Result summary
- **Author/committer on all blamed commits:** `sharpninja <ninja@thesharp.ninja>`
- **Explicit agent provenance found on some commits:** `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
- **Explicit model provenance (e.g., GPT-4o / Claude / etc.):** **not recorded** in any blamed commit metadata/body
- **Uncommitted working-tree lines (`000...`):** no commit metadata exists, so agent/model cannot be determined from Git; working tree may contain mixed authorship

#### Per-commit agent/model provenance (audited commit set)
- `0854c8a396bc0c16d692d94a928cb1e634b20bbe`
  - Agent provenance: `Copilot` explicitly recorded (`Co-authored-by` trailer present)
  - Model provenance: not recorded
- `d0b0a6ba09321101374f46d0278a27eb3555d74f`
  - Agent provenance: `Copilot` explicitly recorded (`Co-authored-by` trailer present)
  - Model provenance: not recorded
- `74e099eee0ac7b45e9975b8260e681a82b918083`
  - Agent provenance: `Copilot` explicitly recorded (`Co-authored-by` trailer present)
  - Model provenance: not recorded
- `0e77cf3adbfc89355989de630e24a2b219bc4895`
  - Agent provenance: no explicit agent trailer/marker found
  - Model provenance: not recorded
- `0000000000000000000000000000000000000000` (`Not Committed Yet`)
  - Agent provenance: not determinable from Git (no commit object)
  - Model provenance: not determinable from Git
- `f5eac34af57dc4e1511731f27203faeea4b3cb9d`
  - Agent provenance: no explicit agent trailer/marker found
  - Model provenance: not recorded
- `caa421ceefe7fc609d3f1d8b323a6a222034fd2d`
  - Agent provenance: no explicit agent trailer/marker found
  - Model provenance: not recorded
- `2987350134f3b5c925fe58f8f1913671838e29ea`
  - Agent provenance: `Copilot` explicitly recorded (`Co-authored-by` trailer present)
  - Model provenance: not recorded
- `23d7595cf9bc99b23470e49bfd17ccd9291de4ab`
  - Agent provenance: `Copilot` explicitly recorded (`Co-authored-by` trailer present)
  - Model provenance: not recorded
- `7864087e2f44de148b99001faed98d2fef0e5d6b`
  - Agent provenance: `Copilot` explicitly recorded (`Co-authored-by` trailer present)
  - Model provenance: not recorded
- `f542e27696e10eb9816cb8a1622f3ec0e57307c5`
  - Agent provenance: `Copilot` explicitly recorded (`Co-authored-by` trailer present)
  - Model provenance: not recorded
- `f3cf9ee417f15d0bcb6b4d16624fab6beb20ceb0`
  - Agent provenance: no explicit agent trailer/marker found
  - Model provenance: not recorded

#### Provenance implications for the audit
- The audit can reliably distinguish:
  - commits with explicit **Copilot co-author** provenance
  - commits with **no explicit AI provenance recorded**
- The audit **cannot** reliably determine the exact model used for any blamed commit from Git metadata alone.
- Absence of an AI trailer is **not evidence of human-only authorship**; it only means no explicit provenance was recorded in the commit metadata/body.

### Line-level attribution map (audited references)

#### A) CQRS wrapper anti-pattern (`Core/Commands`)
- `src/McpServerManager.Core/Commands/AllCommands.cs:12`
  - Blame: `d0b0a6ba09321101374f46d0278a27eb3555d74f` (`sharpninja`, `2026-02-19 21:43:12 +00:00`)
  - Summary: `Add Android project, CQRS infrastructure, and three-project architecture`
  - Code: `public MainWindowViewModel ViewModel { get; }`
- `src/McpServerManager.Core/Commands/AllCommands.cs:20`
  - Blame: `d0b0a6ba09321101374f46d0278a27eb3555d74f` (`sharpninja`, `2026-02-19 21:43:12 +00:00`)
  - Summary: `Add Android project, CQRS infrastructure, and three-project architecture`
  - Code: `command.ViewModel.NavigateBackInternal();`
- `src/McpServerManager.Core/Commands/ChatCommands.cs:15`
  - Blame: `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (`sharpninja`, `2026-02-20 03:29:39 +00:00`)
  - Summary: `refactor: complete CQRS mediator pattern for all ViewModels`
  - Code: `public ChatWindowViewModel ViewModel { get; }`
- `src/McpServerManager.Core/Commands/ChatCommands.cs:23`
  - Blame: `74e099eee0ac7b45e9975b8260e681a82b918083` (`sharpninja`, `2026-02-20 18:25:19 +00:00`)
  - Summary: `refactor: replace all ConfigureAwait(false) with ConfigureAwait(true)`
  - Code: `await command.ViewModel.SendAsyncInternal().ConfigureAwait(true);`
- `src/McpServerManager.Core/Commands/AsyncCommands.cs:28`
  - Blame: `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (`sharpninja`, `2026-02-20 03:29:39 +00:00`)
  - Summary: `refactor: complete CQRS mediator pattern for all ViewModels`
  - Code: `public MainWindowViewModel ViewModel { get; }`
- `src/McpServerManager.Core/Commands/AsyncCommands.cs:38`
  - Blame: `2987350134f3b5c925fe58f8f1913671838e29ea` (`sharpninja`, `2026-02-20 04:03:29 +00:00`)
  - Summary: `fix: status bar animation stays active until all commands complete`
  - Code: `vm._mediator.TrackBackgroundWork(Task.Run(async () =>`
- `src/McpServerManager.Core/Commands/AsyncCommands.cs:48`
  - Blame: `74e099eee0ac7b45e9975b8260e681a82b918083` (`sharpninja`, `2026-02-20 18:25:19 +00:00`)
  - Summary: `refactor: replace all ConfigureAwait(false) with ConfigureAwait(true)`
  - Code: `await vm.ReloadFromMcpAsyncInternal().ConfigureAwait(true);`
- `src/McpServerManager.Core/Commands/AsyncCommands.cs:98`
  - Blame: `23d7595cf9bc99b23470e49bfd17ccd9291de4ab` (`sharpninja`, `2026-02-20 19:41:23 +00:00`)
  - Summary: `perf: fix Android ANR - eliminate double MCP fetch and heavy JSON tree on startup`
  - Code: `sessions = await vm.McpSessionService.GetAllSessionsAsync(cancellationToken).ConfigureAwait(true);`
- `src/McpServerManager.Core/Commands/AsyncCommands.cs:132`
  - Blame: `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (`sharpninja`, `2026-02-20 03:29:39 +00:00`)
  - Summary: `refactor: complete CQRS mediator pattern for all ViewModels`
  - Code: `vm.BuildUnifiedSummaryAndIndexInternal(masterLog, summary);`
- `src/McpServerManager.Core/Commands/AsyncCommands.cs:226`
  - Blame: `2987350134f3b5c925fe58f8f1913671838e29ea` (`sharpninja`, `2026-02-20 04:03:29 +00:00`)
  - Summary: `fix: status bar animation stays active until all commands complete`
  - Code: `vm._mediator.TrackBackgroundWork(Task.Run(async () =>`
- `src/McpServerManager.Core/Commands/AsyncCommands.cs:230`
  - Blame: `74e099eee0ac7b45e9975b8260e681a82b918083` (`sharpninja`, `2026-02-20 18:25:19 +00:00`)
  - Summary: `refactor: replace all ConfigureAwait(false) with ConfigureAwait(true)`
  - Code: `var sessions = await vm.McpSessionService.GetAllSessionsAsync(cancellationToken).ConfigureAwait(true);`
- `src/McpServerManager.Core/Commands/AsyncCommands.cs:296`
  - Blame: `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (`sharpninja`, `2026-02-20 03:29:39 +00:00`)
  - Summary: `refactor: complete CQRS mediator pattern for all ViewModels`
  - Code: `vm.LoadJsonInternal(command.FilePath);`

#### B) Core ViewModel app-logic ownership violations
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:310`
  - Blame: `0e77cf3adbfc89355989de630e24a2b219bc4895` (`sharpninja`, `2026-02-21 07:37:48 +00:00`)
  - Summary: `feat: add workspace management UI and connection switching`
  - Code: `private void InitializeMcpEndpoint(string mcpBaseUrl)`
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:337`
  - Blame: `0e77cf3adbfc89355989de630e24a2b219bc4895` (`sharpninja`, `2026-02-21 07:37:48 +00:00`)
  - Summary: `feat: add workspace management UI and connection switching`
  - Code: `private void ApplyActiveMcpBaseUrl(string mcpBaseUrl)`
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:580`
  - Blame: `0000000000000000000000000000000000000000` (`Not Committed Yet`, `2026-02-23 16:25:47 +00:00`)
  - Summary: current working tree version
  - Code: `var service = new McpWorkspaceService(baseUrl);`
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:697`
  - Blame: `0000000000000000000000000000000000000000` (`Not Committed Yet`, `2026-02-23 16:25:47 +00:00`)
  - Summary: current working tree version
  - Code: `if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(filter) || !Directory.Exists(directory))`
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:702`
  - Blame: `0000000000000000000000000000000000000000` (`Not Committed Yet`, `2026-02-23 16:25:47 +00:00`)
  - Summary: current working tree version
  - Code: `_agentsReadmeWatcher = new FileSystemWatcher(directory, filter)`
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:776`
  - Blame: `0000000000000000000000000000000000000000` (`Not Committed Yet`, `2026-02-23 16:25:47 +00:00`)
  - Summary: current working tree version
  - Code: `private async Task LoadAgentsReadmeFileAsync(string filePath)`
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:853`
  - Blame: `0e77cf3adbfc89355989de630e24a2b219bc4895` (`sharpninja`, `2026-02-21 07:37:48 +00:00`)
  - Summary: `feat: add workspace management UI and connection switching`
  - Code: `var query = await _workspaceCatalogService.QueryAsync().ConfigureAwait(true);`
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:1937`
  - Blame: `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (`sharpninja`, `2026-02-20 03:29:39 +00:00`)
  - Summary: `refactor: complete CQRS mediator pattern for all ViewModels`
  - Code: `internal async Task ReloadFromMcpAsyncInternal()`
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:2436`
  - Blame: `d0b0a6ba09321101374f46d0278a27eb3555d74f` (`sharpninja`, `2026-02-19 21:43:12 +00:00`)
  - Summary: `Add Android project, CQRS infrastructure, and three-project architecture`
  - Code: `public void ArchiveInternal()`
- `src/McpServerManager.Core/ViewModels/MainWindowViewModel.cs:2666`
  - Blame: `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (`sharpninja`, `2026-02-20 03:29:39 +00:00`)
  - Summary: `refactor: complete CQRS mediator pattern for all ViewModels`
  - Code: `internal void LoadJsonInternal(string path)`
- `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs:72`
  - Blame: `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (`sharpninja`, `2026-02-20 03:29:39 +00:00`)
  - Summary: `refactor: complete CQRS mediator pattern for all ViewModels`
  - Code: `internal void OpenAgentConfigInternal()`
- `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs:89`
  - Blame: `d0b0a6ba09321101374f46d0278a27eb3555d74f` (`sharpninja`, `2026-02-19 21:43:12 +00:00`)
  - Summary: `Add Android project, CQRS infrastructure, and three-project architecture`
  - Code: `if (!File.Exists(path))`
- `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs:96`
  - Blame: `d0b0a6ba09321101374f46d0278a27eb3555d74f` (`sharpninja`, `2026-02-19 21:43:12 +00:00`)
  - Summary: `Add Android project, CQRS infrastructure, and three-project architecture`
  - Code: `Process.Start(new ProcessStartInfo`
- `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs:170`
  - Blame: `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (`sharpninja`, `2026-02-20 03:29:39 +00:00`)
  - Summary: `refactor: complete CQRS mediator pattern for all ViewModels`
  - Code: `internal async Task LoadModelsAsyncInternal()`
- `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs:174`
  - Blame: `74e099eee0ac7b45e9975b8260e681a82b918083` (`sharpninja`, `2026-02-20 18:25:19 +00:00`)
  - Summary: `refactor: replace all ConfigureAwait(false) with ConfigureAwait(true)`
  - Code: `var models = await OllamaLogAgentService.GetAvailableModelsAsync(null, CancellationToken.None).ConfigureAwait(true);`
- `src/McpServerManager.Core/ViewModels/ChatWindowViewModel.cs:207`
  - Blame: `0854c8a396bc0c16d692d94a928cb1e634b20bbe` (`sharpninja`, `2026-02-20 03:29:39 +00:00`)
  - Summary: `refactor: complete CQRS mediator pattern for all ViewModels`
  - Code: `internal async Task SendAsyncInternal()`
- `src/McpServerManager.Core/ViewModels/TodoListViewModel.cs:75`
  - Blame: `7864087e2f44de148b99001faed98d2fef0e5d6b` (`sharpninja`, `2026-02-20 16:19:50 +00:00`)
  - Summary: `feat: add Todo management tab with CQRS, MCP integration, and Desktop+Android views`
  - Code: `private void RegisterCqrsHandlers(McpTodoService service)`
- `src/McpServerManager.Core/ViewModels/TodoListViewModel.cs:96`
  - Blame: `0e77cf3adbfc89355989de630e24a2b219bc4895` (`sharpninja`, `2026-02-21 07:37:48 +00:00`)
  - Summary: `feat: add workspace management UI and connection switching`
  - Code: `RegisterCqrsHandlers(new McpTodoService(mcpBaseUrl));`
- `src/McpServerManager.Core/ViewModels/WorkspaceViewModel.cs:83`
  - Blame: `0e77cf3adbfc89355989de630e24a2b219bc4895` (`sharpninja`, `2026-02-21 07:37:48 +00:00`)
  - Summary: `feat: add workspace management UI and connection switching`
  - Code: `private void RegisterCqrsHandlers(McpWorkspaceService service)`
- `src/McpServerManager.Core/ViewModels/WorkspaceViewModel.cs:113`
  - Blame: `0e77cf3adbfc89355989de630e24a2b219bc4895` (`sharpninja`, `2026-02-21 07:37:48 +00:00`)
  - Summary: `feat: add workspace management UI and connection switching`
  - Code: `RegisterCqrsHandlers(new McpWorkspaceService(mcpBaseUrl));`

#### C) Desktop code-behind (current app) non-UI composition
- `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs:248`
  - Blame: `caa421ceefe7fc609d3f1d8b323a6a222034fd2d` (`sharpninja`, `2026-02-21 08:14:46 +00:00`)
  - Summary: `refactor`
  - Code: `var agentService = new McpServerManager.Core.Services.OllamaLogAgentService();`
- `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs:249`
  - Blame: `caa421ceefe7fc609d3f1d8b323a6a222034fd2d` (`sharpninja`, `2026-02-21 08:14:46 +00:00`)
  - Summary: `refactor`
  - Code: `var configModel = McpServerManager.Core.Models.AgentConfigIo.GetModelFromConfig();`
- `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs:250`
  - Blame: `caa421ceefe7fc609d3f1d8b323a6a222034fd2d` (`sharpninja`, `2026-02-21 08:14:46 +00:00`)
  - Summary: `refactor`
  - Code: `var chatVm = new ChatWindowViewModel(...);`
- `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs:270`
  - Blame: `d0b0a6ba09321101374f46d0278a27eb3555d74f` (`sharpninja`, `2026-02-19 21:43:12 +00:00`)
  - Summary: `Add Android project, CQRS infrastructure, and three-project architecture`
  - Code: `var s = LayoutSettingsIo.Load() ?? new LayoutSettings();`

#### D) Legacy project non-compliance attribution (`src/McpServerManager`)
- `src/McpServerManager/ViewModels/MainWindowViewModel.cs:191`
  - Blame: `f542e27696e10eb9816cb8a1622f3ec0e57307c5` (`sharpninja`, `2026-02-19 18:20:36 +00:00`)
  - Summary: `Refresh MCP session data on All JSON node clicks`
  - Code: `_mcpSessionService = new McpSessionLogService(GetMcpBaseUrl());`
- `src/McpServerManager/ViewModels/MainWindowViewModel.cs:222`
  - Blame: `f5eac34af57dc4e1511731f27203faeea4b3cb9d` (`sharpninja`, `2026-02-03 20:19:03 +00:00`)
  - Summary: `Agent config, prompts, chat, and UX improvements`
  - Code: `OllamaLogAgentService.TryStartOllamaIfNeeded();`
- `src/McpServerManager/ViewModels/MainWindowViewModel.cs:1006`
  - Blame: `74e099eee0ac7b45e9975b8260e681a82b918083` (`sharpninja`, `2026-02-20 18:25:19 +00:00`)
  - Summary: `refactor: replace all ConfigureAwait(false) with ConfigureAwait(true)`
  - Code: `var sessions = await _mcpSessionService.GetAllSessionsAsync(CancellationToken.None).ConfigureAwait(true);`
- `src/McpServerManager/ViewModels/ChatWindowViewModel.cs:146`
  - Blame: `74e099eee0ac7b45e9975b8260e681a82b918083` (`sharpninja`, `2026-02-20 18:25:19 +00:00`)
  - Summary: `refactor: replace all ConfigureAwait(false) with ConfigureAwait(true)`
  - Code: `var models = await OllamaLogAgentService.GetAvailableModelsAsync(null, CancellationToken.None).ConfigureAwait(true);`
- `src/McpServerManager/Views/MainWindow.axaml.cs:527`
  - Blame: `f5eac34af57dc4e1511731f27203faeea4b3cb9d` (`sharpninja`, `2026-02-03 20:19:03 +00:00`)
  - Summary: `Agent config, prompts, chat, and UX improvements`
  - Code: `var agentService = new Services.OllamaLogAgentService();`
- `src/McpServerManager/Views/MainWindow.axaml.cs:528`
  - Blame: `f5eac34af57dc4e1511731f27203faeea4b3cb9d` (`sharpninja`, `2026-02-03 20:19:03 +00:00`)
  - Summary: `Agent config, prompts, chat, and UX improvements`
  - Code: `var configModel = AgentConfigIo.GetModelFromConfig();`
- `src/McpServerManager/Views/MainWindow.axaml.cs:529`
  - Blame: `f5eac34af57dc4e1511731f27203faeea4b3cb9d` (`sharpninja`, `2026-02-03 20:19:03 +00:00`)
  - Summary: `Agent config, prompts, chat, and UX improvements`
  - Code: `var chatVm = new ChatWindowViewModel(...);`
- `src/McpServerManager/Views/MainWindow.axaml.cs:549`
  - Blame: `f3cf9ee417f15d0bcb6b4d16624fab6beb20ceb0` (`sharpninja`, `2026-02-03 23:19:38 +00:00`)
  - Summary: `Archive JSON, tree context menu, chat layout, details UI, search timestamp`
  - Code: `var s = LayoutSettingsIo.Load() ?? new LayoutSettings();`

### Attribution implications (for refactor planning)
- Several non-compliant patterns were introduced in commits labeled as CQRS refactors (`0854c8a...`, `d0b0a6b...`), which confirms the audit conclusion: CQRS was introduced structurally, but logic ownership was not fully migrated out of ViewModels.
- Some lines are last-touched by mechanical or incidental commits (`74e099e...` ConfigureAwait refactor, `2987350...` busy-state fix), so **blame should not be read as sole architectural ownership**, only as last-touch lineage.
- The AGENTS watcher / workspace-health non-compliance is currently in uncommitted working-tree changes (`Not Committed Yet`) and should be corrected before commit by moving watcher/file-load/health logic into handlers/services rather than preserving it in `MainWindowViewModel`.

## Explicitly Allowed / Not Flagged as Violations
- Markdown viewer code-behind assignment fallback in legacy shell:
  - Documented in `docs/EXCEPTION-EVALUATION.md`
  - Used to work around `Markdown.Avalonia` binding incompatibility
- Layout splitter/window persistence in code-behind:
  - Treated as UI concerns (especially after splitter persistence helper refactor)
- Control synchronization for `AvaloniaEdit` text bindings:
  - Treated as UI interop, not application logic

## Root Cause Summary (Why the drift happened)
- The project introduced CQRS incrementally without completing responsibility migration.
- Commands/handlers were often added as dispatch wrappers to keep `[RelayCommand]` call sites stable.
- ViewModels accumulated "internal" methods as the real application API.
- This created the appearance of CQRS while preserving the original ViewModel-centric application logic.

## Refactor Direction (Required for actual compliance)
To satisfy the clarified rule, the project must move from "CQRS wrappers" to "CQRS-owned application logic":

1. **Stop passing concrete ViewModels into commands**
- Commands should carry IDs, DTOs, user inputs, and selection tokens only.
- Handlers should depend on services and return result DTOs.

2. **Remove `*Internal(...)` as application API surface**
- Keep ViewModel methods focused on:
  - command/query dispatch
  - applying returned results to observable state
  - simple UI-only transforms

3. **Move app logic to handlers + services**
- Filesystem IO, process start, network calls, parsing, tree building, watchers, orchestration

4. **Move code-behind composition into an application/service layer**
- Shell code-behind should not instantiate business services or ViewModels with config IO dependencies.

5. **Treat legacy code explicitly**
- Either:
  - keep legacy project out of compliance scope and mark deprecated
  - or schedule migration/refactor work there too

## Implementation Kickoff Plan (This Session)
Begin by refactoring the **Chat subsystem** as a pilot because it is self-contained and demonstrates the anti-pattern clearly:

- Replace Chat CQRS wrapper handlers with real handlers that do not depend on `ChatWindowViewModel`
- Move chat app logic (model discovery, prompt file loading/opening, agent send call) into service-backed handlers
- Keep ViewModel as UI state + command/query dispatch + state application
- Leave UI-only behaviors (e.g., assigning selected prompt text to input, message list projection) in ViewModel where appropriate

This provides a concrete, correct pattern to apply next to `MainWindowViewModel`, `AsyncCommands`, and workspace/todo composition.

## Changes Made This Session (Session 3)
- Performed a repo-wide CQRS/code-behind compliance audit against the clarified rule.
- Captured systemic failure modes and measurable indicators.
- Recorded the strict implementation directive and refactor standard in this handoff.
- Next action (in-progress): start refactoring Chat CQRS handlers to remove ViewModel wrapper usage.

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
