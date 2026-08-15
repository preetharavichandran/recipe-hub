#!/usr/bin/env bash
set -euo pipefail
SUB="${1:-user-a}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
dotnet run --project "$ROOT/tools/DevToken" -- "$SUB"
