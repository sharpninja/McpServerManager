# Client Architecture

This document describes the three OIDC clients configured in Keycloak for McpServer authentication and their respective OAuth 2.0 flows.

## Overview

McpServer uses Keycloak as its identity provider with three distinct client configurations:

1. **mcp-server-api** — Confidential client for JWT validation on the API server
2. **mcp-director** — Public client for CLI authentication via Device Authorization Flow
3. **mcp-web** — Confidential client for browser-based authentication via Authorization Code Flow

All three clients are configured to work with the `mcpserver` realm and include the `mcp-server-api` audience in their tokens.

## 1. mcp-server-api (API Client)

### Purpose
This client validates JWT tokens issued by Keycloak on the McpServer API endpoints. It does not directly authenticate users but provides the API server with the credentials needed to verify tokens.

### Configuration
- **Client Type**: Confidential (has a client secret)
- **Client ID**: `mcp-server-api`
- **Service Accounts**: Enabled (for token introspection if needed)
- **Standard Flow**: Disabled (does not authenticate users directly)
- **Direct Access Grants**: Disabled
- **Audience**: Self-referencing (tokens issued to other clients must include `mcp-server-api` as audience)

### Usage in Code
The API server validates incoming JWT tokens using the client secret and authority URL configured in `appsettings.yaml`:

```yaml
Mcp:
  Auth:
    Authority: http://localhost:7080/realms/mcpserver
    Audience: mcp-server-api
    ClientSecret: <retrieved-from-keycloak>
    RequireHttpsMetadata: false
```

**Location**: `lib/McpServer/appsettings.yaml` (or environment variables/user secrets)

**Code Reference**: `lib/McpServer/src/McpServer.Services/Options/OidcAuthOptions.cs`

## 2. mcp-director (Director CLI Client)

### Purpose
This client authenticates CLI users via the OAuth 2.0 Device Authorization Flow. Users run the Director CLI, receive a user code, navigate to a verification URL in their browser, and authenticate through Keycloak. Once authenticated, the CLI receives an access token.

### Configuration
- **Client Type**: Public (no client secret required)
- **Client ID**: `mcp-director`
- **Device Authorization Grant**: Enabled
- **Standard Flow**: Disabled
- **Direct Access Grants**: Disabled
- **Service Accounts**: Disabled

### Protocol Mappers
- **mcp-server-api-audience**: Adds `mcp-server-api` to the `aud` claim
- **realm-roles**: Maps user realm roles into the `realm_roles` claim

### OAuth 2.0 Device Flow

The Device Authorization Flow is a multi-step process designed for CLI tools and devices without a web browser:

1. **Device Authorization Request**: The CLI calls `/auth/device` (proxied to Keycloak) with the client ID and scopes
2. **User Code Displayed**: Keycloak returns a `user_code`, `verification_uri`, and `device_code`
3. **User Authentication**: The user navigates to the `verification_uri` in a browser, enters the `user_code`, and authenticates with Keycloak
4. **Token Polling**: The CLI polls `/auth/token` (proxied to Keycloak) with the `device_code` until the user completes authentication
5. **Token Issued**: Once authenticated, Keycloak returns an `access_token` and `refresh_token`

The Director CLI caches tokens locally and automatically refreshes them when expired.

### Usage in Code

**CLI Implementation**: `src/McpServer.Director/Auth/OidcAuthService.cs`

The Director CLI discovers OIDC configuration from the MCP server:

```bash
director auth login
```

This:
1. Calls `GET /auth/config` on the MCP server to retrieve authority and client ID
2. Initiates the Device Flow by calling `POST /auth/device`
3. Displays the user code and verification URI
4. Polls `POST /auth/token` until the user completes authentication
5. Caches the token locally in `~/.mcp-director/token.json`

**Configuration Discovery**: `lib/McpServer/src/McpServer.Support.Mcp/Controllers/AuthConfigController.cs`

The MCP server exposes `/auth/config` to provide OIDC metadata to CLI clients:

```json
{
  "enabled": true,
  "authority": "http://localhost:7080/realms/mcpserver",
  "clientId": "mcp-director",
  "scopes": "openid profile email",
  "deviceAuthorizationEndpoint": "http://localhost:7147/auth/device",
  "tokenEndpoint": "http://localhost:7147/auth/token"
}
```

