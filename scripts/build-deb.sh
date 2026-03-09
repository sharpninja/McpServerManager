#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
WORKSPACE_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

VERSION=""
CONFIGURATION="Release"
RID="linux-x64"
NO_BUILD=false
OUTPUT_DIR=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) VERSION="$2"; shift 2 ;;
        --configuration) CONFIGURATION="$2"; shift 2 ;;
        --rid) RID="$2"; shift 2 ;;
        --no-build) NO_BUILD=true; shift ;;
        --output-dir) OUTPUT_DIR="$2"; shift 2 ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

ARGS=(--target BuildDesktopDeb --configuration "$CONFIGURATION" --rid "$RID")
if [[ -n "$VERSION" ]]; then
    ARGS+=(--package-version "$VERSION")
fi
if [[ "$NO_BUILD" == true ]]; then
    ARGS+=(--no-build)
fi
if [[ -n "$OUTPUT_DIR" ]]; then
    ARGS+=(--output-dir "$OUTPUT_DIR")
fi

exec bash "$WORKSPACE_ROOT/build.sh" "${ARGS[@]}"
