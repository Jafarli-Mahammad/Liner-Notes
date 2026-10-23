#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd -- "$repo_root"
dotnet_executable="${LINER_DOTNET:-dotnet}"
if ! "$dotnet_executable" --version >/dev/null 2>&1; then
  dotnet_executable="${HOME}/.dotnet/dotnet"
fi
if ! "$dotnet_executable" --version >/dev/null 2>&1; then
  printf '%s\n' 'Install the SDK pinned in global.json, or set LINER_DOTNET to its dotnet executable.' >&2
  exit 2
fi
if [[ ! -f .linernotes/live/settings.json ]]; then
  "$dotnet_executable" run --file scripts/setup_local_live.cs --no-cache -- --initialize
fi
exec "$dotnet_executable" run --project src/Presentation --launch-profile 'Liner Notes'
