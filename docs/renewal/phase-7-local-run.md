# Phase 7 local QA procedure

The approved local slice is implemented and locally verified; developer confirmation is
pending. This procedure uses fabricated accounts, Synthetic provider fixtures, a disposable
PostgreSQL database and private temporary encrypted email files. See [evidence](phase-7-evidence.md)
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
deletion or disposal of your QA database. On Unix, the configured sink root must be
owner-only (mode `0700` or stricter); the launcher sets the temporary root and categories
to mode `0700`. For a persistent sink, create a dedicated owner-only directory before
configuring `LocalEmail:SinkRoot`. New encrypted files use mode `0600`; the application rejects
a Unix sink directory with group/world access. On Windows, the sink directory must be owned
by the current process user; its allow rules may grant access only to that user, Local System
and built-in Administrators. Review/delete disposable accounts before quitting;
temporary message removal on exit is QA housekeeping, not an adopted product retention rule.
Clear the environment value afterward with `unset LINER_MANUAL_QA_CONNECTION_STRING`.

## Generate, capture and inspect

Enter these actions in order:

1. `reconcile` — measure the database and all six configured artifact categories.
2. `generate` — invoke the existing generator for the explicit user/week.
3. `reconcile` — generation changed database accounting; refresh explicitly.
4. `capture` — load the persisted digest and publish a bounded encrypted local email file.
5. `capture` again — expect `already_captured`, the same digest ID and unchanged file bytes.

Inspect the digest and export on `/manual-test`. A captured digest has status
`LocalCaptured`, with `SentAt` null. Export version is now **2.2**; the `account`
section exposes stored Identity profile fields while excluding credentials and
authentication internals. The subscriber includes
`emailUnsubscribedAtUtc`, and archive inspection exposes sanitized plaintext/HTML and issues.
The stored score explanation appears in both alternatives, in persisted rank order.
Listening links are ordinary links; opening the message loads no remote images, fonts,
scripts, CSS resources, players or tracking pixels.

Stored files are now `<digest-id-without-dashes>.eml.enc`. The entire MIME message,
including recipient, taste data and unsubscribe capability, is authenticated ciphertext;
partial writes also contain ciphertext. A MIME viewer cannot open these files directly.
The original MIME bound is 400,000 bytes, with up to 16,384 additional stored bytes reserved
for encryption metadata. Storage accounting uses the actual encrypted byte count.

Use `inspect` in the launcher for the selected user's week. It decrypts in memory and
prints sanitized text to your terminal without writing a plaintext preview file. The
helper requires Development, a loopback database, the existing subscriber/digest and
operator access to the configured storage and database. Both sanitized alternatives
remain available through the authenticated data export on `/manual-test`. The export
removes the unsubscribe capability; the separate explicit `unsubscribe-link` action
reveals the stored capability only for local testing below. Do not save or redirect private previews,
paste capabilities into chat or commit them.

For a persistent sink containing older `.eml` copies, configure the same database and
artifact roots, run Worker `reconcile`, then run the explicit conversion for each user/week:

```bash
dotnet run --file scripts/phase7_local_email.cs --no-cache -- \
  --action convert --user "$subscriber_id" --week "$iso_week"
```

Conversion validates the receipt's ownership, reserves ciphertext headroom, writes and
verifies a private encrypted partial, publishes without overwrite, and removes the
authenticated original. Retry resumes a matching published destination. Complete matching
legacy partials are removed; unverifiable partials stop with an explicit cleanup reason.
Truncated/corrupt files and previously created `.preview.html`/`.preview.txt` copies cannot
be assigned to an account safely: explicitly remove your own disposable QA leftovers or
review their provenance locally. Do not claim conversion complete while these remain.
Reconcile after conversion. Capture stops for a legacy final copy; export reports it
incomplete instead of silently returning a plaintext-file fallback. Account deletion can
still remove authenticated legacy copies.

File encryption uses a separate Data Protection purpose, with persistent keys in the
existing `DataProtectionKeys` database table, outside source control and the email sink.
Retain those keys across process restarts and restore; deleting them makes encrypted
copies unreadable and prevents authenticated cleanup. Automatic key rotation retains
older decrypting keys. The database key store is not additionally wrapped by this change:
restrict database access and protect its backups separately from copies. Theft of copies
alone is covered; possession of both keys and copies, or compromise of the running service
or host administrator, is outside this protection. Scheduling, external delivery and
production key custody remain separately gated.

For an empty-list case use a separate disposable account with three tags unsupported by
the fixtures (for example `rock`, `electronic`, `ambient`). A persisted empty digest still
captures both alternatives and says no eligible discoveries were found. Lists of 1 and 3
are covered by renderer checks; the real process demonstration covered 0 and 5.

## Confirm local unsubscribe

Keep Presentation running. Choose `unsubscribe-link` in the launcher, then open the
printed loopback link locally. This explicit operator action decrypts only the selected
owned file to retrieve its original capability; it requires an existing subscriber and
creates no plaintext file. Treat the terminal URL as private and do not paste it into
chat, reports or source. GET displays
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
