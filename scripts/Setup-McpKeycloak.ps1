#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automates Keycloak realm and client setup for McpServer OIDC authentication.

.DESCRIPTION
    Creates the mcpserver realm, configures mcp-server-api (confidential client for JWT validation),
    mcp-director (public client for Device Flow), and mcp-web (confidential client for web UI)
    clients with appropriate protocol mappers, redirect URIs, and audience claims.
    Displays client secrets in the setup summary.

.PARAMETER KeycloakUrl
    Base URL of the Keycloak server (default: http://localhost:7080)

.PARAMETER AdminUser
    Keycloak admin username (default: admin)

.PARAMETER AdminPassword
    Keycloak admin password (default: admin)

.PARAMETER RealmName
    Name of the realm to create (default: mcpserver)

.PARAMETER McpServerUrl
    Base URL of the MCP server for redirect URIs (default: http://localhost:7147)

.EXAMPLE
    .\Setup-McpKeycloak.ps1

.EXAMPLE
    .\Setup-McpKeycloak.ps1 -KeycloakUrl "http://keycloak:8080" -McpServerUrl "https://mcp.example.com"
#>

[CmdletBinding()]
param(
    [string]$KeycloakUrl = "http://localhost:7080",
    [string]$AdminUser = "admin",
    [string]$AdminPassword = "admin",
    [string]$RealmName = "mcpserver",
    [string]$McpServerUrl = "http://localhost:7147"
)

$ErrorActionPreference = "Stop"

$KeycloakUrl = $KeycloakUrl.TrimEnd('/')
$McpServerUrl = $McpServerUrl.TrimEnd('/')

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "McpServer Keycloak Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Keycloak URL: $KeycloakUrl"
Write-Host "Realm: $RealmName"
Write-Host "MCP Server URL: $McpServerUrl"
Write-Host ""

function Invoke-KeycloakApi {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        [object]$Body = $null
    )

    $uri = "$KeycloakUrl$Path"
    $headers = @{
        "Authorization" = "Bearer $Token"
        "Content-Type" = "application/json"
    }

    $params = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
    }

    if ($Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }

    try {
        return Invoke-RestMethod @params
    }
    catch {
        Write-Error "API call failed: $Method $Path - $_"
        throw
    }
}

Write-Host "[1/10] Authenticating with Keycloak..." -ForegroundColor Yellow

$tokenResponse = Invoke-RestMethod -Uri "$KeycloakUrl/realms/master/protocol/openid-connect/token" -Method Post -Body @{
    grant_type = "password"
    client_id = "admin-cli"
    username = $AdminUser
    password = $AdminPassword
} -ContentType "application/x-www-form-urlencoded"

$token = $tokenResponse.access_token
Write-Host "  ✓ Authenticated as $AdminUser" -ForegroundColor Green

Write-Host "[2/10] Creating realm '$RealmName'..." -ForegroundColor Yellow

$existingRealm = try {
    Invoke-KeycloakApi -Method Get -Path "/admin/realms/$RealmName" -Token $token
} catch { $null }

if ($existingRealm) {
    Write-Host "  ⚠ Realm '$RealmName' already exists, skipping creation" -ForegroundColor DarkYellow
} else {
    $realmConfig = @{
        realm = $RealmName
        enabled = $true
        accessTokenLifespan = 3600
        accessTokenLifespanForImplicitFlow = 3600
        ssoSessionIdleTimeout = 3600
        ssoSessionMaxLifespan = 36000
        refreshTokenMaxReuse = 0
        revokeRefreshToken = $false
    }
    Invoke-KeycloakApi -Method Post -Path "/admin/realms" -Token $token -Body $realmConfig
    Write-Host "  ✓ Realm '$RealmName' created" -ForegroundColor Green
}

Write-Host "[3/10] Creating mcp-server-api client (confidential, JWT validation)..." -ForegroundColor Yellow

$apiClientConfig = @{
    clientId = "mcp-server-api"
    publicClient = $false
    serviceAccountsEnabled = $true
    standardFlowEnabled = $false
    directAccessGrantsEnabled = $false
    attributes = @{
        "oauth2.device.authorization.grant.enabled" = "false"
    }
}

$apiClient = Invoke-KeycloakApi -Method Post -Path "/admin/realms/$RealmName/clients" -Token $token -Body $apiClientConfig
Write-Host "  ✓ Client 'mcp-server-api' created" -ForegroundColor Green

