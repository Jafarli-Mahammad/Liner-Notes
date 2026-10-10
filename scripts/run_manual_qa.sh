#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${LINER_MANUAL_QA_CONNECTION_STRING:-}" ]]; then
  printf '%s\n' "Set LINER_MANUAL_QA_CONNECTION_STRING to a loopback PostgreSQL database named liner_notes_manual_qa." >&2
  exit 2
fi

shopt -s nocasematch
if [[ ! "$LINER_MANUAL_QA_CONNECTION_STRING" =~ (^|\;)[[:space:]]*Host=(localhost|127\.0\.0\.1)(\;|$) ]] ||
   [[ ! "$LINER_MANUAL_QA_CONNECTION_STRING" =~ (^|\;)[[:space:]]*Database=liner_notes_manual_qa(\;|$) ]]; then
  printf '%s\n' "The manual QA launcher requires Host=localhost or 127.0.0.1 and Database=liner_notes_manual_qa." >&2
  exit 2
fi
shopt -u nocasematch

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
manual_qa_root="$(mktemp -d "${TMPDIR:-/tmp}/linernotes-manual-qa.XXXXXX")"
manual_qa_pid=""

cleanup() {
  if [[ -n "$manual_qa_pid" ]] && kill -0 "$manual_qa_pid" 2>/dev/null; then
    kill "$manual_qa_pid" 2>/dev/null || true
    wait "$manual_qa_pid" 2>/dev/null || true
  fi
  rm -rf -- "$manual_qa_root"
}
trap cleanup EXIT HUP INT TERM

for category in recordings cache reports copies backups partials; do
  mkdir -p -- "$manual_qa_root/$category"
done

export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export ConnectionStrings__DefaultConnection="$LINER_MANUAL_QA_CONNECTION_STRING"
export Generation__Origin=Synthetic
export GenerationStorage__InventoryPath="$manual_qa_root/inventory.json"
export GenerationStorage__LeasePath="$manual_qa_root/lease.lock"
export GenerationStorage__DatabaseOverheadBytesPerPick=1000000
export GenerationStorage__ArtifactRoots__recordings__0="$manual_qa_root/recordings"
export GenerationStorage__ArtifactRoots__cache__0="$manual_qa_root/cache"
export GenerationStorage__ArtifactRoots__reports__0="$manual_qa_root/reports"
export GenerationStorage__ArtifactRoots__copies__0="$manual_qa_root/copies"
export GenerationStorage__ArtifactRoots__backups__0="$manual_qa_root/backups"
export GenerationStorage__ArtifactRoots__partials__0="$manual_qa_root/partials"

cd -- "$repo_root"
dotnet ef database update --project src/DataAccess/DataAccess.csproj --startup-project src/Presentation/Presentation.csproj --context AppDbContext

printf '%s\n' "Manual QA is starting in Development with synthetic provider fixtures. Stop with Ctrl+C."
dotnet run --project src/Presentation/Presentation.csproj --no-launch-profile &
manual_qa_pid=$!
wait "$manual_qa_pid"
