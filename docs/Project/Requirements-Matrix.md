# Requirements Matrix (RequestTracker UI)

| Group | Requirement | Status | Source Files |
| --- | --- | --- | --- |
| 1.1.1 | FR-RTUI-001 | 🔶 Gap (planned migration) | `docs/todo.yaml`, `docs/architecture/compliance/UI-USECASE-MATRIX.md` |
| 1.1.2 | FR-RTUI-002 | 🔶 Gap (planned migration) | `docs/todo.yaml`, `docs/architecture/compliance/UI-USECASE-MATRIX.md` |
| 1.1.3 | FR-RTUI-003 | 🔶 Gap (planned migration) | `docs/todo.yaml`, `docs/architecture/compliance/UI-USECASE-MATRIX.md` |
| 1.2.1 | FR-RTUI-004 | 🔶 Gap (planned migration) | `docs/architecture/compliance/UI-USECASE-MATRIX.md` |
| 1.2.2 | FR-RTUI-005 | 🔶 Gap (planned migration) | `docs/todo.yaml` |
| 1.2.3 | FR-RTUI-006 | 🔶 Gap (planned migration) | `docs/todo.yaml` |
| 2.1.1 | FR-RTUI-101 | ✅ Observed (as-built) | `ConnectionViewModel`, `ConnectionWindow`, `ConnectionDialogView`, `App` |
| 2.1.2 | FR-RTUI-102 | ✅ Observed (as-built) | `MainWindowViewModel`, `AndroidConnectionPreferencesService` |
| 2.2.1 | FR-RTUI-103 | ✅ Observed (as-built) | `AdaptiveMainView`, `DeviceFormFactor`, `PhoneMainView`, `TabletMainView`, `MainWindow.axaml` |
| 2.2.2 | FR-RTUI-113 | ✅ Observed (as-built) | `MainWindowViewModel`, `PhoneMainView`, `TabletMainView` |
| 2.2.3 | FR-RTUI-115 | ✅ Observed (as-built) | `MainWindowViewModel`, `AgentsReadmeView` |
| 2.3.1 | FR-RTUI-104 | ✅ Observed (as-built) | `TodoListViewModel`, `TodoListView`, `PhoneTodoView` |
| 2.3.2 | FR-RTUI-105 | ✅ Observed (as-built) | `TodoListViewModel`, `TodoListView.axaml`, `PhoneTodoView` |
| 2.3.3 | FR-RTUI-106 | ✅ Observed (as-built) | `PhoneTodoView`, `AndroidBackNavigationService`, `MainActivity` |
| 2.4.1 | FR-RTUI-107 | ✅ Observed (as-built) | `WorkspaceViewModel`, `WorkspaceView` (Desktop/Android) |
| 2.5.1 | FR-RTUI-108 | ✅ Observed (as-built) | `MainWindowViewModel`, `McpServerManagerView`, `PhoneSessionLogView` |
| 2.6.1 | FR-RTUI-109 | ✅ Observed (as-built) | `VoiceConversationViewModel`, `SimplifiedVoiceView` |
| 2.6.2 | FR-RTUI-110 | ✅ Observed (as-built) | `SimplifiedVoiceView`, `AndroidVoiceAudioServices`, `VoiceSessionForegroundService` |
| 2.7.1 | FR-RTUI-111 | ✅ Observed (as-built) | `SettingsViewModel`, `SpeechFilterService` |
| 2.7.2 | FR-RTUI-112 | ✅ Observed (as-built) | `LogViewModel`, `LogView` (Desktop/Android) |
| 2.7.3 | FR-RTUI-114 | ✅ Observed (as-built) | `LayoutSettings`, `LayoutSettingsIo`, `SplitterLayoutPersistence`, `MainWindow` |
| 2.7.4 | FR-RTUI-117 | ✅ Observed (as-built) | `AndroidCrashDiagnostics`, `MainApplication`, `App`, `collect-android-crash-artifacts.ps1`, `android-crash-diagnostics-workflow.md` |
| 2.8.1 | FR-RTUI-116 | ✅ Observed (as-built) | `MainWindowViewModel`, `McpAgentEventStreamService`, `ISystemNotificationService` |
| 1.1.1 | TR-RTUI-001 | 🔶 Gap (planned migration) | `docs/todo.yaml` |
| 1.1.2 | TR-RTUI-002 | 🔶 Gap (planned migration) | `docs/architecture/compliance/UI-USECASE-MATRIX.md` |
| 1.1.3 | TR-RTUI-003 | 🔶 Gap (planned migration) | `docs/architecture/compliance/UI-USECASE-MATRIX.md` |
| 1.1.4 | TR-RTUI-004 | 🔶 Gap (planned migration) | `docs/architecture/compliance/UI-USECASE-MATRIX.md` |
| 1.2.1 | TR-RTUI-005 | 🔶 Gap (planned migration) | `docs/todo.yaml` |
| 1.2.2 | TR-RTUI-006 | 🔶 Gap (planned migration) | `docs/todo.yaml` |
| 2.1.1 | TR-RTUI-101 | ✅ Observed (as-built) | `ConnectionViewModel` |
| 2.1.2 | TR-RTUI-102 | ✅ Observed (as-built) | `ConnectionViewModel`, `McpOidcAuthService` |
| 2.1.3 | TR-RTUI-103 | ✅ Observed (as-built) | `AndroidQrScannerService`, `AndroidBrowserService`, `OidcWebViewActivity` |
| 2.1.4 | TR-RTUI-104 | ✅ Observed (as-built) | `OidcWebViewActivity`, `AndroidReturnToAppNotificationService` |
| 2.1.5 | TR-RTUI-105 | ✅ Observed (as-built) | `DesktopConnectionPreferencesService`, `AndroidConnectionPreferencesService` |
| 2.1.6 | TR-RTUI-106 | ✅ Observed (as-built) | `AndroidOidcJwtCacheInvalidationMonitor` |
| 2.2.1 | TR-RTUI-107 | ✅ Observed (as-built) | `MainWindowViewModel` |
| 2.2.2 | TR-RTUI-108 | ✅ Observed (as-built) | `MainWindowViewModel`, `TodoListViewModel`, `WorkspaceViewModel`, `VoiceConversationViewModel` |
| 2.2.3 | TR-RTUI-109 | ✅ Observed (as-built) | `MainWindowViewModel`, `WorkspaceViewModel` |
| 2.2.4 | TR-RTUI-110 | ✅ Observed (as-built) | `MainWindowViewModel` |
| 2.3.1 | TR-RTUI-111 | ✅ Observed (as-built) | `DeviceFormFactor`, `AdaptiveMainView`, `MainActivity` |
| 2.3.2 | TR-RTUI-112 | ✅ Observed (as-built) | `AndroidBackNavigationService`, `PhoneTodoView`, `PhoneSessionLogView` |
| 2.4.1 | TR-RTUI-113 | ✅ Observed (as-built) | `TodoListViewModel`, `TodoMarkdown` |
| 2.4.2 | TR-RTUI-114 | ✅ Observed (as-built) | `TodoListViewModel` |
| 2.4.3 | TR-RTUI-115 | ✅ Observed (as-built) | `WorkspaceViewModel` |
| 2.4.4 | TR-RTUI-116 | ✅ Observed (as-built) | `WorkspaceView` (Desktop), `WorkspaceViewModel` |
| 2.5.1 | TR-RTUI-117 | ✅ Observed (as-built) | `MainWindowViewModel`, `McpServerManagerView`, `PhoneSessionLogView` |
| 2.5.2 | TR-RTUI-118 | ✅ Observed (as-built) | `MainWindowViewModel`, `McpServerManagerView` |
| 2.6.1 | TR-RTUI-119 | ✅ Observed (as-built) | `VoiceConversationViewModel`, `McpVoiceConversationService` |
| 2.6.2 | TR-RTUI-120 | ✅ Observed (as-built) | `SimplifiedVoiceView` |
| 2.6.3 | TR-RTUI-121 | ✅ Observed (as-built) | `AndroidSpeechRecognitionService`, `AndroidTextToSpeechService` |
| 2.6.4 | TR-RTUI-122 | ✅ Observed (as-built) | `SimplifiedVoiceView`, `VoiceSessionForegroundService` |
| 2.6.5 | TR-RTUI-123 | ✅ Observed (as-built) | `SpeechFilterService`, `SettingsViewModel`, `SimplifiedVoiceView` |
| 2.7.1 | TR-RTUI-124 | ✅ Observed (as-built) | `LogViewModel`, `LogView` (Desktop/Android) |
| 2.7.2 | TR-RTUI-125 | ✅ Observed (as-built) | `LayoutSettings`, `LayoutSettingsIo`, `SplitterLayoutPersistence`, `MainWindow` |
| 2.7.3 | TR-RTUI-126 | ✅ Observed (as-built) | `MainWindowViewModel`, `McpAgentEventStreamService`, `ISystemNotificationService` |
| 2.7.4 | TR-RTUI-127 | ✅ Observed (as-built) | `AndroidCrashDiagnostics`, `MainApplication`, `App`, `AppLogService` |
| 2.7.5 | TR-RTUI-128 | ✅ Observed (as-built) | `AndroidCrashDiagnostics`, `AndroidPorcupineWakeWordEngine`, `AndroidVoskWakeWordEngine`, `collect-android-crash-artifacts.ps1`, `android-crash-diagnostics-workflow.md` |
