# Setup Scripts

This document describes how to run the Keycloak setup scripts that automate the creation and configuration of the `mcpserver` realm and its three OIDC clients.

## Overview

The setup scripts automate the following tasks:
1. Create the `mcpserver` realm in Keycloak
2. Configure the `mcp-server-api` client (confidential, JWT validation)
3. Configure the `mcp-director` client (public, Device Flow)
4. Configure the `mcp-web` client (confidential, Authorization Code Flow)
5. Add protocol mappers for audience and realm roles
6. Create realm roles (`admin`, `agent-manager`, `viewer`)
7. Display client secrets for manual configuration

## Prerequisites

Before running the setup scripts, ensure:
1. **Keycloak is running** (default: `http://localhost:7080`)
2. **Admin credentials are available** (default: `admin` / `admin`)
3. **PowerShell 5.1+** (Windows) or **bash with curl** (Linux/macOS)

## PowerShell Script (Windows)

### Location
```
scripts/Setup-McpKeycloak.ps1
```

### Basic Usage
```powershell
.\scripts\Setup-McpKeycloak.ps1
```

This uses default values:
- Keycloak URL: `http://localhost:7080`
- Admin username: `admin`
- Admin password: `admin`
- Realm name: `mcpserver`
- MCP Server URL: `http://localhost:7147`

### Custom Configuration
```powershell
.\scripts\Setup-McpKeycloak.ps1 `
    -KeycloakUrl "http://keycloak:8080" `
    -AdminUser "admin" `
    -AdminPassword "MySecurePassword" `
    -RealmName "mcpserver" `
    -McpServerUrl "https://mcp.example.com"
```

### Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-KeycloakUrl` | Base URL of the Keycloak server | `http://localhost:7080` |
| `-AdminUser` | Keycloak admin username | `admin` |
| `-AdminPassword` | Keycloak admin password | `admin` |
| `-RealmName` | Name of the realm to create | `mcpserver` |
| `-McpServerUrl` | Base URL of the MCP server for redirect URIs | `http://localhost:7147` |

### Example Output
```
========================================
McpServer Keycloak Setup
========================================
Keycloak URL: http://localhost:7080
Realm: mcpserver
MCP Server URL: http://localhost:7147

[1/10] Authenticating with Keycloak...
  ✓ Authenticated as admin
[2/10] Creating realm 'mcpserver'...
  ✓ Realm 'mcpserver' created
[3/10] Creating mcp-server-api client (confidential, JWT validation)...
  ✓ Client 'mcp-server-api' created
[4/10] Retrieving mcp-server-api client secret...
  ✓ Client secret retrieved
[5/10] Creating mcp-director client (public, Device Flow)...
  ✓ Client 'mcp-director' created
[6/10] Adding protocol mappers to mcp-director...
  ✓ Added audience mapper
  ✓ Added realm-roles mapper
[7/10] Creating mcp-web client (confidential, Standard Flow)...
  ✓ Client 'mcp-web' created
[8/10] Retrieving mcp-web client secret...
  ✓ Client secret retrieved
[9/10] Adding protocol mappers to mcp-web...
  ✓ Added audience mapper
  ✓ Added realm-roles mapper
[10/10] Creating realm roles...
  ✓ Created role 'admin'
  ✓ Created role 'agent-manager'
  ✓ Created role 'viewer'
[10/10] Setup complete!

========================================
Setup Summary
========================================

Realm: mcpserver
Authority: http://localhost:7080/realms/mcpserver

Clients configured:
  • mcp-server-api (confidential, JWT validation)
  • mcp-director (public, Device Flow)
  • mcp-web (confidential, Standard Flow)

mcp-server-api client secret:
  a1b2c3d4-e5f6-7890-abcd-ef1234567890

mcp-web client secret:
  z9y8x7w6-v5u4-3210-zyxw-vu9876543210

Redirect URIs (mcp-web):
  • http://localhost:*
  • http://localhost:7147/*

Web Origins (mcp-web):
  • http://localhost:*
  • http://localhost:7147

Roles created: admin, agent-manager, viewer

Next steps:
  1. Create users in Keycloak admin console (http://localhost:7080/admin)
  2. Assign realm roles to users
  3. Configure McpServer appsettings.json:

     "Mcp": {
       "Auth": {
         "Authority": "http://localhost:7080/realms/mcpserver",
         "Audience": "mcp-server-api",
         "ClientSecret": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
         "RequireHttpsMetadata": false,
         "DirectorClientId": "mcp-director"
       }
     }

========================================
```

## Bash Script (Linux/macOS)

### Location
```
scripts/setup-mcp-keycloak.sh
```

