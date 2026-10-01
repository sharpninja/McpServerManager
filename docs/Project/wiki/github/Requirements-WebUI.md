# Web UI Requirements

This document tracks functional and technical requirements for the browser-based interfaces of McpServer, including the Pairing UI and the Management Dashboard.

## Functional Requirements

### FR-MCP-014 Pairing Web UI

The server shall provide a browser-based login flow for authorized users to retrieve the server API key for MCP client configuration, backed by SHA-256 constant-time password verification and HttpOnly session cookies.

**Covered by:** `PairingHtml`, `PairingOptions`, `PairingSessionService`

### FR-MCP-031 McpServer Management Web UI

A web-based management UI for McpServer providing workspace management, agent configuration, session log viewing, todo management, workspace memory, and system health monitoring. Integrates with the platform-wide open-source .NET OIDC provider for authentication.

### FR-MANAGER-MEMORY-002 Web Memory Pages

Mcp-Web SHALL expose Viewer-accessible Memory pages at `/memory` and `/memory/{Id}` with a NavLink after Todos and before Triage. Surfaces list/get/add/update/remove. Version is display-only. No Operator role and no expectedVersion/OCC.

**Status:** Shipped — PLAN-MANAGER-MEMORY-UI-001 closed with box H-done **AGREE** (Accuracy 99 / Completeness 99) on 2026-09-20. Manager tip `e1636b34545c0b3af428f59df202a18ee23ec3e5` (PR #7). Box MCP `1.0.0+720c2b49`.

**Lab:** Web `/memory` five-verb PASS (LIST/ADD/GET/UPDATE/REMOVE) plus NAV (Memory immediately after Todos). UPDATE confirmed Version 1→2 display-only. Receipt: `docs/receipts/PLAN-MANAGER-MEMORY-UI-001/lab-smoke-web-box-20260919T235547Z.md`.

**Covered by:** `Pages/Memory/MemoryList.razor`, `Pages/Memory/MemoryDetail.razor`, `MemoryApiClientAdapter`
