#!/usr/bin/env bash
set -euo pipefail
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/common.sh"
usage() { printf 'Usage: diagnostics/health.sh [--help]\nCheck SDK, Linux environment, and diagnostics CLI availability.\n'; }
if [[ "${1:-}" == -h || "${1:-}" == --help ]]; then usage; exit 0; fi
(("$#" == 0)) || die "Unknown option: $1"
failures=0
tool_versions="$(dotnet tool list --global 2>/dev/null || true)"
local_versions="$(dotnet tool list --local 2>/dev/null || true)"
check() {
  local command_name="$1"
  local package="$2"
  local version
  if ! command -v "$command_name" >/dev/null 2>&1; then
    printf 'MISSING %-24s install the corresponding .NET global tool\n' "$command_name"
    failures=$((failures + 1))
    return
  fi
  version="$(printf '%s\n%s\n' "$tool_versions" "$local_versions" | awk -v package="$package" '$1 == package { print $2; exit }')"
  if [[ -n "$version" ]]; then
    printf 'OK      %-24s %s\n' "$command_name" "$version"
  else
    printf 'OK      %-24s installed (version not listed)\n' "$command_name"
  fi
}
printf 'Repository: %s\nResults:    %s\nOS:         %s\n' "$REPO_ROOT" "$RESULTS_DIR" "$(uname -sr)"
if command -v dotnet >/dev/null 2>&1; then
  sdk_version="$(dotnet --version)"
  printf 'SDK:        %s\n' "$sdk_version"
  sdk_major="${sdk_version%%.*}"
  if ((sdk_major < 10)); then printf 'WARN SDK %s; repository targets net10.0\n' "$sdk_version"; failures=$((failures + 1)); fi
else
  printf 'MISSING dotnet SDK\n'
  failures=$((failures + 1))
fi
check dotnet-trace dotnet-trace
check dotnet-counters dotnet-counters
check dotnet-dump dotnet-dump
check dotnet-gcdump dotnet-gcdump
if [[ -f "$REPO_ROOT/.config/dotnet-tools.json" ]] && (cd "$REPO_ROOT" && dotnet tool list --local 2>/dev/null | rg -q 'dotnet-reportgenerator-globaltool'); then
  printf 'OK reportgenerator (repository local tool)\n'
elif command -v reportgenerator >/dev/null 2>&1; then
  printf 'OK reportgenerator (%s)\n' "$(command -v reportgenerator)"
else
  printf 'MISSING reportgenerator (dotnet-reportgenerator-globaltool)\n'
  failures=$((failures + 1))
fi
if [[ "$(uname -s)" == Linux ]]; then printf 'INFO    Linux attach may need same-user access; dumps may be restricted by ptrace policy.\n'; fi
if command -v shellcheck >/dev/null 2>&1; then
  printf 'OK      shellcheck              %s\n' "$(shellcheck --version | sed -n '2p')"
else
  printf 'INFO    shellcheck              not installed (optional)\n'
fi
if ((failures > 0)); then exit 1; fi
printf 'Health check passed.\n'