$apiClients = Invoke-KeycloakApi -Method Get -Path "/admin/realms/$RealmName/clients?clientId=mcp-server-api" -Token $token
$apiClientId = $apiClients[0].id

Write-Host "[4/10] Retrieving mcp-server-api client secret..." -ForegroundColor Yellow

$apiClientSecret = Invoke-KeycloakApi -Method Get -Path "/admin/realms/$RealmName/clients/$apiClientId/client-secret" -Token $token
$apiSecretValue = $apiClientSecret.value
Write-Host "  ✓ Client secret retrieved" -ForegroundColor Green

Write-Host "[5/10] Creating mcp-director client (public, Device Flow)..." -ForegroundColor Yellow

$directorClientConfig = @{
    clientId = "mcp-director"
    publicClient = $true
    standardFlowEnabled = $false
    directAccessGrantsEnabled = $false
    serviceAccountsEnabled = $false
    oauth2DeviceAuthorizationGrantEnabled = $true
    attributes = @{
        "oauth2.device.authorization.grant.enabled" = "true"
    }
}

$directorClient = Invoke-KeycloakApi -Method Post -Path "/admin/realms/$RealmName/clients" -Token $token -Body $directorClientConfig
Write-Host "  ✓ Client 'mcp-director' created" -ForegroundColor Green

$directorClients = Invoke-KeycloakApi -Method Get -Path "/admin/realms/$RealmName/clients?clientId=mcp-director" -Token $token
$directorClientId = $directorClients[0].id

Write-Host "[6/10] Adding protocol mappers to mcp-director..." -ForegroundColor Yellow

$audienceMapper = @{
    name = "mcp-server-api-audience"
    protocol = "openid-connect"
    protocolMapper = "oidc-audience-mapper"
    config = @{
        "included.client.audience" = "mcp-server-api"
        "id.token.claim" = "true"
        "access.token.claim" = "true"
    }
}

Invoke-KeycloakApi -Method Post -Path "/admin/realms/$RealmName/clients/$directorClientId/protocol-mappers/models" -Token $token -Body $audienceMapper
Write-Host "  ✓ Added audience mapper" -ForegroundColor Green

$realmRolesMapper = @{
    name = "realm-roles"
    protocol = "openid-connect"
    protocolMapper = "oidc-usermodel-realm-role-mapper"
    config = @{
        "claim.name" = "realm_roles"
        "jsonType.label" = "String"
        "multivalued" = "true"
        "id.token.claim" = "true"
        "access.token.claim" = "true"
        "userinfo.token.claim" = "true"
    }
}

Invoke-KeycloakApi -Method Post -Path "/admin/realms/$RealmName/clients/$directorClientId/protocol-mappers/models" -Token $token -Body $realmRolesMapper
Write-Host "  ✓ Added realm-roles mapper" -ForegroundColor Green

Write-Host "[7/10] Creating mcp-web client (confidential, Standard Flow)..." -ForegroundColor Yellow

$webClientConfig = @{
    clientId = "mcp-web"
    publicClient = $false
    standardFlowEnabled = $true
    directAccessGrantsEnabled = $false
    serviceAccountsEnabled = $false
    redirectUris = @(
        "http://localhost:*",
        "$McpServerUrl/*"
    )
    webOrigins = @(
        "http://localhost:*",
        "$McpServerUrl"
    )
}

$webClient = Invoke-KeycloakApi -Method Post -Path "/admin/realms/$RealmName/clients" -Token $token -Body $webClientConfig
Write-Host "  ✓ Client 'mcp-web' created" -ForegroundColor Green

$webClients = Invoke-KeycloakApi -Method Get -Path "/admin/realms/$RealmName/clients?clientId=mcp-web" -Token $token
$webClientId = $webClients[0].id

Write-Host "[8/10] Retrieving mcp-web client secret..." -ForegroundColor Yellow

$webClientSecret = Invoke-KeycloakApi -Method Get -Path "/admin/realms/$RealmName/clients/$webClientId/client-secret" -Token $token
$webSecretValue = $webClientSecret.value
Write-Host "  ✓ Client secret retrieved" -ForegroundColor Green

Write-Host "[9/10] Adding protocol mappers to mcp-web..." -ForegroundColor Yellow

