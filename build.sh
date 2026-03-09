#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ $# -eq 0 ]]; then
    set -- --help
fi

has_explicit_target=false
for argument in "$@"; do
    case "$argument" in
        --target|-target|/target|-t|/t)
            has_explicit_target=true
            break
            ;;
    esac
done

if [[ "$has_explicit_target" == false && $# -gt 0 && "$1" != -* && "$1" != /* ]]; then
    set -- --target "$1" "${@:2}"
fi

dotnet run --project "$SCRIPT_DIR/build/Build.csproj" -- --root "$SCRIPT_DIR" "$@"
