#!/usr/bin/env bash
set -euo pipefail
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/common.sh"
usage() {
  cat <<'HELP'
Usage:
  diagnostics/profile.sh --pid PID [--duration SECONDS] [--name NAME]
  diagnostics/profile.sh --launch --duration SECONDS [--name NAME] -- COMMAND [ARG...]
Capture sampled managed thread stacks and write .nettrace plus Speedscope JSON.
Samples include waits; combine them with runtime CPU counters and stage timings.
Attach mode requires an explicit PID. Duration defaults to 30 seconds (maximum 3600).
HELP
}
pid=
launch=false
duration=30
name="cpu-$(timestamp)"
command_args=()
while (($#)); do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --pid) (($# >= 2)) || die "--pid needs a value"; pid="$2"; shift 2 ;;
    --launch) launch=true; shift ;;
    --duration) (($# >= 2)) || die "--duration needs a value"; duration="$2"; shift 2 ;;
    --name) (($# >= 2)) || die "--name needs a value"; name="$2"; shift 2 ;;
    --) shift; command_args=("$@"); break ;;
    *) die "Unknown option: $1" ;;
  esac
done
[[ "$duration" =~ ^[1-9][0-9]*$ ]] || die "Duration must be positive whole seconds."
((duration <= 3600)) || die "Duration is capped at 3600 seconds."
safe_name "$name"
need dotnet-trace
if [[ "$launch" == true ]]; then
  [[ -z "$pid" ]] || die "Choose either --pid or --launch."
  (("${#command_args[@]}" > 0)) || die "--launch requires a command after --."
else
  [[ -n "$pid" ]] || die "Specify --pid PID or --launch COMMAND."
  (("${#command_args[@]}" == 0)) || die "A command after -- requires --launch."
  valid_pid "$pid"
fi
dir="$RESULTS_DIR/profile-$name"
[[ ! -e "$dir" ]] || die "Output already exists: $dir"
mkdir -p -- "$dir"
trace="$dir/$name.nettrace"
duration_arg="$(printf '%02d:%02d:%02d:%02d' "$((duration / 86400))" "$((duration / 3600 % 24))" "$((duration / 60 % 60))" "$((duration % 60))")"
# .NET diagnostics 10 separates native CPU sampling (collect-linux) from
# launch-compatible EventPipe thread sampling. Older tools call the latter cpu-sampling.
profile=cpu-sampling
if dotnet-trace list-profiles | grep -q 'dotnet-sampled-thread-time'; then
  profile=dotnet-sampled-thread-time
fi
args=(collect --duration "$duration_arg" --profile "$profile" --format NetTrace --output "$trace")
if [[ "$launch" == true ]]; then
  dotnet-trace "${args[@]}" --show-child-io -- "${command_args[@]}"
else
  dotnet-trace "${args[@]}" --process-id "$pid"
fi
[[ -s "$trace" ]] || die "No trace was written; target may have exited or collection failed."
dotnet-trace convert --format Speedscope --output "$dir/$name" "$trace"
[[ -s "$dir/$name.speedscope.json" ]] || die "No Speedscope file was written."
printf '\nOriginal trace: %s\nSpeedscope: %s\n' "$trace" "$dir/$name.speedscope.json"
printf 'Hotspot summary: dotnet-trace report %q topN -n 20\n' "$trace"