$webAudienceMapper = @{
    name = "mcp-server-api-audience"
    protocol = "openid-connect"
    protocolMapper = "oidc-audience-mapper"
    config = @{
        "included.client.audience" = "mcp-server-api"
        "id.token.claim" = "true"
        "access.token.claim" = "true"
    }
}

Invoke-KeycloakApi -Method Post -Path "/admin/realms/$RealmName/clients/$webClientId/protocol-mappers/models" -Token $token -Body $webAudienceMapper
Write-Host "  ✓ Added audience mapper" -ForegroundColor Green

$webRealmRolesMapper = @{
    name = "realm-roles"
    protocol = "openid-connect"
    protocolMapper = "oidc-usermodel-realm-role-mapper"
    config = @{
        "claim.name" = "realm_roles"
        "jsonType.label" = "String"
        "multivalued" = "true"
        "id.token.claim" = "true"
        "access.token.claim" = "true"
        "userinfo.token.claim" = "true"
    }
}

Invoke-KeycloakApi -Method Post -Path "/admin/realms/$RealmName/clients/$webClientId/protocol-mappers/models" -Token $token -Body $webRealmRolesMapper
Write-Host "  ✓ Added realm-roles mapper" -ForegroundColor Green

Write-Host "[10/10] Creating realm roles..." -ForegroundColor Yellow

$roles = @("admin", "agent-manager", "viewer")

foreach ($role in $roles) {
    $existingRole = try {
        Invoke-KeycloakApi -Method Get -Path "/admin/realms/$RealmName/roles/$role" -Token $token
    } catch { $null }

    if ($existingRole) {
        Write-Host "  ⚠ Role '$role' already exists" -ForegroundColor DarkYellow
    } else {
        $roleConfig = @{
            name = $role
        }
        Invoke-KeycloakApi -Method Post -Path "/admin/realms/$RealmName/roles" -Token $token -Body $roleConfig
        Write-Host "  ✓ Created role '$role'" -ForegroundColor Green
    }
}

Write-Host "[10/10] Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Realm: $RealmName" -ForegroundColor White
Write-Host "Authority: $KeycloakUrl/realms/$RealmName" -ForegroundColor White
Write-Host ""
Write-Host "Clients configured:" -ForegroundColor White
Write-Host "  • mcp-server-api (confidential, JWT validation)" -ForegroundColor White
Write-Host "  • mcp-director (public, Device Flow)" -ForegroundColor White
Write-Host "  • mcp-web (confidential, Standard Flow)" -ForegroundColor White
Write-Host ""
Write-Host "mcp-server-api client secret:" -ForegroundColor Yellow
Write-Host "  $apiSecretValue" -ForegroundColor Cyan
Write-Host ""
Write-Host "mcp-web client secret:" -ForegroundColor Yellow
Write-Host "  $webSecretValue" -ForegroundColor Cyan
Write-Host ""
Write-Host "Redirect URIs (mcp-web):" -ForegroundColor White
Write-Host "  • http://localhost:*" -ForegroundColor White
Write-Host "  • $McpServerUrl/*" -ForegroundColor White
Write-Host ""
Write-Host "Web Origins (mcp-web):" -ForegroundColor White
Write-Host "  • http://localhost:*" -ForegroundColor White
Write-Host "  • $McpServerUrl" -ForegroundColor White
Write-Host ""
Write-Host "Roles created: admin, agent-manager, viewer" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Create users in Keycloak admin console ($KeycloakUrl/admin)" -ForegroundColor White
Write-Host "  2. Assign realm roles to users" -ForegroundColor White
Write-Host "  3. Configure McpServer appsettings.json:" -ForegroundColor White
Write-Host ""
Write-Host '     "Mcp": {' -ForegroundColor DarkGray
Write-Host '       "Auth": {' -ForegroundColor DarkGray
Write-Host "         `"Authority`": `"$KeycloakUrl/realms/$RealmName`"," -ForegroundColor DarkGray
Write-Host '         "Audience": "mcp-server-api",' -ForegroundColor DarkGray
Write-Host "         `"ClientSecret`": `"$apiSecretValue`"," -ForegroundColor DarkGray
Write-Host '         "RequireHttpsMetadata": false,' -ForegroundColor DarkGray
Write-Host '         "DirectorClientId": "mcp-director"' -ForegroundColor DarkGray
Write-Host '       }' -ForegroundColor DarkGray
Write-Host '     }' -ForegroundColor DarkGray
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
