#!/usr/bin/env bash
# build-deb.sh — Build a .deb package for McpServerManager Desktop (Linux)
#
# Usage:
#   ./scripts/build-deb.sh [--version <semver>] [--configuration Release|Debug]
#                           [--rid linux-x64] [--no-build] [--output-dir artifacts]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
WORKSPACE_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Defaults
VERSION=""
CONFIGURATION="Release"
RID="linux-x64"
NO_BUILD=false
OUTPUT_DIR="$WORKSPACE_ROOT/artifacts"
PROJECT="$WORKSPACE_ROOT/src/McpServerManager.Desktop/McpServerManager.Desktop.csproj"

# Parse args
while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)        VERSION="$2"; shift 2 ;;
        --configuration)  CONFIGURATION="$2"; shift 2 ;;
        --rid)            RID="$2"; shift 2 ;;
        --no-build)       NO_BUILD=true; shift ;;
        --output-dir)     OUTPUT_DIR="$2"; shift 2 ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

# Version detection
if [ -z "$VERSION" ]; then
    if command -v dotnet-gitversion &>/dev/null; then
        VERSION=$(dotnet-gitversion /showvariable SemVer 2>/dev/null || true)
    fi
    if [ -z "$VERSION" ] && command -v dotnet &>/dev/null; then
        VERSION=$(dotnet tool run dotnet-gitversion /showvariable SemVer 2>/dev/null || true)
    fi
    if [ -z "$VERSION" ]; then
        VERSION=$(git describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || echo "0.1.0")
    fi
fi
echo "Version: $VERSION"

PKG_NAME="mcpservermanager"
PUBLISH_DIR="$OUTPUT_DIR/publish-linux"
DEB_ROOT="$OUTPUT_DIR/deb-staging"
DEB_FILE="$OUTPUT_DIR/${PKG_NAME}_${VERSION}_amd64.deb"

# Publish
if [ "$NO_BUILD" = false ]; then
    echo "Publishing $PROJECT -> $PUBLISH_DIR"
    dotnet publish "$PROJECT" \
        -c "$CONFIGURATION" \
        -r "$RID" \
        -f net9.0 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$PUBLISH_DIR"
fi

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "ERROR: Publish output not found at $PUBLISH_DIR" >&2
    exit 1
fi

# Find the main executable
EXE_NAME="McpServerManager.Desktop"
if [ ! -f "$PUBLISH_DIR/$EXE_NAME" ]; then
    # Fallback: first executable
    EXE_NAME=$(find "$PUBLISH_DIR" -maxdepth 1 -type f -executable | head -1 | xargs basename)
fi

# Clean staging
rm -rf "$DEB_ROOT"
mkdir -p "$DEB_ROOT/DEBIAN"
mkdir -p "$DEB_ROOT/opt/mcpservermanager"
mkdir -p "$DEB_ROOT/usr/bin"
mkdir -p "$DEB_ROOT/usr/share/applications"
mkdir -p "$DEB_ROOT/usr/share/icons/hicolor/128x128/apps"
mkdir -p "$DEB_ROOT/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$DEB_ROOT/usr/share/icons/hicolor/512x512/apps"

# Copy published files
cp -r "$PUBLISH_DIR"/* "$DEB_ROOT/opt/mcpservermanager/"
chmod +x "$DEB_ROOT/opt/mcpservermanager/$EXE_NAME"

# Symlink to /usr/bin
ln -sf "/opt/mcpservermanager/$EXE_NAME" "$DEB_ROOT/usr/bin/mcpservermanager"

# Icons
ICON_DIR="$WORKSPACE_ROOT/src/McpServerManager.Core/Assets"
if [ -f "$ICON_DIR/logo-128.png" ]; then
    cp "$ICON_DIR/logo-128.png" "$DEB_ROOT/usr/share/icons/hicolor/128x128/apps/mcpservermanager.png"
fi
if [ -f "$ICON_DIR/logo-256.png" ]; then
    cp "$ICON_DIR/logo-256.png" "$DEB_ROOT/usr/share/icons/hicolor/256x256/apps/mcpservermanager.png"
fi
if [ -f "$ICON_DIR/logo-512.png" ]; then
    cp "$ICON_DIR/logo-512.png" "$DEB_ROOT/usr/share/icons/hicolor/512x512/apps/mcpservermanager.png"
fi

# Desktop entry
cat > "$DEB_ROOT/usr/share/applications/mcpservermanager.desktop" << EOF
[Desktop Entry]
Name=McpServerManager
Comment=Browse and analyze Copilot request/session logs
Exec=/opt/mcpservermanager/$EXE_NAME
Icon=mcpservermanager
Terminal=false
Type=Application
Categories=Development;Utility;
StartupWMClass=McpServerManager.Desktop
EOF

# DEBIAN/control
INSTALLED_SIZE=$(du -sk "$DEB_ROOT/opt/mcpservermanager" | awk '{print $1}')
cat > "$DEB_ROOT/DEBIAN/control" << EOF
Package: $PKG_NAME
Version: $VERSION
Section: devel
Priority: optional
Architecture: amd64
Installed-Size: $INSTALLED_SIZE
Maintainer: sharpninja <ninja@thesharp.ninja>
Description: McpServerManager Desktop
 Avalonia desktop app for browsing, searching, and analyzing
 Copilot request/session logs. Supports portrait and landscape
 layouts with tree view, markdown/JSON viewer, and search history.
Homepage: https://github.com/sharpninja/McpServerManager
EOF

# DEBIAN/postinst
cat > "$DEB_ROOT/DEBIAN/postinst" << 'EOF'
#!/bin/sh
set -e
update-desktop-database /usr/share/applications 2>/dev/null || true
gtk-update-icon-cache /usr/share/icons/hicolor 2>/dev/null || true
EOF
chmod 755 "$DEB_ROOT/DEBIAN/postinst"

# DEBIAN/postrm
cat > "$DEB_ROOT/DEBIAN/postrm" << 'EOF'
#!/bin/sh
set -e
update-desktop-database /usr/share/applications 2>/dev/null || true
gtk-update-icon-cache /usr/share/icons/hicolor 2>/dev/null || true
EOF
chmod 755 "$DEB_ROOT/DEBIAN/postrm"

# Build deb
dpkg-deb --build --root-owner-group "$DEB_ROOT" "$DEB_FILE"
DEB_SIZE=$(du -sh "$DEB_FILE" | awk '{print $1}')
echo ""
echo "═══════════════════════════════════════════════════════"
echo "  DEB package ready: $DEB_FILE ($DEB_SIZE)"
echo "  Version: $VERSION"
echo "  Install: sudo dpkg -i $DEB_FILE"
echo "═══════════════════════════════════════════════════════"
