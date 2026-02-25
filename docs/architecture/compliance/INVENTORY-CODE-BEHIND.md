# Inventory: Desktop and Android Code-Behind Classification

Status: Phase 0 inventory baseline  
Scope: all Desktop and Android `.axaml.cs` files (`24` total code-behind files across repo; this inventory covers `20` Desktop+Android files)

## Classification Rules

- `UI-only`: event wiring, control sync, splitter/layout persistence, platform view behavior
- `Documented exception`: non-UI behavior with explicit exception rationale documented
- `Extraction required`: feature composition or app logic that must move out of code-behind

## Summary

- Desktop code-behind files: `9`
- Android code-behind files: `11`
- Extraction required (Phase 0): `1` file
- Documented exceptions (Phase 0): `4` files
- UI-only files (Phase 0): `15` files

High-confidence extraction target:

- `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs` (chat feature composition/config IO)

## Desktop Inventory (`9`)

| File | Classification | Notes |
|---|---|---|
| `src/McpServerManager.Desktop/App.axaml.cs` | Documented exception | App bootstrap/platform clipboard wiring and crash-log write (`crash.log`) are host-level concerns; keep minimal and documented |
| `src/McpServerManager.Desktop/Views/AgentsReadmeView.axaml.cs` | UI-only | `AvaloniaEdit` viewer sync and readonly control behavior |
| `src/McpServerManager.Desktop/Views/ChatWindow.axaml.cs` | UI-only | View control wiring/editor sync (no feature composition in current scan) |
| `src/McpServerManager.Desktop/Views/LogView.axaml.cs` | UI-only | View event wiring only |
| `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs` | Extraction required | Creates chat VM/services and performs config-model wiring; violates UI-only rule (`:249`, `:250`) |
| `src/McpServerManager.Desktop/Views/McpServerManagerView.axaml.cs` | Documented exception | Layout/splitter persistence (UI-only) plus Markdown binding workaround class historically treated as exception (`docs/EXCEPTION-EVALUATION.md`) |
| `src/McpServerManager.Desktop/Views/RequestDetailsView.axaml.cs` | UI-only | View interaction wiring |
| `src/McpServerManager.Desktop/Views/TodoListView.axaml.cs` | UI-only | List/editor sync and splitter persistence |
| `src/McpServerManager.Desktop/Views/WorkspaceView.axaml.cs` | UI-only | `AvaloniaEdit` sync and splitter persistence |

## Android Inventory (`11`)

| File | Classification | Notes |
|---|---|---|
| `src/McpServerManager.Android/App.axaml.cs` | Documented exception | App bootstrap/platform clipboard wiring and crash-log write |
| `src/McpServerManager.Android/Views/AdaptiveMainView.axaml.cs` | UI-only | Layout/view switching behavior |
| `src/McpServerManager.Android/Views/AnimatedStatusBar.axaml.cs` | UI-only | Visual control behavior |
| `src/McpServerManager.Android/Views/ConnectionDialogView.axaml.cs` | UI-only | Dialog UI interaction wiring |
| `src/McpServerManager.Android/Views/LogView.axaml.cs` | UI-only | View interaction wiring |
| `src/McpServerManager.Android/Views/McpServerManagerTabletView.axaml.cs` | Documented exception | UI layout persistence + Markdown workaround parity with desktop host view concerns |
| `src/McpServerManager.Android/Views/PhoneMainView.axaml.cs` | UI-only | Phone navigation/view wiring |
| `src/McpServerManager.Android/Views/RequestDetailsView.axaml.cs` | UI-only | View interaction wiring |
| `src/McpServerManager.Android/Views/TabletMainView.axaml.cs` | UI-only | Tablet host UI behavior |
| `src/McpServerManager.Android/Views/TodoListView.axaml.cs` | UI-only | List/editor sync |
| `src/McpServerManager.Android/Views/WorkspaceView.axaml.cs` | UI-only | `AvaloniaEdit` sync for workspace/global prompt editors |

## Evidence Snapshots for Extraction Classification

- Desktop MainWindow code-behind non-UI composition:
  - `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs:249`
  - `src/McpServerManager.Desktop/Views/MainWindow.axaml.cs:250`

- Legacy parity (not in this inventory scope, but confirms pattern origin):
  - `src/McpServerManager/Views/MainWindow.axaml.cs:528`
  - `src/McpServerManager/Views/MainWindow.axaml.cs:529`

## Follow-up Extraction Backlog

1. Move chat service/config composition out of `Desktop MainWindow.axaml.cs` into a compliant factory/composition layer.
2. Re-audit code-behind after that refactor and reduce extraction-required count to zero for Desktop+Android.

