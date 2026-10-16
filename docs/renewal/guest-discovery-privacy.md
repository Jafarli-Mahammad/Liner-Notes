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
