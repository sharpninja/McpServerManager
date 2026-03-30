#!/usr/bin/env bash
#
# Updates the mcp-web client secret in appsettings.Development.json from Keycloak.
#
# Retrieves the mcp-web client secret from Keycloak via REST API and updates the
# Authentication.Schemes.OpenIdConnect.ClientSecret field in appsettings.Development.json
# using jq for JSON manipulation, with fallback to Python json module if jq is unavailable.
#
# Usage:
#   ./update-mcp-web-client-secret.sh [OPTIONS]
#
# Options:
#   --keycloak-url URL       Base URL of the Keycloak server (default: http://localhost:7080)
#   --admin-user USER        Keycloak admin username (default: admin)
#   --admin-password PASS    Keycloak admin password (default: admin)
#   --realm-name REALM       Name of the realm where mcp-web client exists (default: mcpserver)
#   --appsettings-path PATH  Path to appsettings.Development.json (default: src/McpServer.Web/appsettings.Development.json)
#   -h, --help               Show this help message
#
# Examples:
#   ./update-mcp-web-client-secret.sh
#   ./update-mcp-web-client-secret.sh --keycloak-url "http://keycloak:8080" --realm-name "mcp"

set -euo pipefail

# Default values
KEYCLOAK_URL="http://localhost:7080"
ADMIN_USER="admin"
ADMIN_PASSWORD="admin"
REALM_NAME="mcpserver"
APPSETTINGS_PATH="src/McpServer.Web/appsettings.Development.json"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --keycloak-url)
            KEYCLOAK_URL="$2"
            shift 2
            ;;
        --admin-user)
            ADMIN_USER="$2"
            shift 2
            ;;
        --admin-password)
            ADMIN_PASSWORD="$2"
            shift 2
            ;;
        --realm-name)
            REALM_NAME="$2"
            shift 2
            ;;
        --appsettings-path)
            APPSETTINGS_PATH="$2"
            shift 2
            ;;
        -h|--help)
            sed -n '2,/^$/p' "$0" | grep '^#' | sed 's/^# \?//'
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Remove trailing slash from Keycloak URL
KEYCLOAK_URL="${KEYCLOAK_URL%/}"

# Color codes
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
RED='\033[0;31m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}Update mcp-web Client Secret${NC}"
echo -e "${CYAN}========================================${NC}"
echo "Keycloak URL: $KEYCLOAK_URL"
echo "Realm: $REALM_NAME"
echo "Appsettings: $APPSETTINGS_PATH"
echo ""

# Function to update JSON using jq
update_json_with_jq() {
    local file="$1"
    local secret="$2"
    
    jq --arg secret "$secret" \
        '.Authentication.Schemes.OpenIdConnect.ClientSecret = $secret' \
        "$file" > "$file.tmp" && mv "$file.tmp" "$file"
}

# Function to update JSON using Python
update_json_with_python() {
    local file="$1"
    local secret="$2"
    
    python3 - "$file" "$secret" <<'EOF'
import sys
import json

file_path = sys.argv[1]
secret = sys.argv[2]

with open(file_path, 'r') as f:
    data = json.load(f)

data['Authentication']['Schemes']['OpenIdConnect']['ClientSecret'] = secret

with open(file_path, 'w') as f:
    json.dump(data, f, indent=2)
    f.write('\n')
EOF
}

# Determine which JSON tool to use
if command -v jq &> /dev/null; then
    JSON_TOOL="jq"
    UPDATE_JSON_FUNC=update_json_with_jq
elif command -v python3 &> /dev/null; then
    JSON_TOOL="python3"
    UPDATE_JSON_FUNC=update_json_with_python
else
    echo -e "${RED}Error: Neither jq nor python3 is available. Please install one of them.${NC}"
    exit 1
fi

echo -e "${YELLOW}[1/4] Authenticating with Keycloak...${NC}"

# Authenticate with Keycloak
TOKEN_RESPONSE=$(curl -s -X POST \
    "$KEYCLOAK_URL/realms/master/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password" \
    -d "client_id=admin-cli" \
    -d "username=$ADMIN_USER" \
    -d "password=$ADMIN_PASSWORD")

# Extract access token
if command -v jq &> /dev/null; then
    TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.access_token')
else
    TOKEN=$(echo "$TOKEN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['access_token'])")
fi

if [ -z "$TOKEN" ] || [ "$TOKEN" = "null" ]; then
    echo -e "${RED}Error: Failed to authenticate with Keycloak${NC}"
    exit 1
fi

echo -e "${GREEN}  ✓ Authenticated as $ADMIN_USER${NC}"

echo -e "${YELLOW}[2/4] Retrieving mcp-web client ID...${NC}"

# Retrieve mcp-web client
WEB_CLIENTS=$(curl -s -X GET \
    "$KEYCLOAK_URL/admin/realms/$REALM_NAME/clients?clientId=mcp-web" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json")

# Extract client ID
if command -v jq &> /dev/null; then
    WEB_CLIENT_ID=$(echo "$WEB_CLIENTS" | jq -r '.[0].id')
else
    WEB_CLIENT_ID=$(echo "$WEB_CLIENTS" | python3 -c "import sys, json; data = json.load(sys.stdin); print(data[0]['id'] if data else '')")
fi

if [ -z "$WEB_CLIENT_ID" ] || [ "$WEB_CLIENT_ID" = "null" ]; then
    echo -e "${RED}Error: Client 'mcp-web' not found in realm '$REALM_NAME'${NC}"
    exit 1
fi

echo -e "${GREEN}  ✓ Found mcp-web client (ID: $WEB_CLIENT_ID)${NC}"

echo -e "${YELLOW}[3/4] Retrieving mcp-web client secret...${NC}"

# Retrieve client secret
WEB_CLIENT_SECRET=$(curl -s -X GET \
    "$KEYCLOAK_URL/admin/realms/$REALM_NAME/clients/$WEB_CLIENT_ID/client-secret" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json")

# Extract secret value
if command -v jq &> /dev/null; then
    WEB_SECRET_VALUE=$(echo "$WEB_CLIENT_SECRET" | jq -r '.value')
else
    WEB_SECRET_VALUE=$(echo "$WEB_CLIENT_SECRET" | python3 -c "import sys, json; print(json.load(sys.stdin)['value'])")
fi

if [ -z "$WEB_SECRET_VALUE" ] || [ "$WEB_SECRET_VALUE" = "null" ]; then
    echo -e "${RED}Error: Failed to retrieve client secret${NC}"
    exit 1
fi

echo -e "${GREEN}  ✓ Client secret retrieved${NC}"

echo -e "${YELLOW}[4/4] Updating appsettings.Development.json...${NC}"

# Check if appsettings file exists
if [ ! -f "$APPSETTINGS_PATH" ]; then
    echo -e "${RED}Error: Appsettings file not found at: $APPSETTINGS_PATH${NC}"
    exit 1
fi

# Update the appsettings file
$UPDATE_JSON_FUNC "$APPSETTINGS_PATH" "$WEB_SECRET_VALUE"

echo -e "${GREEN}  ✓ Client secret updated in $APPSETTINGS_PATH${NC}"

echo ""
echo -e "${CYAN}========================================${NC}"
echo -e "${GREEN}Update Complete!${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""
echo -e "${WHITE}Client secret has been updated to:${NC}"
echo -e "${CYAN}  $WEB_SECRET_VALUE${NC}"
echo ""
