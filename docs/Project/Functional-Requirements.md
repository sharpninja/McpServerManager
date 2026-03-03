# Functional Requirements (RequestTracker UI)

This document combines:

- Projected UI requirements derived from the `McpServer` workspace requirements and the UI.Core migration directives.
- Newly captured requirements discovered during the as-built audit of `McpServerManager.Android` and `McpServerManager.Desktop`.

## Group Hierarchy (Up To 3 Levels)

1. Projected requirements from McpServer workspace
   1.1 Shared UI.Core command/handler parity contract
   1.2 Cross-host architecture and migration scope
2. As-built uncaptured requirements (Android + Desktop)
   2.1 Connection and workspace bootstrap
   2.2 Shell surfaces and status affordances
   2.3 Todo workflows
   2.4 Workspace workflows
   2.5 Session-log workflows
   2.6 Voice workflows
   2.7 Cross-cutting runtime UX
   2.8 Agent event notifications

## FR-RTUI-001 1.1.1 Shared UI.Core ViewModel and RelayCommand contract

All user-facing actions across Phone, Tablet, Desktop, TUI, and Web shall execute through `McpServer.UI.Core` ViewModels and `RelayCommand`/`IAsyncRelayCommand` contracts, with host-specific behavior layered on top.

**Projected from:** `FR-MCP-029`, `FR-MCP-030`, `FR-MCP-031`, `FR-MCP-037`

## FR-RTUI-002 1.1.2 Complete command-to-mutation path per use case

Each documented user use case shall have a complete `RelayCommand -> Handler -> ViewModel mutation` path with no method-only bypasses for active UI operations.

**Projected from:** `FR-MCP-029`

## FR-RTUI-003 1.1.3 Endpoint and handler parity for shared UI domains

For TODO, Workspace, Session Log, Health, Tunnel, and Template domains, each supported UI endpoint path shall be represented by at least one handler and corresponding UI command path.

**Projected from:** `FR-MCP-029`, `FR-MCP-049`

## FR-RTUI-004 1.2.1 Cross-surface behavior parity with explicit divergence notes

Common use cases shall have equivalent behavior across Phone, Tablet, Desktop, TUI, and Web. Any intentional divergence shall be documented and traceable.

**Projected from:** `FR-MCP-030`, `FR-MCP-031`

## FR-RTUI-005 1.2.2 Extension-only host customization

Host-specific UX behavior shall be implemented as extensions around shared UI logic rather than forks of endpoint logic in host-local ViewModels.

**Projected from:** `FR-MCP-030`, `FR-MCP-031`

## FR-RTUI-006 1.2.3 Director and Web host migration to RequestTracker baseline

Director (TUI) and Web UI hosts shall converge on the same shared command/runtime baseline used by the Android and Desktop apps in this workspace.

**Projected from:** `FR-MCP-030`, `FR-MCP-031`

## FR-RTUI-101 2.1.1 Connection bootstrap and authenticated session entry

The UI shall provide host/port connection, optional QR-assisted host capture (Android), OIDC device flow sign-in, and automatic reconnect from saved connection preferences.

**Covered by:** `ConnectionViewModel`, `ConnectionWindow`, `ConnectionDialogView`, `App` (Desktop/Android), `AndroidQrScannerService`

## FR-RTUI-102 2.1.2 Workspace selection with health-gated switching and rollback

The shell shall allow workspace selection, persist the selected workspace key, run preflight health checks during switch, and roll back to the previous workspace when switch validation fails.

**Covered by:** `MainWindowViewModel`, `WorkspaceConnectionOption`, `AndroidConnectionPreferencesService`

## FR-RTUI-103 2.2.1 Adaptive shell by form factor

Android shall switch between phone and tablet shells at runtime based on display width, while Desktop provides a multi-tab shell with Request Tracker, Todos, Voice, Workspaces, Logs, AGENTS, and Settings.

**Covered by:** `AdaptiveMainView`, `DeviceFormFactor`, `PhoneMainView`, `TabletMainView`, `MainWindow.axaml`

## FR-RTUI-113 2.2.2 Global status and shell quick actions

The shell shall expose live status text, workspace health indicator, app version, and logout. Android status text supports tap-to-dialog and long-press-to-copy affordances.

**Covered by:** `MainWindowViewModel`, `PhoneMainView`, `TabletMainView`, `MainWindow.axaml`

