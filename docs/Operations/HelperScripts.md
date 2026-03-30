# Helper Scripts

This document describes the helper scripts that automate syncing client secrets from Keycloak into application configuration files.

## Overview

After running the setup scripts, the `mcp-web` client secret needs to be synced into the Web UI's `appsettings.Development.json` file. The helper scripts automate this process by:
1. Authenticating with Keycloak
2. Retrieving the `mcp-web` client secret
3. Updating the `Authentication:Schemes:OpenIdConnect:ClientSecret` field in `appsettings.Development.json`

This ensures the Web UI always has the correct client secret without manual copy-paste.

## PowerShell Script (Windows)

### Location
```
scripts/Update-McpWebClientSecret.ps1
```

### Basic Usage
```powershell
.\scripts\Update-McpWebClientSecret.ps1
```

This uses default values:
- Keycloak URL: `http://localhost:7080`
- Admin username: `admin`
- Admin password: `admin`
- Realm name: `mcpserver`
- Appsettings path: `src/McpServer.Web/appsettings.Development.json`

### Custom Configuration
```powershell
.\scripts\Update-McpWebClientSecret.ps1 `
    -KeycloakUrl "http://keycloak:8080" `
    -AdminUser "admin" `
    -AdminPassword "MySecurePassword" `
    -RealmName "mcpserver" `
    -AppsettingsPath "src/McpServer.Web/appsettings.Development.json"
```

### Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-KeycloakUrl` | Base URL of the Keycloak server | `http://localhost:7080` |
| `-AdminUser` | Keycloak admin username | `admin` |
| `-AdminPassword` | Keycloak admin password | `admin` |
| `-RealmName` | Name of the realm where mcp-web client exists | `mcpserver` |
| `-AppsettingsPath` | Path to appsettings.Development.json file | `src/McpServer.Web/appsettings.Development.json` |

### Example Output
```
========================================
Update mcp-web Client Secret
========================================
Keycloak URL: http://localhost:7080
Realm: mcpserver
Appsettings: src/McpServer.Web/appsettings.Development.json

[1/4] Authenticating with Keycloak...
  ✓ Authenticated as admin
[2/4] Retrieving mcp-web client ID...
  ✓ Found mcp-web client (ID: f9e8d7c6-b5a4-3210-9876-543210fedcba)
[3/4] Retrieving mcp-web client secret...
  ✓ Client secret retrieved
[4/4] Updating appsettings.Development.json...
  ✓ Client secret updated in src/McpServer.Web/appsettings.Development.json

========================================
Update Complete!
========================================

Client secret has been updated to:
  z9y8x7w6-v5u4-3210-zyxw-vu9876543210

```

### How It Works

The PowerShell script:
1. Authenticates with Keycloak using the admin credentials
2. Queries the `mcp-web` client by client ID
3. Retrieves the client secret from Keycloak's client-secret endpoint
4. Loads `appsettings.Development.json` using `ConvertFrom-Json`
5. Updates the `Authentication.Schemes.OpenIdConnect.ClientSecret` property
6. Saves the file using `ConvertTo-Json` (preserving JSON structure)

This approach preserves all existing configuration in the file while only updating the client secret.

## Bash Script (Linux/macOS)

### Location
```
scripts/update-mcp-web-client-secret.sh
```

### Basic Usage
```bash
./scripts/update-mcp-web-client-secret.sh
```

### Custom Configuration with Command-Line Arguments
```bash
./scripts/update-mcp-web-client-secret.sh \
    --keycloak-url "http://keycloak:8080" \
    --admin-user "admin" \
    --admin-password "MySecurePassword" \
    --realm-name "mcpserver" \
    --appsettings-path "src/McpServer.Web/appsettings.Development.json"
```

### Command-Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `--keycloak-url URL` | Base URL of the Keycloak server | `http://localhost:7080` |
| `--admin-user USER` | Keycloak admin username | `admin` |
| `--admin-password PASS` | Keycloak admin password | `admin` |
| `--realm-name REALM` | Name of the realm where mcp-web client exists | `mcpserver` |
| `--appsettings-path PATH` | Path to appsettings.Development.json | `src/McpServer.Web/appsettings.Development.json` |
| `-h, --help` | Show help message | N/A |

### Example Output
The bash script produces the same output as the PowerShell script, with colored text for improved readability.

### How It Works

The bash script:
1. Authenticates with Keycloak using the admin credentials
2. Queries the `mcp-web` client by client ID
3. Retrieves the client secret from Keycloak's client-secret endpoint
4. Loads and updates `appsettings.Development.json` using:
   - **jq** (preferred, if available)
   - **Python 3** (fallback, if jq is not available)
5. Saves the file with the updated client secret

The script automatically detects which JSON tool is available and uses the appropriate method to preserve JSON structure.

### Prerequisites

