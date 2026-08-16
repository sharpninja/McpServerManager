# Use Case Designer

MCP Web includes a workspace-scoped Use Case Designer for editing semantic use-case records and their visual use-case diagram graph.

## Routes

- `/usecases` lists use cases for the active workspace.
- `/usecases/new` creates a new use case.
- `/usecases/{id}` edits semantic use-case detail.
- `/usecases/{id}/diagram` edits the visual diagram graph.

All routes require authentication and use the active workspace context. Changing workspace clears cached use-case state before new workspace data is loaded.

## Semantic Editor

The detail editor supports title, brief description, precondition, postcondition, scope, priority, approval status, product key, actors, flows, flow steps, and functional requirement links. The shared UI.Core view model calls the typed `SharpNinja.McpServer.Client` UseCases surface through the Web adapter; page components do not call page-level REST endpoints.

The client supports append/link operations for actors, flows, steps, and FR links. Existing nested actors, flows, and steps are displayed from the server detail response; update/delete of those nested records is not exposed by the current typed client surface.

## Diagram Editor

The diagram page uses a fresh SVG implementation in McpServerManager. It does not reuse the older McpServer canvas implementation.

Supported diagram operations:

- Add or select a system boundary.
- Add actor and use-case nodes.
- Rename and position selected nodes through the properties panel.
- Connect nodes with association, include, extend, or generalization edges.
- Delete selected nodes or edges.
- Save and reload `UseCaseDiagramGraph` values with `SystemBoundary`, `Nodes`, and `Edges`.
- Refresh the rendered Mermaid preview from the typed client.

## State And Validation

Save actions are disabled unless the detail or diagram is dirty and valid. Failed saves preserve local edits and display an actionable error. The shared view model tracks the loaded use-case ID and workspace for detail and diagram saves; if the workspace or loaded identity changes before a save, the stale state is cleared and the user must reload before writing.

## Test Coverage

Current tests cover list/detail/diagram loading, create/update, approval and product assignment, actor/flow/step/FR-link operations, graph add/move/rename/connect/delete/save, stale workspace save refusal, graph load failure behavior, and anonymous route redirects for use-case list, create, detail, and diagram routes.
