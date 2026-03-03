# Technical Requirements (RequestTracker UI)

## Group Hierarchy (Up To 3 Levels)

1. Projected migration requirements
   1.1 UI.Core contract normalization
   1.2 Cross-host architecture scope
2. As-built uncaptured technical requirements (Android + Desktop)
   2.1 Connection and authentication runtime
   2.2 Workspace orchestration and context propagation
   2.3 Shell adaptation and navigation
   2.4 Todo/workspace interaction pipelines
   2.5 Session-log browsing pipelines
   2.6 Voice platform pipelines
   2.7 Cross-cutting UX/runtime observability

## TR-RTUI-001

**1.1.1 UI.Core composition root standardization** — Each UI host composition root shall register `McpServer.UI.Core` services, ViewModels, handlers, and dispatcher infrastructure as the primary command runtime.

**Projected from:** `TR-MCP-CQRS-001`, `TR-MCP-CQRS-005`, `TR-MCP-DIR-001`

## TR-RTUI-002

**1.1.2 Shared RelayCommand surface normalization** — Each active user action shall be represented by a `RelayCommand`/`IAsyncRelayCommand` surface in the shared ViewModel layer, not by host-only imperative methods.

**Projected from:** `TR-MCP-DIR-003`, `TR-MCP-TPL-004`

## TR-RTUI-003

**1.1.3 Endpoint-to-handler coverage matrix enforcement** — A maintained coverage matrix shall map `endpoint -> message -> handler -> expected result`, and CI shall fail when any required endpoint lacks handler coverage.

**Projected from:** `TR-MCP-TPL-004`

## TR-RTUI-004

**1.1.4 Use-case command chain matrix enforcement** — A maintained use-case matrix shall map `use case -> RelayCommand -> handler -> ViewModel mutation` and tag each use case by UI surface with documented divergence notes.

**Projected from:** `TR-MCP-DIR-001`

## TR-RTUI-005

**1.2.1 Extension-only host customization boundary** — Host-specific customization shall be implemented via extension ViewModels/wrappers; shared endpoint logic shall not be duplicated in host-local forks.

**Projected from:** `TR-MCP-DRY-001`

## TR-RTUI-006

**1.2.2 Director/Web parity runtime baseline** — Director and Web host bootstraps shall use the same shared command/runtime contracts as Android and Desktop within the RequestTracker workspace.

**Projected from:** `TR-MCP-DIR-001`, `TR-MCP-DIR-004`

## TR-RTUI-101

**2.1.1 Connection validation and health-probe upgrade path** — `ConnectionViewModel.ConnectAsync` shall validate host/port, probe `/health`, adopt redirect-upgraded scheme when necessary, and expose cancelable connect execution via `CancellationTokenSource`.

**Covered by:** `ConnectionViewModel`

## TR-RTUI-102

**2.1.2 OIDC token reuse and fallback flow** — OIDC sign-in shall support cached token reuse, interactive device authorization fallback, and post-auth API key acquisition with fallback behavior when token-based key retrieval is rejected.

**Covered by:** `ConnectionViewModel`, `McpOidcAuthService`

## TR-RTUI-103

**2.1.3 Android QR and in-app OIDC browser orchestration** — Android shall support QR-based host capture and in-app OIDC browser flow, including launch/return orchestration and verification URL opening.

**Covered by:** `AndroidQrScannerService`, `AndroidBrowserService`, `OidcWebViewActivity`

## TR-RTUI-104

**2.1.4 OIDC timeout and return-notification fallback** — Android OIDC WebView sign-in shall enforce timeout handling and provide a return-to-app notification fallback path.

**Covered by:** `OidcWebViewActivity`, `AndroidReturnToAppNotificationService`

## TR-RTUI-105

**2.1.5 Connection preference persistence contract** — Connection preferences shall persist host/port and OIDC JWT by host/port context; Android additionally persists selected workspace key for shell restoration.

**Covered by:** `DesktopConnectionPreferencesService`, `AndroidConnectionPreferencesService`

## TR-RTUI-106

**2.1.6 Unauthorized-token auto-invalidation behavior** — Unauthorized `/mcpserver/*` client failures shall trigger Android OIDC JWT cache invalidation to prevent stale token loops.

**Covered by:** `AndroidOidcJwtCacheInvalidationMonitor`

## TR-RTUI-107

**2.2.1 Workspace switch preflight and rollback flow** — Workspace switches shall run preflight health checks, resolve active API key/bearer context, update active workspace path, and revert prior connection state on failure.

**Covered by:** `MainWindowViewModel`

## TR-RTUI-108

**2.2.2 Workspace context fan-out contract** — Workspace-path changes shall fan out through `WorkspacePathChanged` to refresh TODO, Workspace, and Voice ViewModels without imperative host refresh calls for each VM.

**Covered by:** `MainWindowViewModel`, `TodoListViewModel`, `WorkspaceViewModel`, `VoiceConversationViewModel`

## TR-RTUI-109

**2.2.3 Workspace health timer and indicator state model** — Workspace health indicators shall be refreshed periodically by timer and rendered as status text plus color-coded brush state in shell and workspace views.

**Covered by:** `MainWindowViewModel`, `WorkspaceViewModel`

## TR-RTUI-110

**2.2.4 AGENTS marker watcher lifecycle** — Desktop shall watch `AGENTS-README-FIRST.yaml` for the selected workspace and update displayed file path, timestamp, and content on file-system changes.

**Covered by:** `MainWindowViewModel`

## TR-RTUI-111

**2.3.1 Adaptive form-factor routing contract** — Android form-factor routing shall classify tablet mode at `>= 600dp` width and dynamically replace phone/tablet root views on configuration change events.