The MCP server proxies Device Flow requests to Keycloak so CLI clients only need to know the MCP server URL (e.g., `http://localhost:7147`), not the Keycloak URL.

## 3. mcp-web (Web UI Client)

### Purpose
This client authenticates browser-based users via the OAuth 2.0 Authorization Code Flow. Users navigate to the McpServer Web UI, click "Login", are redirected to Keycloak for authentication, and redirected back to the Web UI with an authorization code that is exchanged for tokens.

### Configuration
- **Client Type**: Confidential (has a client secret)
- **Client ID**: `mcp-web`
- **Standard Flow**: Enabled (Authorization Code Flow)
- **Direct Access Grants**: Disabled
- **Service Accounts**: Disabled
- **Redirect URIs**:
  - `http://localhost:*` (for local development)
  - `<MCP_SERVER_URL>/*` (for deployed environments)
- **Web Origins**:
  - `http://localhost:*`
  - `<MCP_SERVER_URL>`

### Protocol Mappers
- **mcp-server-api-audience**: Adds `mcp-server-api` to the `aud` claim
- **realm-roles**: Maps user realm roles into the `realm_roles` claim

### OAuth 2.0 Authorization Code Flow

The Authorization Code Flow is the standard OAuth 2.0 flow for browser-based applications:

1. **Login Initiated**: User clicks "Login" in the Web UI
2. **Redirect to Keycloak**: User is redirected to Keycloak's authorization endpoint
3. **Authentication**: User enters credentials in Keycloak
4. **Authorization Code**: Keycloak redirects back to the Web UI with an authorization code
5. **Token Exchange**: The Web UI backend exchanges the authorization code for an access token and refresh token using the client secret
6. **Token Storage**: Tokens are stored in an HTTP-only session cookie

### Usage in Code

**Web UI Configuration**: `src/McpServer.Web/appsettings.Development.json`

```json
{
  "Authentication": {
    "Schemes": {
      "OpenIdConnect": {
        "Authority": "http://localhost:7080/realms/mcpserver",
        "ClientId": "mcp-web",
        "ClientSecret": "<retrieved-from-keycloak>"
      }
    }
  }
}
```

**Code Reference**: `src/McpServer.Web/Program.cs`

The Web UI uses ASP.NET Core's OpenID Connect middleware to handle the Authorization Code Flow automatically. The middleware:
- Redirects unauthenticated requests to Keycloak
- Handles the callback with the authorization code
- Exchanges the code for tokens using the client secret
- Stores tokens in a secure session cookie
- Automatically refreshes tokens when expired

The Web UI can also discover OIDC configuration from the MCP server by setting `DiscoverAuthorityFromMcpAuthConfig: true` in `appsettings.json`.

## Token Validation

All three clients issue tokens that include:
- **Audience (`aud`)**: `mcp-server-api` (via the audience mapper)
- **Realm Roles (`realm_roles`)**: User's assigned roles (`admin`, `agent-manager`, `viewer`)
- **Subject (`sub`)**: Unique user identifier
- **Username (`preferred_username`)**: User's username
- **Email (`email`)**: User's email address

The MCP server validates these tokens using the `mcp-server-api` client credentials and the Keycloak authority URL.

## Security Considerations

### mcp-server-api
- **Client secret must be kept secure** and never exposed in client applications
- Configured via environment variables, user secrets, or secure configuration management
- Only used server-side for token validation

### mcp-director
- **Public client** (no client secret)
- Device Flow ensures user authentication happens in a browser, not in the CLI
- Tokens cached locally in `~/.mcp-director/token.json` with appropriate file permissions

### mcp-web
- **Client secret must be kept secure** and never exposed to the browser
- Stored server-side only (in `appsettings.Development.json` or user secrets)
- Tokens stored in HTTP-only, secure session cookies (not accessible to JavaScript)

## Next Steps

- [Setup Scripts](./SetupScripts.md) — Running the Keycloak setup automation
- [Helper Scripts](./HelperScripts.md) — Syncing client secrets into application configuration
