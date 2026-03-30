#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates the mcp-web client secret in appsettings.Development.json from Keycloak.

.DESCRIPTION
    Retrieves the mcp-web client secret from Keycloak via REST API and updates the
    Authentication:Schemes:OpenIdConnect:ClientSecret field in appsettings.Development.json
    using ConvertTo-Json/ConvertFrom-Json to preserve JSON structure.

.PARAMETER KeycloakUrl
    Base URL of the Keycloak server (default: http://localhost:7080)

.PARAMETER AdminUser
    Keycloak admin username (default: admin)

.PARAMETER AdminPassword
    Keycloak admin password (default: admin)

.PARAMETER RealmName
    Name of the realm where mcp-web client exists (default: mcpserver)

.PARAMETER AppsettingsPath
    Path to appsettings.Development.json file (default: src/McpServer.Web/appsettings.Development.json)

.EXAMPLE
    .\Update-McpWebClientSecret.ps1

.EXAMPLE
    .\Update-McpWebClientSecret.ps1 -KeycloakUrl "http://keycloak:8080" -RealmName "mcp"
#>

[CmdletBinding()]
param(
    [string]$KeycloakUrl = "http://localhost:7080",
    [string]$AdminUser = "admin",
    [string]$AdminPassword = "admin",
    [string]$RealmName = "mcpserver",
    [string]$AppsettingsPath = "src/McpServer.Web/appsettings.Development.json"
)

$ErrorActionPreference = "Stop"

$KeycloakUrl = $KeycloakUrl.TrimEnd('/')

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Update mcp-web Client Secret" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Keycloak URL: $KeycloakUrl"
Write-Host "Realm: $RealmName"
Write-Host "Appsettings: $AppsettingsPath"
Write-Host ""

Write-Host "[1/4] Authenticating with Keycloak..." -ForegroundColor Yellow

$tokenResponse = Invoke-RestMethod -Uri "$KeycloakUrl/realms/master/protocol/openid-connect/token" -Method Post -Body @{
    grant_type = "password"
    client_id = "admin-cli"
    username = $AdminUser
    password = $AdminPassword
} -ContentType "application/x-www-form-urlencoded"

$token = $tokenResponse.access_token
Write-Host "  ✓ Authenticated as $AdminUser" -ForegroundColor Green

Write-Host "[2/4] Retrieving mcp-web client ID..." -ForegroundColor Yellow

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$webClients = Invoke-RestMethod -Uri "$KeycloakUrl/admin/realms/$RealmName/clients?clientId=mcp-web" -Method Get -Headers $headers

if ($webClients.Count -eq 0) {
    Write-Error "Client 'mcp-web' not found in realm '$RealmName'"
    exit 1
}

$webClientId = $webClients[0].id
Write-Host "  ✓ Found mcp-web client (ID: $webClientId)" -ForegroundColor Green

Write-Host "[3/4] Retrieving mcp-web client secret..." -ForegroundColor Yellow

$webClientSecret = Invoke-RestMethod -Uri "$KeycloakUrl/admin/realms/$RealmName/clients/$webClientId/client-secret" -Method Get -Headers $headers
$webSecretValue = $webClientSecret.value
Write-Host "  ✓ Client secret retrieved" -ForegroundColor Green

Write-Host "[4/4] Updating appsettings.Development.json..." -ForegroundColor Yellow

if (-not (Test-Path $AppsettingsPath)) {
    Write-Error "Appsettings file not found at: $AppsettingsPath"
    exit 1
}

$appsettings = Get-Content $AppsettingsPath -Raw | ConvertFrom-Json
$appsettings.Authentication.Schemes.OpenIdConnect.ClientSecret = $webSecretValue
$appsettings | ConvertTo-Json -Depth 10 | Set-Content $AppsettingsPath
Write-Host "  ✓ Client secret updated in $AppsettingsPath" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Update Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Client secret has been updated to:" -ForegroundColor White
Write-Host "  $webSecretValue" -ForegroundColor Cyan
Write-Host ""
