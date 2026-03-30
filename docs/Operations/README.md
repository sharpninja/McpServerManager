# Operations Documentation

This directory contains operational documentation for McpServer authentication and configuration.

## Contents

### [Client Architecture](./ClientArchitecture.md)
Describes the three OIDC clients configured in Keycloak for McpServer authentication:
- **mcp-server-api** — Confidential client for JWT validation on the API server
- **mcp-director** — Public client for CLI authentication via Device Authorization Flow
- **mcp-web** — Confidential client for browser-based authentication via Authorization Code Flow

Learn about the OAuth 2.0 flows used by each client, their configuration, and how they integrate with the McpServer codebase.

### [Setup Scripts](./SetupScripts.md)
Instructions for running the automated Keycloak setup scripts that:
- Create the `mcpserver` realm
- Configure all three OIDC clients
- Add protocol mappers for audience and realm roles
- Create realm roles (`admin`, `agent-manager`, `viewer`)
- Display client secrets for configuration

Includes both PowerShell (Windows) and bash (Linux/macOS) script documentation.

### [Helper Scripts](./HelperScripts.md)
Instructions for using helper scripts to sync client secrets from Keycloak into application configuration files:
- **Update-McpWebClientSecret.ps1** (PowerShell)
- **update-mcp-web-client-secret.sh** (bash)

These scripts automate updating the `mcp-web` client secret in `appsettings.Development.json` to ensure the Web UI stays in sync with Keycloak.

## Quick Start

1. **Start Keycloak**:
   ```bash
   docker compose -f infra/docker-compose.keycloak.yml up -d
   ```

2. **Run the setup script**:
   ```powershell
   # PowerShell
   .\scripts\Setup-McpKeycloak.ps1
   ```
   ```bash
   # Bash
   ./scripts/setup-mcp-keycloak.sh
   ```

3. **Sync the Web UI client secret**:
   ```powershell
   # PowerShell
   .\scripts\Update-McpWebClientSecret.ps1
   ```
   ```bash
   # Bash
   ./scripts/update-mcp-web-client-secret.sh
   ```

4. **Update the MCP server configuration** with the `mcp-server-api` client secret from the setup output

5. **Create users and assign roles** in the Keycloak admin console (`http://localhost:7080/admin`)

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                         Keycloak (IdP)                           │
│  Realm: mcpserver                                                │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐    │
│  │ mcp-server-api │  │  mcp-director  │  │    mcp-web     │    │
│  │ (confidential) │  │    (public)    │  │ (confidential) │    │
│  │ JWT validation │  │  Device Flow   │  │   Auth Code    │    │
│  └────────────────┘  └────────────────┘  └────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
         │                      │                      │
         │                      │                      │
         ▼                      ▼                      ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  MCP Server API  │  │  Director CLI    │  │  Web UI          │
│  Validates JWT   │  │  Device Flow     │  │  Auth Code Flow  │
│  tokens          │  │  authentication  │  │  + session       │
└──────────────────┘  └──────────────────┘  └──────────────────┘
```

### Token Flow
1. **Director CLI**: User authenticates via Device Flow → receives JWT → stores locally → includes in API requests
2. **Web UI**: User authenticates via Auth Code Flow → receives JWT → stored in HTTP-only cookie → included in API requests
3. **MCP Server**: Validates JWT tokens from both clients using `mcp-server-api` credentials

All tokens include:
- **Audience (`aud`)**: `mcp-server-api`
- **Realm Roles (`realm_roles`)**: User's assigned roles
- **Username (`preferred_username`)**: User's username

## Additional Resources

- **Keycloak Documentation**: https://www.keycloak.org/documentation
- **OAuth 2.0 Device Flow**: https://oauth.net/2/device-flow/
- **OAuth 2.0 Authorization Code Flow**: https://oauth.net/2/grant-types/authorization-code/
- **JWT Validation**: https://jwt.io/

## Support

For issues or questions:
1. Check the [Troubleshooting](./SetupScripts.md#troubleshooting) section in each document
2. Review the Keycloak logs: `docker compose -f infra/docker-compose.keycloak.yml logs -f`
3. Inspect the MCP server logs for authentication errors
