# Phase 7 local QA procedure

The approved local slice is implemented and locally verified; developer confirmation is
pending. This procedure uses fabricated accounts, Synthetic provider fixtures, a disposable
PostgreSQL database and private temporary MIME files. See [evidence](phase-7-evidence.md)
for results and limitations.

## Start the local application and Worker menu

Prerequisites: .NET 10 SDK, restored repository packages, the existing EF CLI tool, and a
local PostgreSQL 16+ database named `liner_notes_manual_qa`. Create that disposable database
with your PostgreSQL administration tool. Use a connection targeting `localhost` or
`127.0.0.1`; enter it without echoing or storing it in source:

```bash
read -r -s -p 'Local QA PostgreSQL connection string: ' LINER_MANUAL_QA_CONNECTION_STRING
printf '\n'
export LINER_MANUAL_QA_CONNECTION_STRING
bash scripts/run_phase7_local_qa.sh
```

The launcher applies tracked migrations with `--context AppDbContext`, explicitly enables
local email, selects Synthetic generation and starts Presentation at
`http://127.0.0.1:5087/manual-test`. It prints its private temporary `copies` directory.
Wait for the host to start before using the page. Register a disposable account and inspect
the profile for its subscriber GUID. Save three fixture tags: `darkwave`, `synthwave`,
`soundtrack`. Enter that GUID and an ISO week, such as `2026-W42`, in the launcher prompts.

The launcher does not create or drop a database. On `quit` or Ctrl+C it stops its server and
removes only its temporary artifact directory. SQL account data remains until account
deletion or disposal of your QA database. Review/delete disposable accounts before quitting;
temporary message removal on exit is QA housekeeping, not an adopted product retention rule.
Clear the environment value afterward with `unset LINER_MANUAL_QA_CONNECTION_STRING`.

## Generate, capture and inspect

Enter these actions in order:

1. `reconcile` — measure the database and all six configured artifact categories.
2. `generate` — invoke the existing generator for the explicit user/week.
3. `reconcile` — generation changed database accounting; refresh explicitly.
4. `capture` — load the persisted digest and publish a bounded local MIME file.
5. `capture` again — expect `already_captured`, the same digest ID and unchanged file bytes.

Inspect the digest and export on `/manual-test`. A captured digest has status
`LocalCaptured`, with `SentAt` null. Export version is **2.1**; the subscriber includes
`emailUnsubscribedAtUtc`, and archive inspection exposes sanitized plaintext/HTML and issues.
The stored score explanation appears in both alternatives, in persisted rank order.
Listening links are ordinary links; opening the message loads no remote images, fonts,
scripts, CSS resources, players or tracking pixels.

Open `<digest-id-without-dashes>.eml` in a local MIME viewer to inspect the private message.
It contains recipient/taste data and a protected unsubscribe capability. Keep it local;
public export removes capabilities. On Unix newly created files have mode `0600`.

To inspect either MIME alternative without printing its capability, decode it to a private
local HTML/text file and open it locally. Export `PHASE7_QA_MESSAGE` with the one message's
absolute path (`export PHASE7_QA_MESSAGE='/absolute/private/path/digest.eml'`), then run:

```bash
python3 - <<'PY'
import os
from email import policy
from email.parser import BytesParser
from pathlib import Path

message = Path(os.environ['PHASE7_QA_MESSAGE'])
with message.open('rb') as source:
    mime = BytesParser(policy=policy.default).parse(source)
for part in mime.iter_parts():
    extension = 'html' if part.get_content_type() == 'text/html' else 'txt'
    target = message.with_suffix('.preview.' + extension)
    descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, 'w', encoding='utf-8') as output:
        output.write(part.get_content())
PY
```

Decoded previews are additional private copies. The configured `copies` category counts
them, so reconcile after creating them. The archive cannot authenticate their ownership
as MIME receipts: remove your own previews explicitly before checking account cleanup.
The launcher removes them on exit because they live inside its disposable artifact root.
Do not upload messages/previews, paste capabilities into chat or commit them.

For an empty-list case use a separate disposable account with three tags unsupported by
the fixtures (for example `rock`, `electronic`, `ambient`). A persisted empty digest still
captures both alternatives and says no eligible discoveries were found. Lists of 1 and 3
are covered by renderer checks; the real process demonstration covered 0 and 5.

## Confirm local unsubscribe

Keep Presentation running. Open the private message's unsubscribe link. GET displays
**Confirm unsubscribe** and makes no account change. Submit the confirmation form; POST
sets the first UTC opt-out timestamp. Reload the profile/export and verify the field.
Repeated confirmation preserves the timestamp and does not reveal account details.

For a fresh week, generation can still persist a digest, but `capture` stops with
`email_unsubscribed`. Account/profile/feedback access remains available. This is an
explicitly enabled Development flow with a literal loopback application origin; Production
and disabled routes return 404. It is not external provider one-click unsubscribe support.

## Delete the disposable account and retry cleanup

Use the page's account-deletion confirmation. The account lease coordinates capture and
deletion; SQL/Identity deletion commits before filesystem cleanup. HTTP 204 means configured
owned-copy cleanup completed. HTTP 200 with `accountDeleted: true` and
`localCopiesDeleted: false` reports incomplete cleanup; the UI signs out and displays that
result. Protected requests are denied after deletion.

