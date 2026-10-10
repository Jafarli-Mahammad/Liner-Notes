#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${LINER_MANUAL_QA_CONNECTION_STRING:-}" ]]; then
  printf '%s\n' "Set LINER_MANUAL_QA_CONNECTION_STRING to a loopback PostgreSQL database named liner_notes_manual_qa." >&2
  exit 2
fi

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet run --file "$repo_root/scripts/validate_manual_qa_connection.cs" --no-cache
phase7_qa_root="$(mktemp -d "${TMPDIR:-/tmp}/linernotes-phase7-qa.XXXXXX")"
phase7_qa_pid=""

cleanup() {
  if [[ -n "$phase7_qa_pid" ]] && kill -0 "$phase7_qa_pid" 2>/dev/null; then
    kill "$phase7_qa_pid" 2>/dev/null || true
    wait "$phase7_qa_pid" 2>/dev/null || true
  fi
  rm -rf -- "$phase7_qa_root"
}
trap cleanup EXIT HUP INT TERM

for category in recordings cache reports copies backups partials; do
  mkdir -p -- "$phase7_qa_root/$category"
done
if [[ "$(uname -s)" != MINGW* && "$(uname -s)" != CYGWIN* && "$(uname -s)" != MSYS* ]]; then
  chmod 700 -- "$phase7_qa_root" "$phase7_qa_root/recordings" "$phase7_qa_root/cache" \
    "$phase7_qa_root/reports" "$phase7_qa_root/copies" "$phase7_qa_root/backups" "$phase7_qa_root/partials"
fi

export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export ConnectionStrings__DefaultConnection="$LINER_MANUAL_QA_CONNECTION_STRING"
export Generation__Origin=Synthetic
export LocalEmail__Enabled=true
export LocalEmail__ApplicationOrigin=http://127.0.0.1:5087
export LocalEmail__SinkRoot="$phase7_qa_root/copies"
export ASPNETCORE_URLS=http://127.0.0.1:5087
export GenerationStorage__InventoryPath="$phase7_qa_root/inventory.json"
export GenerationStorage__LeasePath="$phase7_qa_root/lease.lock"
export GenerationStorage__DatabaseOverheadBytesPerPick=1000000
export GenerationStorage__ArtifactRoots__recordings__0="$phase7_qa_root/recordings"
export GenerationStorage__ArtifactRoots__cache__0="$phase7_qa_root/cache"
export GenerationStorage__ArtifactRoots__reports__0="$phase7_qa_root/reports"
export GenerationStorage__ArtifactRoots__copies__0="$phase7_qa_root/copies"
export GenerationStorage__ArtifactRoots__backups__0="$phase7_qa_root/backups"
export GenerationStorage__ArtifactRoots__partials__0="$phase7_qa_root/partials"

cd -- "$repo_root"
dotnet ef database update --project src/DataAccess/DataAccess.csproj --startup-project src/Presentation/Presentation.csproj --context AppDbContext

printf '%s\n' "Manual QA is starting in Development with synthetic provider fixtures. Stop with Ctrl+C."
dotnet run --project src/Presentation/Presentation.csproj --no-launch-profile &
phase7_qa_pid=$!
printf '%s\n' "Open http://127.0.0.1:5087/manual-test, register a disposable account and save fixture seeds."
printf '%s\n' "Use darkwave, synthwave and soundtrack for a nonempty fixture list. Inspect the subscriber ID."
printf '%s\n' "Keep this launcher running during email/unsubscribe checks. MIME files: $phase7_qa_root/copies"
read -r -p 'Subscriber GUID from profile: ' phase7_qa_user
read -r -p 'ISO week (for example 2026-W41): ' phase7_qa_week
while true; do
  read -r -p 'Action (reconcile, generate, capture, cleanup, quit): ' phase7_qa_action
  case "$phase7_qa_action" in
    quit) break ;;
    cleanup)
      if dotnet run --file scripts/phase7_cleanup_local_email.cs --no-cache -- --user "$phase7_qa_user"; then
        printf '%s\n' 'Owned-copy cleanup completed after account deletion.'
      else
        printf '%s\n' 'Cleanup stopped or incomplete; inspect the reason above.'
      fi
      ;;
    reconcile|generate|capture)
      if dotnet run --project src/Worker/Worker.csproj -- --action "$phase7_qa_action" --user "$phase7_qa_user" --week "$phase7_qa_week" --origin Synthetic; then
        printf '%s\n' 'Worker action completed; inspect its status above.'
      else
        printf '%s\n' 'Worker stopped; inspect its bounded reason above.'
      fi
      ;;
    *) printf '%s\n' 'Choose reconcile, generate, capture, cleanup or quit.' ;;
  esac
done
