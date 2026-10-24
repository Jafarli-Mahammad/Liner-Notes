#!/usr/bin/env bash
set -euo pipefail
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/common.sh"
usage() {
  cat <<'HELP'
Usage:
  diagnostics/memory.sh monitor --pid PID [--duration SECONDS] [--interval SECONDS]
  diagnostics/memory.sh gcdump --pid PID [--name NAME] [--timeout SECONDS]
  diagnostics/memory.sh dump --pid PID [--name NAME] --yes
  diagnostics/memory.sh analyze DUMP [--command SOS_COMMAND]
Full dumps may pause the target, use substantial disk, and contain sensitive data.
HELP
}
(("$#" > 0)) || { usage >&2; exit 2; }
mode="$1"
shift
if [[ "$mode" == -h || "$mode" == --help ]]; then usage; exit 0; fi
pid=
name="memory-$(timestamp)"
duration=60
interval=1
timeout_s=60
confirm=false
file=
command_text=
while (($#)); do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --pid) (($# >= 2)) || die "--pid needs a value"; pid="$2"; shift 2 ;;
    --name) (($# >= 2)) || die "--name needs a value"; name="$2"; shift 2 ;;
    --duration) (($# >= 2)) || die "--duration needs a value"; duration="$2"; shift 2 ;;
    --interval) (($# >= 2)) || die "--interval needs a value"; interval="$2"; shift 2 ;;
    --timeout) (($# >= 2)) || die "--timeout needs a value"; timeout_s="$2"; shift 2 ;;
    --yes) confirm=true; shift ;;
    --command) (($# >= 2)) || die "--command needs a value"; command_text="$2"; shift 2 ;;
    *) [[ "$mode" == analyze && -z "$file" ]] || die "Unknown option or extra argument: $1"; file="$1"; shift ;;
  esac
done
safe_name "$name"
case "$mode" in
  monitor)
    valid_pid "$pid"
    [[ "$duration" =~ ^[1-9][0-9]*$ && "$interval" =~ ^[1-9][0-9]*$ ]] || die "Duration and interval must be positive integers."
    ((duration <= 3600)) || die "Monitor duration is capped at 3600 seconds."
    need dotnet-counters
    out="$RESULTS_DIR/counters-$name.txt"
    [[ ! -e "$out" ]] || die "Output already exists: $out"
    status=0
    timeout --signal=INT --kill-after=5 "$duration" dotnet-counters monitor --process-id "$pid" --refresh-interval "$interval" --counters System.Runtime >"$out" 2>&1 || status=$?
    ((status == 0 || status == 124)) || { cat "$out" >&2; exit "$status"; }
    cat "$out"
    printf '\nSaved: %s\n' "$out"
    ;;
  gcdump)
    valid_pid "$pid"
    [[ "$timeout_s" =~ ^[1-9][0-9]*$ ]] || die "Timeout must be positive whole seconds."
    need dotnet-gcdump
    out="$RESULTS_DIR/heap-$name.gcdump"
    [[ ! -e "$out" ]] || die "Output already exists: $out"
    printf 'Warning: gcdump forces a full Gen 2 GC and may pause the target.\n' >&2
    timeout "$((timeout_s + 10))" dotnet-gcdump collect --process-id "$pid" --timeout "$timeout_s" --output "$out"
    dotnet-gcdump report "$out" >"${out%.gcdump}.txt"
    printf 'GC dump: %s\nHeap summary: %s\n' "$out" "${out%.gcdump}.txt"
    ;;
  dump)
    valid_pid "$pid"
    [[ "$confirm" == true ]] || die "Full dump needs explicit --yes after reviewing impact and sensitivity."
    need dotnet-dump
    out="$RESULTS_DIR/full-$name.dmp"
    [[ ! -e "$out" ]] || die "Output already exists: $out"
    printf 'WARNING: full dump may pause the process, consume disk, and contain credentials or user data.\n' >&2
    dotnet-dump collect --process-id "$pid" --type Full --output "$out"
    printf 'Dump: %s\nAnalyze: diagnostics/memory.sh analyze %q\n' "$out" "$out"
    ;;
  analyze)
    [[ -n "$file" ]] || die "Specify an existing dump file."
    path="$(resolve_path "$file")"
    [[ -f "$path" ]] || die "Dump file not found: $path"
    need dotnet-dump
    if [[ -n "$command_text" ]]; then
      dotnet-dump analyze "$path" --command "$command_text"
    else
      dotnet-dump analyze "$path"
    fi
    ;;
  *) usage >&2; die "Unknown memory command: $mode" ;;
esac
