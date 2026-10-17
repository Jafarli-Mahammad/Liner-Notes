# Discovery ownership and privacy inventory

C2 introduces additive profile, batch and job metadata. Guest HTTP access and job consumption are not enabled by this stage. Existing taste/recommendation rows retain their current ownership until C3.

## New records

| Table | Private fields and purpose | Export and deletion |
| --- | --- | --- |
| ListenerProfiles | Id; optional registered UserId; Revision; CreatedAtUtc, LastActiveAtUtc and ExpiresAtUtc. These identify a taste owner and enforce activity/expiry. Historical registered activity is initialized from existing audit timestamps, not claimed listening activity. | Account export `discovery.profile` includes every column. Registered-user deletion cascades to its profile; guest lifecycle is added before guest access is enabled. |
| DiscoveryBatches | Id, ProfileId, RequestId, Kind, InputRevision, CreatedAtUtc, CompletedAtUtc, CatalogVersion and EvidenceVersion. These describe an accepted recommendation request and the evidence versions used. | Account export `discovery.batches` includes every column. Owned batches cascade from the profile; other profiles and shared tracks remain. Historical scores are not duplicated into this metadata. |
| DiscoveryJobs | Id, ProfileId, RequestKey, RequestHash, Kind, State, InputRevision, PayloadJson, Attempts, LeaseExpiresAtUtc, NextAttemptAtUtc, CreatedAtUtc, UpdatedAtUtc, ProgressCode, ErrorCode, ResultBatchId and ResultJson. These support bounded resumable execution and idempotency. | Account export `discovery.jobs` covers these columns, including structured payload/result JSON. `LeaseToken` is an operational fencing credential and is explicitly excluded. Owned jobs cascade from the profile. |
| WeeklyDigests addition | Nullable BatchId links delivery identity to general batch identity. | Included in digest DTO/export. Existing subscriber/week identity and deletion behavior remain. |

The explicit schema/column contract is maintained in `Phase8PrivacyInventoryTests`; populated tests compare persisted row values against exported metadata. These tests reject an unreviewed new column/table. A foreign key also prevents a job from linking a result batch belonging to another profile.

Export version 2.3 adds the discovery section and batch link to the existing account export. Older history, score JSON, unknown score fields, feedback and security exclusions remain intact. Reader failures propagate; the endpoint must not return a successful partial export when discovery reads fail. The active account-to-profile relationship is resolved in the database, not supplied by an export query parameter.

Before job execution is enabled, lookup query payloads need the approved transient handling: preserve required inputs while work is pending, clear raw lookup query text when the job becomes terminal, and keep only the bounded idempotency/result metadata needed for replay. Do not create a permanent per-person keystroke/search log. Generation input snapshots and explicit taste selections have their own reviewed ownership; public cache metadata is separate.

Lease credentials, guest session credentials and account authentication internals must never appear in exports, API status, logs or browser-visible diagnostics. Later session tables require an explicit inventory/exclusion update before use. Guest expiry and deletion must fence in-flight work so it cannot recreate cleared data.

## Migration boundary

The additive migration creates one registered profile per existing subscriber, including existing hidden rows, and one historical batch per digest. Profile/batch IDs preserve existing subscriber/digest IDs. Existing recommendation IDs, score JSON, feedback, dates and track ownership are not rewritten. A historical batch completion timestamp is set from the original digest creation only when persisted recommendations exist; no new listening or provider evidence is inferred.

New accounts/digests created by old handlers during the additive stage can still lack the new association. C3 must backfill those records in its stopped-writer cutover before new discovery APIs rely on the relationship. This is not a rolling mixed-binary migration promise.

The C2 down/reapply check covers an isolated populated legacy fixture. A downgrade removes the new metadata tables and is not a recovery strategy for a retained database that has acquired guest data or new on-demand history. Real deployment requires a reviewed backup/recovery procedure before applying schema changes; no retained application database was migrated during C2 verification.

## Ownership cutover (C3)

TasteSignals.ProfileId and WeeklyRecommendations.ProfileId/BatchId now identify the private owner and recommendation request. WeeklyDigests.ProfileId identifies the owner of its batch. The legacy account and weekly recommendation links are nullable for guest and on-demand rows. Every new column is included in the DTO/export and reviewed schema inventory. `discovery.recommendations` includes all profile-owned recommendation rows, including non-weekly rows, with original score JSON and feedback. Shared track records remain outside private profile ownership.

Registered endpoints resolve their account through ListenerProfiles. A profile ID need not equal its account ID. Nullable account backreferences are compatibility metadata; deferred database constraints require any populated backreference to agree with the profile owner. Recommendation batch/profile foreign keys reject cross-owner links. Account deletion removes owned rows through the profile even when their legacy account reference is null.

Apply C3 only with old writers stopped. It repeats the additive backfill for records created since C2, then assigns ownership in place. No score/feedback column is rewritten. Downgrade refuses guest profiles or non-weekly recommendations; restore a reviewed backup rather than discarding those records. No retained database has been migrated in this session.