**Covered by:** `DeviceFormFactor`, `AdaptiveMainView`, `MainActivity`

## TR-RTUI-112

**2.3.2 Centralized Android back-navigation broker** — Android back navigation shall route through a centralized callback broker allowing feature views (phone TODO, phone session log) to consume back events in reverse-registration order.

**Covered by:** `AndroidBackNavigationService`, `PhoneTodoView`, `PhoneSessionLogView`, `MainActivity`

## TR-RTUI-113

**2.4.1 TODO command/query mediation and markdown mapping** — TODO workflows shall be mediated through command/query handlers registered by `TodoListViewModel`, including markdown-to-model serialization/deserialization for editor operations.

**Covered by:** `TodoListViewModel`, `TodoMarkdown`, `McpTodoService`

## TR-RTUI-114

**2.4.2 Streaming prompt update and cancellation semantics** — Prompt streaming UI updates shall flush incremental output to the editor during SSE consumption and honor cancellation semantics through active command CTS instances.

**Covered by:** `TodoListViewModel`

## TR-RTUI-115

**2.4.3 Workspace command handler coverage contract** — Workspace management shall expose command handlers for load/refresh/create/update/delete/status/health/init/start/stop and synchronize editor state with selected workspace records.

**Covered by:** `WorkspaceViewModel`

## TR-RTUI-116

**2.4.4 Desktop global prompt editor binding contract** — Desktop workspace UI shall wire dedicated global prompt editor accessors and invoke load/save/reset global prompt commands against workspace API operations.

**Covered by:** `WorkspaceView` (Desktop), `WorkspaceViewModel`

## TR-RTUI-117

**2.5.1 Session-log indexed navigation contract** — Session-log request browsing shall support searchable indexed rows, list/detail transitions, details-mode navigation (previous/next), and context-sensitive visibility toggles.

**Covered by:** `MainWindowViewModel`, `McpServerManagerView`, `PhoneSessionLogView`

## TR-RTUI-118

**2.5.2 Session-log content action contract** — Session-log content operations shall support preview-in-browser, raw markdown toggle, and archive actions for eligible markdown artifacts.

**Covered by:** `MainWindowViewModel`, `McpServerManagerView`

## TR-RTUI-119

**2.6.1 Voice ViewModel sync and SSE turn pipeline** — Voice conversation ViewModel shall implement synchronous and SSE turn submission paths, transcript retrieval, tool-call capture, interrupt, ESC, and session end workflows.

**Covered by:** `VoiceConversationViewModel`, `McpVoiceConversationService`

## TR-RTUI-120

**2.6.2 Android continuous listen/detect/speak loop** — Android voice harness shall implement an iterative listen/command-detect/send/speak loop with spoken command parsing and pause/resume semantics.

**Covered by:** `SimplifiedVoiceView`

## TR-RTUI-121

**2.6.3 Android STT/TTS cancellable service contract** — Android speech recognition and TTS services shall provide cancellable one-shot recognition, utterance lifecycle callbacks, and language-tag aware playback.

**Covered by:** `AndroidSpeechRecognitionService`, `AndroidTextToSpeechService`

## TR-RTUI-122

**2.6.4 Foreground voice session continuity contract** — Android voice sessions shall run a foreground service with notification updates and periodic heartbeat behavior to maintain active sessions during long idle periods.

**Covered by:** `SimplifiedVoiceView`, `VoiceSessionForegroundService`

## TR-RTUI-123

**2.6.5 Speech-filter enforcement contract** — Speech filtering shall persist user phrase lists and enforce case-insensitive substring exclusion during TTS output.

**Covered by:** `SpeechFilterService`, `SettingsViewModel`, `SimplifiedVoiceView`

## TR-RTUI-124

**2.7.1 Log buffer/filter/pause runtime model** — Log viewing shall maintain a full entry buffer plus filtered projection, support pause buffering with flush-on-resume, and enforce context-menu pause safety during copy actions.

**Covered by:** `LogViewModel`, `LogView` (Desktop/Android)

## TR-RTUI-125

**2.7.2 Layout persistence serialization and restore contract** — Layout persistence shall serialize/deserialize window, tab, chat window, and splitter dimensions and restore orientation-specific split layouts across sessions.

**Covered by:** `LayoutSettings`, `LayoutSettingsIo`, `SplitterLayoutPersistence`, `MainWindow`, `McpServerManagerView`, `TodoListView`, `WorkspaceView`

## TR-RTUI-126

**2.7.3 Agent event notification fan-out contract** — Agent event notifications shall stream from backend change events, emit actionable status summaries, and invoke system notification services for user-visible lifecycle events.

**Covered by:** `MainWindowViewModel`, `McpAgentEventStreamService`, `ISystemNotificationService`

## TR-RTUI-127

**2.7.4 Android shared crash handler and persistence contract** — Android shall install a process-wide shared crash handler from `MainApplication`, route managed fatal callbacks and Java uncaught exceptions through that shared handler, persist fatal and diagnostic artifacts under the app files directory, and replay pending crash evidence on the next successful UI startup.

**Covered by:** `AndroidCrashDiagnostics`, `MainApplication`, `App`, `AppLogService`

## TR-RTUI-128

**2.7.5 Android native-boundary and adb artifact collection contract** — Native-sensitive Android operations shall publish active crash boundaries for postmortem correlation, and the repository shall provide a scriptable adb workflow that captures logcat, activity exit info, app diagnostics, and optional bugreport evidence for device crash reproduction.

**Covered by:** `AndroidCrashDiagnostics`, `AndroidPorcupineWakeWordEngine`, `AndroidVoskWakeWordEngine`, `collect-android-crash-artifacts.ps1`, `android-crash-diagnostics-workflow.md`