### Basic Usage
```bash
./scripts/setup-mcp-keycloak.sh
```

### Custom Configuration with Environment Variables
```bash
export KEYCLOAK_URL="http://keycloak:8080"
export ADMIN_USER="admin"
export ADMIN_PASSWORD="MySecurePassword"
export REALM_NAME="mcpserver"
export MCP_SERVER_URL="https://mcp.example.com"
./scripts/setup-mcp-keycloak.sh
```

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `KEYCLOAK_URL` | Base URL of the Keycloak server | `http://localhost:7080` |
| `ADMIN_USER` | Keycloak admin username | `admin` |
| `ADMIN_PASSWORD` | Keycloak admin password | `admin` |
| `REALM_NAME` | Name of the realm to create | `mcpserver` |
| `MCP_SERVER_URL` | Base URL of the MCP server for redirect URIs | `http://localhost:7147` |

### Example Output
The bash script produces the same output as the PowerShell script, with colored text for improved readability.

## Idempotency

Both scripts are idempotent and can be run multiple times safely:
- If the realm already exists, it skips creation
- If roles already exist, they are skipped
- Client secrets remain unchanged if clients already exist

**Note**: Existing protocol mappers are not updated. To reconfigure, delete the clients in Keycloak and re-run the script.

## Post-Setup Steps

After running the setup script:

### 1. Create Users in Keycloak
1. Navigate to the Keycloak admin console: `http://localhost:7080/admin`
2. Log in with admin credentials
3. Select the `mcpserver` realm
4. Go to **Users** → **Add user**
5. Set username, email, and other attributes
6. Go to **Credentials** → **Set password** (uncheck "Temporary")

### 2. Assign Roles to Users
1. In the user detail page, go to **Role Mappings**
2. Under **Realm Roles**, assign one or more roles:
   - `admin` — Full administrative access
   - `agent-manager` — Manage agents and sessions
   - `viewer` — Read-only access

### 3. Configure MCP Server
Update `lib/McpServer/appsettings.yaml` with the client secret displayed in the setup summary:

```yaml
Mcp:
  Auth:
    Authority: http://localhost:7080/realms/mcpserver
    Audience: mcp-server-api
    ClientSecret: a1b2c3d4-e5f6-7890-abcd-ef1234567890
    RequireHttpsMetadata: false
    DirectorClientId: mcp-director
```

Or use environment variables:
```bash
export Mcp__Auth__Authority="http://localhost:7080/realms/mcpserver"
export Mcp__Auth__Audience="mcp-server-api"
export Mcp__Auth__ClientSecret="a1b2c3d4-e5f6-7890-abcd-ef1234567890"
export Mcp__Auth__RequireHttpsMetadata="false"
export Mcp__Auth__DirectorClientId="mcp-director"
```

### 4. Configure Web UI
Use the helper script to sync the `mcp-web` client secret into the Web UI configuration:

```powershell
.\scripts\Update-McpWebClientSecret.ps1
```

See [Helper Scripts](./HelperScripts.md) for details.

## Troubleshooting

### Error: "Failed to authenticate with Keycloak"
- Verify Keycloak is running: `curl http://localhost:7080/health`
- Check admin credentials in Keycloak admin console
- Ensure the `admin-cli` client is enabled in the `master` realm

### Error: "Client 'mcp-server-api' already exists"
This is expected if re-running the script. The script skips existing clients but does not update them.

To force recreation:
1. Delete the clients in Keycloak admin console
2. Re-run the setup script

### Error: "Realm 'mcpserver' already exists"
This is expected and safe. The script skips realm creation if it already exists.

## Keycloak Token Lifespan Configuration

The setup script configures the following token lifespans in the `mcpserver` realm:
- **Access Token Lifespan**: 3600 seconds (1 hour)
- **SSO Session Idle Timeout**: 3600 seconds (1 hour)
- **SSO Session Max Lifespan**: 36000 seconds (10 hours)
- **Refresh Token Max Reuse**: 0 (refresh tokens can be reused)
- **Revoke Refresh Token**: false (refresh tokens are not revoked after use)

These values ensure tokens remain valid for at least 1 hour, which is enforced by the MCP server's token lifetime validation.

To adjust these values:
1. Navigate to the Keycloak admin console: `http://localhost:7080/admin`
2. Select the `mcpserver` realm
3. Go to **Realm Settings** → **Tokens**
4. Adjust the lifespan values as needed

## Next Steps

- [Client Architecture](./ClientArchitecture.md) — Understanding the three OIDC clients
- [Helper Scripts](./HelperScripts.md) — Syncing client secrets into application configuration