The bash script requires one of the following:
- **jq** (recommended): `sudo apt install jq` or `brew install jq`
- **Python 3**: Usually pre-installed on Linux/macOS

If neither is available, the script will exit with an error message.

## When to Run

Run the helper script in the following scenarios:

### 1. After Initial Setup
After running the Keycloak setup script for the first time:
```powershell
.\scripts\Setup-McpKeycloak.ps1
.\scripts\Update-McpWebClientSecret.ps1
```

### 2. After Regenerating Client Secrets
If you regenerate the `mcp-web` client secret in Keycloak:
1. Navigate to the Keycloak admin console
2. Go to **Clients** → **mcp-web** → **Credentials**
3. Click **Regenerate Secret**
4. Run the helper script to sync the new secret

### 3. After Cloning the Repository
If a new developer clones the repository and needs to configure local development:
```powershell
.\scripts\Setup-McpKeycloak.ps1
.\scripts\Update-McpWebClientSecret.ps1
```

### 4. Continuous Integration / Deployment
In CI/CD pipelines where Keycloak is dynamically provisioned:
```bash
./scripts/setup-mcp-keycloak.sh
./scripts/update-mcp-web-client-secret.sh
```

## Security Considerations

### Client Secret Storage
The `mcp-web` client secret is stored in `appsettings.Development.json`, which is:
- **Committed to source control** for local development
- **Should be overridden** in production using:
  - **User Secrets** (`dotnet user-secrets set "Authentication:Schemes:OpenIdConnect:ClientSecret" "...")`)
  - **Environment Variables** (`Authentication__Schemes__OpenIdConnect__ClientSecret`)
  - **Azure Key Vault** or other secure configuration providers

### Production Best Practices
1. **Never commit production secrets** to source control
2. **Use environment variables** or secure configuration providers in production
3. **Rotate client secrets** regularly
4. **Restrict Keycloak admin access** to authorized personnel only

### File Permissions
Ensure `appsettings.Development.json` has appropriate file permissions:
- **Windows**: Ensure the file is not readable by all users
- **Linux/macOS**: Set permissions to `600` or `640`:
  ```bash
  chmod 600 src/McpServer.Web/appsettings.Development.json
  ```

## Troubleshooting

### Error: "Client 'mcp-web' not found in realm 'mcpserver'"
- Verify the realm name is correct
- Run the setup script first: `.\scripts\Setup-McpKeycloak.ps1`
- Check the Keycloak admin console to ensure the `mcp-web` client exists

### Error: "Appsettings file not found"
- Verify the appsettings path is correct (default: `src/McpServer.Web/appsettings.Development.json`)
- Ensure you're running the script from the repository root

### Error: "Neither jq nor python3 is available" (Bash)
Install one of the required JSON tools:
```bash
# Ubuntu/Debian
sudo apt install jq

# macOS
brew install jq

# Or ensure Python 3 is installed
python3 --version
```

### PowerShell Error: "ConvertFrom-Json: Invalid JSON"
- The `appsettings.Development.json` file may be malformed
- Restore the file from source control or manually fix JSON syntax errors
- Ensure the file contains valid JSON before running the script

## Manual Alternative

If the helper scripts are not available or fail, you can manually update the client secret:

1. **Retrieve the client secret from Keycloak**:
   - Navigate to `http://localhost:7080/admin`
   - Select the `mcpserver` realm
   - Go to **Clients** → **mcp-web** → **Credentials**
   - Copy the **Client Secret**

2. **Update appsettings.Development.json**:
   ```json
   {
     "Authentication": {
       "Schemes": {
         "OpenIdConnect": {
           "ClientSecret": "z9y8x7w6-v5u4-3210-zyxw-vu9876543210"
         }
       }
     }
   }
   ```

3. **Or use dotnet user-secrets**:
   ```bash
   cd src/McpServer.Web
   dotnet user-secrets set "Authentication:Schemes:OpenIdConnect:ClientSecret" "z9y8x7w6-v5u4-3210-zyxw-vu9876543210"
   ```

## Syncing the API Client Secret

The helper scripts currently only sync the `mcp-web` client secret. To sync the `mcp-server-api` client secret into the MCP server configuration:

### Option 1: Manual Update
Update `lib/McpServer/appsettings.yaml`:
```yaml
Mcp:
  Auth:
    ClientSecret: a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

### Option 2: Environment Variables
```bash
export Mcp__Auth__ClientSecret="a1b2c3d4-e5f6-7890-abcd-ef1234567890"
```

### Option 3: User Secrets
```bash
cd lib/McpServer/src/McpServer.Support.Mcp
dotnet user-secrets set "Mcp:Auth:ClientSecret" "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
```

A dedicated helper script for the API client secret may be added in the future.

## Next Steps

- [Client Architecture](./ClientArchitecture.md) — Understanding the three OIDC clients
- [Setup Scripts](./SetupScripts.md) — Running the Keycloak setup automation