## Guest lifecycle and weekly boundary (C4)

`GuestSessions` stores ProfileId, CreatedAtUtc and CredentialHash. ProfileId/CreatedAtUtc are exported as guest session metadata. CredentialHash is an explicitly excluded operational authentication secret, like job LeaseToken. Only a SHA-256 hash of a random 256-bit credential is persisted; the credential stays in an HttpOnly, SameSite Strict cookie and uses Secure in HTTPS deployments. No guest has an Identity or subscriber row. Ordinary bootstrap/search does not create profiles.

The guest export includes all profile metadata, taste signals, batches, recommendations and jobs, including audit fields and structured historical score evidence, with the two operational secrets excluded. Expiry is checked during credential resolution before cleanup, not only by a timer. Activity can extend expiry at most once per hour; active interactions renew browser cookie lifetime. Cleanup deletes at most 100 expired profiles per slice through profile cascades.

New-account registration verifies the current guest cookie and CSRF/origin before attaching the same profile inside the account transaction. The guest credential is deleted and the cookie revoked after commit. Authenticated account identity takes precedence over a separate guest cookie. Existing-account import is a separate, explicit protected action: preserve the account profile; cancel unfinished jobs for both profiles; retain recommendation IDs, scores and feedback; keep the account's duplicate manual seeds; namespace imported request keys by immutable IDs; transfer remaining rows; increment the account profile revision; then delete the source profile and credentials atomically. This is not automatic login behavior.

Users.WeeklyEmailOptIn is included in subscriber DTOs/account export and the reviewed inventory. It defaults false, including migrated accounts without recorded consent. Weekly selection requires opt-in, verified Identity email, a live account and no unsubscribe timestamp. Onsite discovery does not require email verification. No email transport or verification delivery feature is added here.

Guest routes default disabled. The C4 write coordinator intentionally refuses writes until C7 installs coordinated accounting; HTTP lifecycle tests override it only with a named transactional mechanics fixture. In-flight generation and storage admission race evidence belongs to C7–C8, before the feature is enabled.

## Public catalog inventory (C5)

The reviewed inventory adds public CatalogPublications, CatalogHead, CatalogRecords,
CatalogSearchTerms and AlbumCredits, plus Albums.ReleaseGroupMbid. They contain no listener
or account owner. Publications retain source/version/URI, origin, licenses, attribution,
artifact hash/bytes/counts, import cursor/state, acquisition/publication/expiry timestamps,
normalization version and whether publication came from an explicit lookup. Head contains
only the active bulk publication identity.

CatalogRecords contain canonical identity, artist/album references, immutable versioned
labels/disambiguation/date, aliases/tags/credited-artist JSON and a normalized-row hash.
Search terms contain record identity, kind and normalized public text. AlbumCredits retain
all canonical album/artist relationships and credit order. ReleaseGroupMbid distinguishes
album taste identity from the historical release identifier. These public shared records
are excluded from account deletion and global account export; selected private evidence
and references require their own reviewed export/pinning boundary in C6. The schema test
explicitly lists every column, including all public operational import metadata.

Lookup jobs remain profile-owned. Their status endpoint never returns PayloadJson or raw
search query text, and owner resolution uses the validated listener context. No jobs can
be submitted until a consumer is ready. Raw lookup payloads still must be cleared at the
terminal transition in C7/C8. Shared lookup acquisition/cache keys must not preserve a raw
per-person search log. Search alone neither allocates a profile nor calls a provider.

Import restart verifies fingerprints of already committed rows against the approved input,
and the bounded reader verifies the bytes actually consumed before successful completion.
The active pointer is switched only after complete verification. A saved cursor alone is
not authority to publish previously staged rows. Public preparation cannot bypass the
managed-write coordinator while the C7 prerequisite is unfinished.


## C6 canonical taste selections

TasteSignals.CatalogRecordId, CanonicalEntityId and SelectionJson are private selections.
SelectionJson snapshots the chosen public item, credited artists, scoped tag weights,
source/version/hash, licenses, attribution, normalization and freshness. All three
fields are included in guest and account taste exports (account export version 2.4)
and in stored score evidence. They cascade with their profile's taste signals and
are transferred with explicit registration/import. They are never a shared taste cache.
The catalog record ID is a historical reference, without a foreign key: the private
snapshot must remain exportable when an expired public cache version is reclaimed.
Import deduplication uses kind and canonical identity when present; legacy name-only
signals are deduplicated separately. Distinct same-name canonical artists survive.

Prepared pools contain public per-artist/tag candidate evidence only. They contain no
profile, guest credential, selections, ratings or exclusions. C6 retains these pools
only in byte-budgeted process memory; durable prepared publication stays disabled
until C7 can reserve it. No-store, private, expired, or restricted evaluation evidence
is ineligible for shared cache retention. Byte estimates bound cache admission;
they are not process RSS measurements or a complete host memory bound.
