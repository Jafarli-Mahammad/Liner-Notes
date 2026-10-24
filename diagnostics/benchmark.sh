#!/usr/bin/env bash
set -euo pipefail
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/common.sh"
usage() {
  cat <<'HELP'
Usage: diagnostics/benchmark.sh [--project PATH] [--filter GLOB] [--list] [--] [BenchmarkDotNet args...]
Run an existing BenchmarkDotNet project in Release mode. Results go under diagnostics/results/.
If none exists, offers optional isolated-project setup without changing repository code.
HELP
}
project=
filter=
list=false
extra=()
while (($#)); do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --project) (($# >= 2)) || die "--project needs a path"; project="$(resolve_path "$2")"; shift 2 ;;
    --filter) (($# >= 2)) || die "--filter needs a pattern"; filter="$2"; shift 2 ;;
    --list) list=true; shift ;;
    --) shift; extra=("$@"); break ;;
    *) die "Unknown option: $1" ;;
  esac
done
need dotnet
if [[ -z "$project" ]]; then
  mapfile -t candidates < <(rg -l 'BenchmarkDotNet' "$REPO_ROOT" -g '*.csproj' -g '!**/obj/**' -g '!**/bin/**' || true)
  if (("${#candidates[@]}" == 0)); then
    cat >&2 <<'HELP'
No BenchmarkDotNet project exists in this repository. None was added automatically.
Optional isolated setup:
  dotnet new console -n LinerNotes.Benchmarks -o benchmarks/LinerNotes.Benchmarks
  dotnet add benchmarks/LinerNotes.Benchmarks package BenchmarkDotNet
  # Add a ProjectReference only to the production project under test and a
  # [MemoryDiagnoser] benchmark class, then run with --project <csproj>.
HELP
    exit 3
  fi
  (("${#candidates[@]}" == 1)) || die "Multiple benchmark projects found; select one with --project."
  project="${candidates[0]}"
fi
[[ -f "$project" ]] || die "Project not found: $project"
rg -q 'BenchmarkDotNet' "$project" || rg -q 'BenchmarkDotNet' "$(dirname "$project")" -g '*.cs' || die "Selected project does not reference BenchmarkDotNet."
name="$(basename "${project%.csproj}")-$(timestamp)"
out="$RESULTS_DIR/benchmarks/$name"
mkdir -p -- "$out"
args=(run --configuration Release --project "$project" -- --artifacts "$out")
if [[ "$list" == true ]]; then args+=(--list flat); fi
if [[ -n "$filter" ]]; then args+=(--filter "$filter"); fi
args+=("${extra[@]}")
printf 'Benchmark project: %s\nArtifacts: %s\n' "$project" "$out"
dotnet "${args[@]}"
