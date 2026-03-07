# MVVM Data Resolution Pattern

This workspace uses a strict MVVM source-of-truth pattern for runtime data.

## Rules
- Never push view-owned runtime data into services just to satisfy timing.
- ViewModels expose resolver/readiness functions for dynamic state.
- Views only initiate actions when ViewModel readiness gates pass.
- Service clients consume data through resolver delegates.
- Selection/switch workflows publish state changes; consumers react.

## Required Shape
- `ResolveXxx`: returns current value from source-of-truth state.
- `ResolveXxxReady`: returns `true` only when action preconditions are stable.
- Action handlers must guard with `ResolveXxxReady` before invoking network/session calls.

## Applied Example
- Workspace path resolution and voice-start readiness are implemented through:
  - `ResolveActiveWorkspacePath`
  - `ResolveWorkspaceReady`
  - `VoiceConversationViewModel.IsWorkspaceReady`

## Rollout Requirement
All apps in this workspace should follow this pattern for every runtime data flow:
- Desktop
- Android
- Shared UI.Core/Core viewmodels
- Director/Web viewmodels where applicable

Avoid imperative writes that bypass viewmodel state ownership.
