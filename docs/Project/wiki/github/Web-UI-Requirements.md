# Web UI Requirements

This document tracks functional and technical requirements for the MCP Server browser UI and the Blazor Hybrid host packaged as the `mcp-web` .NET tool.

## Functional Requirements

### FR-MCP-014 Pairing Web UI

The server shall provide a browser-based login flow for authorized users to retrieve the server API key for MCP client configuration, backed by constant-time credential verification and HttpOnly session cookies where server-hosted pairing is enabled.

**Covered by:** MCP Server pairing endpoints and authentication middleware.

### FR-MCP-031 McpServer Management Web UI

The management UI shall provide workspace management, TODO navigation, session visibility, template testing, search, health, authentication, and agent chat surfaces for MCP Server workspaces.

**Status:** Complete.

**Covered by:** `McpServerManager.Web`, `McpServerManager.Web.Hybrid`, and shared `McpServerManager.UI.Core` services/view models.

### FR-MCP-WEB-HYBRID-001 Hybrid Host

The `mcp-web` global .NET tool shall launch the Blazor Hybrid host for MCP Web while reusing the shared Blazor source and UI.Core behavior used by the browser Web UI.

**Status:** Complete.

**Covered by:** `src/McpServerManager.Web.Hybrid` and the NUKE `UpdateWebUiTool` / `DeployAll` WebUi pipeline.

### FR-MCP-WEB-TRIAGE-001 Triage Dashboard

MCP Web shall expose the triage queue, report group queue, run history, triage-created TODOs, grouping/consolidation actions, run resubmission, filtering, sorting, and cross-workspace TODO navigation.

**Status:** Complete.

**Covered by:** shared triage services/view models and MCP Web triage page tests.

### FR-MCP-WEB-WORKSPACE-001 Workspace Context

Workspace selection shall be visible, changeable, and honored by dashboard, TODO, triage, use-case, and detail pages. Changing workspace shall invalidate cached page state so the new workspace is loaded before data is displayed.

**Status:** Complete.

**Covered by:** shared workspace context services, navigation helpers, and bUnit navigation tests.

### FR-MCP-WEB-USECASE-001 Use Case Designer

MCP Web shall list, create, edit, approve, assign product keys, link functional requirements, and edit SVG use-case diagrams for the active workspace.

**Status:** Complete.

**Covered by:** `src/McpServerManager.Web/Pages/UseCases`, `McpServerManager.UI.Core` use-case view models, the typed Web use-case adapter, and UI.Core/Web use-case tests.

## Technical Requirements

### TR-MCP-WEB-001 Shared UI.Core Backing

Browser Web UI and Blazor Hybrid host shall use shared UI.Core services and view models for MCP operations rather than duplicating endpoint logic in page components.

### TR-MCP-WEB-002 Workspace Credential Cache

When launched in a workspace, shared auth shall reuse a valid cached workspace token and remove an expired cached token before obtaining a fresh token from the identity server.

### TR-MCP-WEB-003 NUKE Packaging

The WebUi NUKE deployment path shall package `McpServerManager.Web.Hybrid` as the `SharpNinja.McpServer.Web` global .NET tool with command name `mcp-web`.

### TR-MCP-WEB-004 Test Coverage

bUnit tests shall cover navigation with and without auth, workspace-specific TODO routes, triage table usage, filtering and sorting controls, template test required-variable JSON behavior, and use-case list/detail/diagram routes.

### TR-MCP-WEB-005 Typed UseCases Client

Use-case list, detail, approval, product, FR-link, rendered diagram, and graph save/load behavior shall be routed through shared UI.Core abstractions backed by typed `SharpNinja.McpServer.Client` UseCases methods. Razor pages shall not issue page-level REST calls for use-case operations.

### TEST-MCP-WEB-USECASE-001 Use Case Designer Coverage

UI.Core and Web tests shall cover workspace-aware use-case loading, create/update, approval and product assignment, actor/flow/step/FR-link operations, graph load/save, SVG node and edge editing, anonymous route redirects, stale workspace save refusal, and error preservation.
