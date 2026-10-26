#!/usr/bin/env bash
set -euo pipefail
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/common.sh"
usage() {
  cat <<'HELP'
Usage: diagnostics/coverage.sh [--project PATH] [--configuration CONFIG] [--no-build] [--timeout SECONDS]
Run tests with Coverlet; produce Cobertura XML, HTML, and a concise summary.
Paths are relative to repository root. Results are timestamped under diagnostics/results/.
Options: --project PATH (repeatable), --configuration CONFIG (default Release),
         --timeout SECONDS (default 300 per project), --no-build, -h|--help
HELP
}
projects=()
configuration=Release
no_build=false
timeout_s=300
while (($#)); do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --project) (($# >= 2)) || die "--project needs a path"; projects+=("$(resolve_path "$2")"); shift 2 ;;
    --configuration|-c) (($# >= 2)) || die "$1 needs a value"; configuration="$2"; shift 2 ;;
    --timeout) (($# >= 2)) || die "--timeout needs a value"; timeout_s="$2"; shift 2 ;;
    --no-build) no_build=true; shift ;;
    *) die "Unknown option: $1" ;;
  esac
done
[[ "$timeout_s" =~ ^[1-9][0-9]*$ ]] || die "Timeout must be a positive whole number of seconds."
need dotnet
if (("${#projects[@]}" == 0)); then
  while IFS= read -r -d '' path; do projects+=("$path"); done < <(find "$REPO_ROOT/tests" -type f -name '*.csproj' -print0 2>/dev/null | sort -z)
fi
(("${#projects[@]}" > 0)) || die "No test projects found under tests/."
for project in "${projects[@]}"; do [[ -f "$project" ]] || die "Project not found: $project"; done
run_dir="$RESULTS_DIR/coverage-$(timestamp)"
mkdir -p -- "$run_dir"
for project in "${projects[@]}"; do
  name="$(basename "${project%.csproj}")"
  output="$run_dir/$name/coverage.cobertura.xml"
  mkdir -p -- "$(dirname "$output")"
  args=(test "$project" --configuration "$configuration" --logger "console;verbosity=minimal"
    "-p:CollectCoverage=true" "-p:CoverletOutputFormat=cobertura" "-p:CoverletOutput=$output"
    "-p:UseSourceLink=false")
  if [[ "$no_build" == true ]]; then args+=(--no-build); fi
  printf '\n== %s ==\n' "$name"
  status=0
  timeout --signal=INT --kill-after=10 "$timeout_s" dotnet "${args[@]}" || status=$?
  if ((status == 124 || status == 137)); then die "Test project $name exceeded the ${timeout_s}s timeout."; fi
  if ((status != 0)); then exit "$status"; fi
done
shopt -s nullglob
reports=("$run_dir"/*/coverage.cobertura.xml)
(("${#reports[@]}" > 0)) || die "Coverlet produced no Cobertura reports."
report_list="$(IFS=';'; printf '%s' "${reports[*]}")"
report_args=("-reports:$report_list")
reportgenerator "${report_args[@]}" "-targetdir:$run_dir/report" "-reporttypes:Html;Cobertura;TextSummary"
printf '\nCoverage summary\n'
if [[ -f "$run_dir/report/Summary.txt" ]]; then
  cat "$run_dir/report/Summary.txt"
else
  printf 'Summary: %s\n' "$run_dir/report/index.html"
fi
printf 'Cobertura XML: %s\nHTML report: %s\n' "$run_dir/report/Cobertura.xml" "$run_dir/report/index.html"