The archive removes only authenticated copies for that account in the configured sink.
Another account's copies remain. Corrupt/unknown partials and unconfigured archives produce
issues rather than an erasure claim. Shared catalog, copied files outside the configured
sink and backups are outside this local cleanup boundary.

While the launcher still has its configuration, enter `cleanup` to retry after SQL deletion.
It refuses with `account_still_exists` while either the domain or Identity account exists.
For a separately configured operator session, the equivalent command is:

```bash
dotnet run --file scripts/phase7_cleanup_local_email.cs --no-cache -- --user YOUR_DELETED_ACCOUNT_GUID
```

Use `--file` explicitly: this repository also has a legacy root project. `--no-cache` avoids
stale file-based assemblies; the helper disables AOT for its EF composition. Its environment
must have `DOTNET_ENVIRONMENT=Development`, a loopback connection and the same enabled
`LocalEmail`/storage settings. Never remove unknown artifacts by guessing account ownership.
An operator may remove their own failed QA artifacts, then reconcile before another write.

## Direct Worker and recorded-input boundaries

The Worker has exactly three one-shot actions. In an already configured local environment:

```bash
dotnet run --project src/Worker/Worker.csproj -- --action reconcile --user YOUR_ACCOUNT_GUID --week 2026-W42 --origin Synthetic
dotnet run --project src/Worker/Worker.csproj -- --action generate --user YOUR_ACCOUNT_GUID --week 2026-W42 --origin Synthetic
dotnet run --project src/Worker/Worker.csproj -- --action reconcile --user YOUR_ACCOUNT_GUID --week 2026-W42 --origin Synthetic
dotnet run --project src/Worker/Worker.csproj -- --action capture --user YOUR_ACCOUNT_GUID --week 2026-W42 --origin Synthetic
```

Missing arguments stop with exit 2; cancellation returns 130. Invalid configuration, storage
or receipt integrity stops with bounded reasons. A captured digest whose receipt is missing
or conflicts is not silently recreated. For process restart, retain the same database,
protection keys, sink and template configuration. The temporary launcher intentionally
removes its sink on exit; use a separate explicit ignored sink to study retained restarts.

Required configuration groups are `ConnectionStrings:DefaultConnection`, `Generation`,
`GenerationStorage` and `LocalEmail` (`Enabled`, literal loopback `ApplicationOrigin`,
absolute `SinkRoot`). The sink must be counted once under `copies`; all six storage
categories must be declared, with nonoverlapping, nonsymlinked roots. Defaults bound each
UTF-8 alternative to 100,000 bytes and total MIME to 400,000; overflow stops without truncation.

Recorded mode is available only with **separate exact permission for local generation**.
Do not reuse acquisition/evaluation permission as approval for a subscriber. Configure
`LocalRecordedInput:ManifestPath`, `InputRoot` (absolute paths) and an independently
approved `ApprovedManifestSha256`; then select `--origin Recorded`. The exact local-use
manifest has these case-sensitive fields:

| Field | Required value/boundary |
|---|---|
| `Version`, `Use`, `Origin` | `1`, `local-digest-generation`, `Recorded` |
| `UserId`, `Week` | The exact authorized account and canonical ISO week |
| `ExpiresAtUtc`, `DeveloperReference` | Current permission, bounded approval reference |
| `SourceManifestPath`, `SourceManifestSha256` | Contained acquisition-manifest path and exact byte hash |
| `Responses` | 1–1,000 explicitly named immutable responses |
| Each response | `Method`, `Identity`, `Path`, `Sha256`, `RetrievedAtUtc`, `Limit`, `Origin` |

Source approval must be an unexpired approved Phase 4/5 Last.fm acquisition manifest with
its canonical approval digest intact; local use cannot extend its retention. Response paths
stay under `InputRoot`, without symlinks; hashes/origins must match. The loader rejects
held-out/evaluation-only, mixed, credential-bearing, expired, changed and oversized inputs.
Reads are bounded to 1 MB per manifest, 100 KB per response and 20 MB total responses.
No suitable approved input stops the action. It invokes neither recorder nor HTTP and
performs no fixture substitution. A previously persisted digest can be inspected/recovered
without reacquisition. No real recording was used in this demonstration.

## Limits and next decision

The **1,000,000,000-byte stop is temporary and unresolved**; actual hosting capacity is
decided at build-order step 9. Reservations cover three possible MIME-sized partials;
failed partials stay counted. New writes stop on stale or changed inventory, unknown
accounting, or the threshold. Explicit reconciliation is required; there is no automatic
retention or refresh. Existing inspection, opt-out, deletion and receipt recovery remain
available at the threshold.

Local file publication and SQL commit do not share a transaction. Tests establish recovery
for process/fault/cancellation windows; power-loss durability and distributed writers are
not verified. Recommendation quality, full privacy/backup audit, cost/capacity, Last.fm
text-only email attribution/territorial questions and public-page approval remain open.
Before any later phase, the developer must explicitly choose **proceed, narrow or revise**.
Phase 8's privacy and delivery approvals are separate; Phase 9 remains unapproved.