## FR-RTUI-115 2.2.3 AGENTS marker visibility in shell

Desktop shall expose an AGENTS tab that loads and refreshes `AGENTS-README-FIRST.yaml` metadata/content for the selected workspace.

**Covered by:** `MainWindowViewModel`, `AgentsReadmeView`, `MainWindow.axaml`

## FR-RTUI-104 2.3.1 TODO lifecycle management

Users shall be able to list, filter, group, create, open, edit, save, toggle done, copy ID, and delete TODO items from UI surfaces.

**Covered by:** `TodoListViewModel`, `TodoListView` (Desktop/Android), `PhoneTodoView`

## FR-RTUI-105 2.3.2 TODO AI-assisted workflows

Users shall be able to run requirements analysis and status/plan/implement prompt workflows from TODO context with streaming output and explicit cancellation/stop controls.

**Covered by:** `TodoListViewModel`, `PhoneTodoView`, `TodoListView.axaml` (Desktop/Android)

## FR-RTUI-106 2.3.3 Phone TODO drill-in navigation and back behavior

Android phone TODO UX shall support list, formatted detail, and markdown edit states with back navigation that unwinds the current screen stack before exiting the tab.

**Covered by:** `PhoneTodoView`, `AndroidBackNavigationService`, `MainActivity`

## FR-RTUI-107 2.4.1 Workspace lifecycle and prompt editing operations

Users shall be able to create/edit/delete workspaces, inspect status/health, and invoke init/start/stop operations. Workspace-level prompt templates are editable on Desktop and Android; global prompt template editing is provided on Desktop.

**Covered by:** `WorkspaceViewModel`, `WorkspaceView` (Desktop/Android), `WorkspaceView.axaml` (Desktop)

## FR-RTUI-108 2.5.1 Session-log explorer and request-detail inspection

Users shall be able to browse session logs, search/filter indexed request rows, open request details, navigate previous/next request details, and copy request payloads.

**Covered by:** `MainWindowViewModel`, `McpServerManagerView`, `PhoneSessionLogView`

## FR-RTUI-109 2.6.1 Voice conversation session lifecycle

Users shall be able to create/resume a voice session, submit turns (sync and stream), inspect transcript/tool calls, interrupt generation, send ESC, and end sessions.

**Covered by:** `VoiceConversationViewModel`, `SimplifiedVoiceView`, `VoiceConversationView`

## FR-RTUI-110 2.6.2 Android continuous voice loop and background resilience

Android voice UX shall support continuous listen/send/speak operation with spoken command detection (`send now`, `start over`, `clear chat`, `pause`, `resume`, `end chat`) and background session survivability.

**Covered by:** `SimplifiedVoiceView`, `AndroidVoiceAudioServices`, `VoiceSessionForegroundService`

## FR-RTUI-111 2.7.1 User-managed speech filter phrases

Users shall be able to manage speech filter phrases in Settings and apply them to TTS output filtering, including import from text, JSON, and YAML content.

**Covered by:** `SettingsViewModel`, `SpeechFilterService`, `SimplifiedVoiceView`

## FR-RTUI-112 2.7.2 Operational log inspection

Users shall be able to view runtime logs with level filtering, pause/resume, copy selected/all entries, and clear logs.

**Covered by:** `LogViewModel`, `LogView` (Desktop/Android)

## FR-RTUI-114 2.7.3 Layout and view-state persistence

Desktop shall persist window geometry/state, selected tab, chat window state, and splitter positions. Shared layout DTOs shall support orientation-aware splitter restoration.

**Covered by:** `LayoutSettings`, `LayoutSettingsIo`, `SplitterLayoutPersistence`, `MainWindow`, `McpServerManagerView`, `TodoListView`, `WorkspaceView`

## FR-RTUI-116 2.8.1 Agent-event surfaced status notifications

The shell shall listen for actionable agent lifecycle events and surface status updates and system notifications to users.

**Covered by:** `MainWindowViewModel`, `McpAgentEventStreamService`, `ISystemNotificationService`

## FR-RTUI-117 2.7.4 Android crash evidence recovery and operator collection workflow

Android operators shall be able to recover prior-launch crash evidence from app-managed diagnostics and collect a reproducible artifact bundle from an attached device using the documented adb workflow.

**Covered by:** `AndroidCrashDiagnostics`, `MainApplication`, `App`, `collect-android-crash-artifacts.ps1`, `android-crash-diagnostics-workflow.md`
