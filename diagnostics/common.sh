#!/usr/bin/env bash
set -euo pipefail
DIAGNOSTICS_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$DIAGNOSTICS_DIR/.." && pwd)"
RESULTS_DIR="${LINER_DIAGNOSTICS_RESULTS:-$DIAGNOSTICS_DIR/results}"
mkdir -p -- "$RESULTS_DIR"
die() { printf 'error: %s\n' "$*" >&2; exit 2; }
need() { command -v "$1" >/dev/null 2>&1 || die "Required command '$1' was not found in PATH."; }
timestamp() { date -u +%Y%m%dT%H%M%S%NZ; }
valid_pid() {
  [[ "${1:-}" =~ ^[1-9][0-9]*$ ]] || die "PID must be an explicitly supplied positive integer."
  kill -0 "$1" 2>/dev/null || die "Process $1 does not exist or is not accessible."
}
safe_name() { [[ "${1:-}" =~ ^[A-Za-z0-9._-]+$ ]] || die "Invalid name: use letters, numbers, dot, underscore, hyphen."; }
resolve_path() { if [[ "$1" = /* ]]; then printf '%s\n' "$1"; else printf '%s/%s\n' "$REPO_ROOT" "$1"; fi; }
reportgenerator() {
  if [[ -f "$REPO_ROOT/.config/dotnet-tools.json" ]] && dotnet tool list --local 2>/dev/null | rg -q 'dotnet-reportgenerator-globaltool'; then
    dotnet tool run reportgenerator "$@"
  elif command -v reportgenerator >/dev/null 2>&1; then
    reportgenerator "$@"
  else
    die "ReportGenerator unavailable. Run dotnet tool restore or install its global tool."
  fi
}
