# Testing Requirements (RequestTracker UI)

Current audit result: there is no UI-focused automated test project in this workspace for `src/McpServerManager.*` (no `*Tests*.csproj` under `src/`). The requirements below define needed test coverage.

- TEST-RTUI-001: Given the UI use-case matrix, when each row is validated, then every active use case has a complete `RelayCommand -> Handler -> ViewModel mutation` chain.
- TEST-RTUI-002: Given endpoint inventory for TODO/Workspace/SessionLog/Health/Tunnel/Template domains, when coverage checks run, then each required endpoint has at least one mapped handler path.
- TEST-RTUI-003: Given host-specific UI layers, when architectural checks run, then shared endpoint logic exists only in shared UI/Core layers and not duplicated in host forks.
- TEST-RTUI-004: Given connection inputs, when invalid host/port and unreachable targets are used, then `ConnectionViewModel` rejects invalid values and reports actionable errors.
- TEST-RTUI-005: Given HTTP health probe redirects, when `/health` returns 30x, then the connect flow upgrades to the redirected scheme before auth and subsequent requests.
- TEST-RTUI-006: Given cached OIDC token paths, when token is valid then connect reuses it; when rejected then cache is cleared and interactive device flow resumes.
- TEST-RTUI-007: Given workspace switching, when preflight health fails, then the UI rolls back to prior workspace selection/base state and reports a reverted status message.
- TEST-RTUI-008: Given saved workspace key preferences, when app restarts, then shell selection restores to persisted workspace key before first full refresh.
- TEST-RTUI-009: Given TODO editor markdown, when create/update/save actions execute, then markdown round-trips to API DTOs without losing technical details, tasks, or requirement IDs.
- TEST-RTUI-010: Given streaming TODO prompt commands, when user triggers Stop, then active stream is canceled and UI state transitions to a stopped/canceled status.
- TEST-RTUI-011: Given Android phone TODO screens, when back is pressed in detail/edit states, then navigation consumes back events and returns to the expected prior screen.
- TEST-RTUI-012: Given workspace health timers, when a workspace remains selected, then indicator brush/tooltip refreshes on schedule and reflects health success/failure transitions.
- TEST-RTUI-013: Given session-log detail mode, when previous/next/back commands run, then request-detail navigation index remains coherent with current filtered search set.
- TEST-RTUI-014: Given voice turn submission (sync and SSE), when responses complete, then transcript items, tool calls, latency, and status text update consistently in ViewModel state.
- TEST-RTUI-015: Given Android continuous voice mode, when spoken commands (`send now`, `start over`, `pause`, `resume`, `end chat`) are recognized, then loop behavior matches command semantics.
- TEST-RTUI-016: Given speech filter settings import/save, when phrases are persisted, then TTS output skips lines containing configured phrases (case-insensitive substring matching).
- TEST-RTUI-017: Given log pause mode, when new entries arrive during pause and resume occurs, then buffered entries flush in order and copy/clear actions still operate correctly.
- TEST-RTUI-018: Given layout persistence, when window/tab/chat/splitter settings are saved and app restarts, then orientation-specific layout and selected tab are restored.
- TEST-RTUI-019: Given AGENTS watcher and agent event stream, when file change or actionable agent events occur, then shell status and notification surfaces update without crashing.
- TEST-RTUI-020: Given an Android fatal managed or Java uncaught exception, when the process restarts, then the shared crash handler replays the pending fatal report into app log/status surfaces and clears the pending artifact.
- TEST-RTUI-021: Given an Android wake-word session or other native-sensitive operation, when a crash bundle is collected with `collect-android-crash-artifacts.ps1`, then the output contains logcat, activity exit-info, and any app diagnostics/boundary artifacts available for the package.
