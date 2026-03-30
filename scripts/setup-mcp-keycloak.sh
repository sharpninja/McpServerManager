#!/usr/bin/env bash
set -euo pipefail

# McpServer Keycloak Setup Script
# Automates Keycloak realm and client setup for McpServer OIDC authentication.

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:7080}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-admin}"
REALM_NAME="${REALM_NAME:-mcpserver}"
MCP_SERVER_URL="${MCP_SERVER_URL:-http://localhost:7147}"

KEYCLOAK_URL="${KEYCLOAK_URL%/}"
MCP_SERVER_URL="${MCP_SERVER_URL%/}"

echo "========================================"
echo "McpServer Keycloak Setup"
echo "========================================"
echo "Keycloak URL: $KEYCLOAK_URL"
echo "Realm: $REALM_NAME"
echo "MCP Server URL: $MCP_SERVER_URL"
echo ""

keycloak_api() {
    local method="$1"
    local path="$2"
    local token="$3"
    local data="${4:-}"

    local url="${KEYCLOAK_URL}${path}"
    
    if [ -z "$data" ]; then
        curl -s -X "$method" "$url" \
            -H "Authorization: Bearer $token" \
            -H "Content-Type: application/json"
    else
        curl -s -X "$method" "$url" \
            -H "Authorization: Bearer $token" \
            -H "Content-Type: application/json" \
            -d "$data"
    fi
}

echo "[1/9] Authenticating with Keycloak..."

TOKEN_RESPONSE=$(curl -s -X POST "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password" \
    -d "client_id=admin-cli" \
    -d "username=${ADMIN_USER}" \
    -d "password=${ADMIN_PASSWORD}")

TOKEN=$(echo "$TOKEN_RESPONSE" | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)

if [ -z "$TOKEN" ]; then
    echo "  ✗ Authentication failed"
    exit 1
fi

echo "  ✓ Authenticated as $ADMIN_USER"

echo "[2/9] Creating realm '$REALM_NAME'..."

EXISTING_REALM=$(keycloak_api GET "/admin/realms/$REALM_NAME" "$TOKEN" 2>/dev/null || echo "")

if [ -n "$EXISTING_REALM" ] && echo "$EXISTING_REALM" | grep -q "\"realm\""; then
    echo "  ⚠ Realm '$REALM_NAME' already exists, skipping creation"
else
    REALM_CONFIG=$(cat <<EOF
{
    "realm": "$REALM_NAME",
    "enabled": true,
    "accessTokenLifespan": 3600,
    "accessTokenLifespanForImplicitFlow": 3600,
    "ssoSessionIdleTimeout": 3600,
    "ssoSessionMaxLifespan": 36000,
    "refreshTokenMaxReuse": 0,
    "revokeRefreshToken": false
}
EOF
)
    keycloak_api POST "/admin/realms" "$TOKEN" "$REALM_CONFIG" > /dev/null
    echo "  ✓ Realm '$REALM_NAME' created"
fi

echo "[3/9] Creating mcp-director client (public, Device Flow)..."

DIRECTOR_CLIENT_CONFIG=$(cat <<EOF
{
    "clientId": "mcp-director",
    "publicClient": true,
    "standardFlowEnabled": false,
    "directAccessGrantsEnabled": false,
    "serviceAccountsEnabled": false,
    "oauth2DeviceAuthorizationGrantEnabled": true,
    "attributes": {
        "oauth2.device.authorization.grant.enabled": "true"
    }
}
EOF
)

keycloak_api POST "/admin/realms/$REALM_NAME/clients" "$TOKEN" "$DIRECTOR_CLIENT_CONFIG" > /dev/null
echo "  ✓ Client 'mcp-director' created"

DIRECTOR_CLIENTS=$(keycloak_api GET "/admin/realms/$REALM_NAME/clients?clientId=mcp-director" "$TOKEN")
DIRECTOR_CLIENT_ID=$(echo "$DIRECTOR_CLIENTS" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

echo "[4/9] Adding protocol mappers to mcp-director..."

AUDIENCE_MAPPER=$(cat <<EOF
{
    "name": "mcp-server-api-audience",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-audience-mapper",
    "config": {
        "included.client.audience": "mcp-server-api",
        "id.token.claim": "true",
        "access.token.claim": "true"
    }
}
EOF
)

keycloak_api POST "/admin/realms/$REALM_NAME/clients/$DIRECTOR_CLIENT_ID/protocol-mappers/models" "$TOKEN" "$AUDIENCE_MAPPER" > /dev/null
echo "  ✓ Added audience mapper"

REALM_ROLES_MAPPER=$(cat <<EOF
{
    "name": "realm-roles",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-usermodel-realm-role-mapper",
    "config": {
        "claim.name": "realm_roles",
        "jsonType.label": "String",
        "multivalued": "true",
        "id.token.claim": "true",
        "access.token.claim": "true",
        "userinfo.token.claim": "true"
    }
}
EOF
)

keycloak_api POST "/admin/realms/$REALM_NAME/clients/$DIRECTOR_CLIENT_ID/protocol-mappers/models" "$TOKEN" "$REALM_ROLES_MAPPER" > /dev/null
echo "  ✓ Added realm-roles mapper"

echo "[5/9] Creating mcp-web client (confidential, Standard Flow)..."

WEB_CLIENT_CONFIG=$(cat <<EOF
{
    "clientId": "mcp-web",
    "publicClient": false,
    "standardFlowEnabled": true,
    "directAccessGrantsEnabled": false,
    "serviceAccountsEnabled": false,
    "redirectUris": [
        "http://localhost:*",
        "$MCP_SERVER_URL/*"
    ],
    "webOrigins": [
        "http://localhost:*",
        "$MCP_SERVER_URL"
    ]
}
EOF
)

keycloak_api POST "/admin/realms/$REALM_NAME/clients" "$TOKEN" "$WEB_CLIENT_CONFIG" > /dev/null
echo "  ✓ Client 'mcp-web' created"

WEB_CLIENTS=$(keycloak_api GET "/admin/realms/$REALM_NAME/clients?clientId=mcp-web" "$TOKEN")
WEB_CLIENT_ID=$(echo "$WEB_CLIENTS" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

echo "[6/9] Retrieving mcp-web client secret..."

WEB_CLIENT_SECRET=$(keycloak_api GET "/admin/realms/$REALM_NAME/clients/$WEB_CLIENT_ID/client-secret" "$TOKEN")
WEB_SECRET_VALUE=$(echo "$WEB_CLIENT_SECRET" | grep -o '"value":"[^"]*"' | cut -d'"' -f4)
echo "  ✓ Client secret retrieved"

echo "[7/9] Adding protocol mappers to mcp-web..."

WEB_AUDIENCE_MAPPER=$(cat <<EOF
{
    "name": "mcp-server-api-audience",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-audience-mapper",
    "config": {
        "included.client.audience": "mcp-server-api",
        "id.token.claim": "true",
        "access.token.claim": "true"
    }
}
EOF
)

keycloak_api POST "/admin/realms/$REALM_NAME/clients/$WEB_CLIENT_ID/protocol-mappers/models" "$TOKEN" "$WEB_AUDIENCE_MAPPER" > /dev/null
echo "  ✓ Added audience mapper"

WEB_REALM_ROLES_MAPPER=$(cat <<EOF
{
    "name": "realm-roles",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-usermodel-realm-role-mapper",
    "config": {
        "claim.name": "realm_roles",
        "jsonType.label": "String",
        "multivalued": "true",
        "id.token.claim": "true",
        "access.token.claim": "true",
        "userinfo.token.claim": "true"
    }
}
EOF
)

keycloak_api POST "/admin/realms/$REALM_NAME/clients/$WEB_CLIENT_ID/protocol-mappers/models" "$TOKEN" "$WEB_REALM_ROLES_MAPPER" > /dev/null
echo "  ✓ Added realm-roles mapper"

echo "[8/9] Creating realm roles..."

for role in admin agent-manager viewer; do
    EXISTING_ROLE=$(keycloak_api GET "/admin/realms/$REALM_NAME/roles/$role" "$TOKEN" 2>/dev/null || echo "")
    
    if [ -n "$EXISTING_ROLE" ] && echo "$EXISTING_ROLE" | grep -q "\"name\""; then
        echo "  ⚠ Role '$role' already exists"
    else
        ROLE_CONFIG=$(cat <<EOF
{
    "name": "$role"
}
EOF
)
        keycloak_api POST "/admin/realms/$REALM_NAME/roles" "$TOKEN" "$ROLE_CONFIG" > /dev/null
        echo "  ✓ Created role '$role'"
    fi
done

echo "[9/9] Setup complete!"
echo ""
echo "========================================"
echo "Setup Summary"
echo "========================================"
echo ""
echo "Realm: $REALM_NAME"
echo "Authority: $KEYCLOAK_URL/realms/$REALM_NAME"
echo ""
echo "Clients configured:"
echo "  • mcp-director (public, Device Flow)"
echo "  • mcp-web (confidential, Standard Flow)"
echo ""
echo "mcp-web client secret:"
echo "  $WEB_SECRET_VALUE"
echo ""
echo "Redirect URIs (mcp-web):"
echo "  • http://localhost:*"
echo "  • $MCP_SERVER_URL/*"
echo ""
echo "Web Origins (mcp-web):"
echo "  • http://localhost:*"
echo "  • $MCP_SERVER_URL"
echo ""
echo "Roles created: admin, agent-manager, viewer"
echo ""
echo "Next steps:"
echo "  1. Create users in Keycloak admin console ($KEYCLOAK_URL/admin)"
echo "  2. Assign realm roles to users"
echo "  3. Configure McpServer appsettings.json:"
echo ""
echo '     "Mcp": {'
echo '       "Auth": {'
echo "         \"Authority\": \"$KEYCLOAK_URL/realms/$REALM_NAME\","
echo '         "Audience": "mcp-server-api",'
echo '         "RequireHttpsMetadata": false,'
echo '         "DirectorClientId": "mcp-director"'
echo '       }'
echo '     }'
echo ""
echo "========================================"
