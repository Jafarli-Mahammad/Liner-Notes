# Phase 6 implementation review

Status: written design approved and implementation explicitly requested on 2026-10-10;
provisional baseline A approved under an explicit Phase 5 evidence bypass.
Implementation is locally verified; see [evidence and configuration](phase-6-evidence.md).
This document records the pre-implementation source audit and approved design. It
does not authorize live acquisition. Its concrete JSON/API, feedback, selection and
storage contracts are approved for Phase 6 implementation. Baseline A's
recommendation quality remains **not verified**; evaluation and subsequent formula
choice remain open. No implementation is developer confirmed.

## Intended outcome

Produce a deterministic weekly digest from a user's stored taste signals through
materialization, discovery, hydration, eligibility, ranking and persistence.
Every persisted pick must explain and reproduce its score from stored evidence.
Domain remains pure; Application orchestrates; provider details stay behind
Infrastructure interfaces. Phase 7 scheduling/email and playback resolution are
later work.

## Current source audit

Inspected against checkout `419fa34` on 2026-10-09:

- `IRecommendationSource` and `ICandidateHydrator` already return explicit gaps,
  response references and separate recorded/live/synthetic origins. Reuse
  `LastFmEvidenceParser` and `RecordedLastFmApiClient`.
- `RecommendationScorer` still subtracts popularity and rejection penalties.
  `ScoringParameters.Default` includes popularity weight 0.3. This is legacy
  behavior, not approved baseline A.
- `ScoreBreakdown.MatchedTags` contains at most five raw products. Those values
  do not reconstruct normalized cosine or the complete score. The record has no
  formula/configuration version or unknown-field preservation.
- `WeeklyRecommendationConfiguration` stores that record in existing JSONB.
  JSON compatibility and PostgreSQL runtime round trips are not verified.
- Application has no digest-generation handler or taste-materialization flow.
  Worker registers dependencies but schedules no work.
- Feedback replacement is transactional and deletes prior derived signals for
  the same recommendation. AlreadyKnown can accompany a rating; low ratings
  create track rejection, and negative feedback does not subtract artist tags.
- `Track.TrackKey` prefers MBID, otherwise normalized artist/title. Materializing
  exclusions must also compare artist/title so missing MBIDs cannot bypass them.
- The current digest export maps a subset of the stored breakdown and limits
  history to 1,000 digests. It cannot expose complete new evidence as written.
- WeeklyDigests has a unique `(UserId, WeekStartDate)` index, but the user/week
  index is nonunique. Canonical ISO-week dates and concurrent generation need
  verification before claiming idempotency.
- Configured hooks are `.githooks/pre-commit` and `.githooks/pre-push`. The
  codebase-memory MCP indexes other checkouts; source files in this checkout
  are the evidence for this review.

## Approved formula decision

The developer explicitly approved proceeding with provisional baseline A and
deferring real evaluation on 2026-10-09: "yes for now yes lets keep things open and
unverified, we will get back to it". Do not treat this as evidence of recommendation
quality, permanent formula selection or permission to execute Phase 5.

The remainder is the written design approved for implementation on 2026-10-10.
Its original proposal wording records the reviewed choices; it is not a pending reapproval gate.

## Approaches considered

| Approach | Consequence |
|---|---|
| Recorded input and complete snapshots in existing JSONB (recommended) | Keeps local Phase 6 mechanically verifiable with no new SQL schema; requires explicit JSON/API review. |
| New evidence tables and production acquisition | Adds retention, migration, coordination and upstream permission decisions; exceeds the concrete scope proposed here. |
| Add only orchestration around the legacy scorer | Retains popularity penalties and incomplete explanations; does not satisfy approved V1 policy or Phase 6. |

## Proposed policy and acquisition

Use ordinary aggregate cosine with configured tag weight 1.0 and novelty weight
0.2; popularity, feedback penalties and similarity-match bonuses are zero.
Persist actual configured weights and a configuration hash. Familiar seed artists
receive no novelty bonus, following evaluation A's binary artist familiarity.
Known and disliked tracks are excluded before scoring; sibling tracks remain
eligible. An empty tag vector produces zero similarity with a stored reason.

Keep ordinal canonical-key ties. Select the highest scoring eligible track per
artist, using the same tie policy, then select up to five artists. A short or empty
supported list is valid. Do not select the upstream highest-ranked track as a
separate representative rule. Upstream top-track acquisition still biases the
candidate pool; retaining its original positions does not remove that bias.
This batch selection is approved for Phase 6; future real evaluation must reassess its applicability.

Use a new pure `BaselineAScorer` for generation while retaining the legacy scorer
and its existing test contracts. The production composition uses only baseline A.
Do not use testing-only `PilotScoring` from product assemblies. A later formula can
be added behind the same pure scoring contract without migrating existing JSONB.

Use a recorder-fed offline pipeline initially. An explicit host configuration
selects recorded or synthetic input; credentials alone cannot enable live calls.
Never mix origins or silently substitute fixtures for missing recordings. Keep
the existing provider abstractions so a separately approved live adapter can be
added later. Seed artist tag materialization also uses the injected client, with
bounded requests and cancellation.

## Proposed persistence and export changes

Prefer **zero new SQL tables, columns, indexes or migrations**, subject to an EF
model comparison. Persist complete versioned evidence inside the existing
`WeeklyRecommendations.ScoreBreakdown` JSONB column. Do not persist raw HTTP
bodies or a second shared tag catalog.

Each new breakdown adds:

- Payload, formula, materialization, tag-normalization and ranking-policy versions.
- Actual configured weights and configuration hash.
- Complete candidate and aggregate taste vectors, their magnitudes, all normalized
  overlapping tag contributions, familiarity input and named score components.
- Canonical track identity and supplied ISO week for replay.
- Provider attribution, origin, discovery paths/positions, response hashes/times,
  supplied match/listener observations, missing/invalid reasons, and tag evidence
  required to audit materialization and scoring.

Use provider-neutral stored records; Application maps transient ingestion models
to those records. Domain never references Infrastructure or Application types.
Preserve unknown JSON fields, including nested versioned records. Unversioned
existing payloads remain readable as legacy; do not invent missing historical
evidence or relabel their raw products as normalized contributions. Unsupported
formula versions remain inspectable and fail explicit replay.

Add the complete versioned breakdown to digest DTOs while retaining existing
fields for compatibility. Version the export as `2.0`, expose the complete stored
breakdown and all digest history through bounded pagination internally, and
include identifiers/audit fields for affected account-owned records. Security
credentials remain outside the export. No generation HTTP endpoint is proposed;
generation is an Application command for a later Worker/local invocation.

The exact JSON/DTO contracts were approved with the implementation instruction. If a future EF
model comparison shows a SQL migration is required, present its exact changes
before creating it. SQL neutrality does not eliminate the JSON/API review gate.

### Concrete JSON contract proposed for approval

Retain the existing top-level `ScoreBreakdown` fields and add a nullable `Snapshot`
object. Legacy payloads without it remain legacy. New snapshots have these fields:

| Field | Stored value |
|---|---|
| `PayloadVersion`, `FormulaVersion` | `phase6-score-v1`, `baseline-a-v1` |
| `ConfigurationVersion`, `ConfigurationSha256`, `Weights` | Explicit configuration identifier, SHA-256 of canonical versioned configuration, configured tag/novelty coefficients |
| `MaterializationVersion`, `TagNormalizationVersion`, `RankingPolicyVersion` | `positive-signals-v1`, existing `max-observed-descriptive-count-v1`, `score-ordinal-one-artist-v1` |
| `TrackKey`, `ArtistName`, `Title`, `Week` | Selected canonical identity and supplied ISO week |
| `TasteWeights`, `CandidateWeights` | Complete normalized-name vectors in ordinal tag order |
| `TasteMagnitude`, `CandidateMagnitude`, `Familiarity` | Actual finite scoring inputs |
| `Contributions` | Every overlapping tag's name, candidate/taste weights and normalized weighted contribution |
| `Components` | Named tag-similarity and novelty components whose sum equals FinalScore |
| `MissingReasons` | Explicit absent seed/candidate evidence reasons |
| `Evidence` | Provider-neutral seed/materialization and selected candidate tag observations, discovery paths, response references and gaps |

Versions, sorted keys and invariant numeric formatting define the configuration
hash. Each versioned object retains unknown JSON properties on read/write. JSON
serialization stays in the persistence boundary; no vendor type enters Domain.
Legacy scalar fields stay available for existing consumers; new normalized
contributions have an explicit field name rather than changing the meaning of
legacy `ContributionProduct`. New popularity and feedback penalties are zero.

Store only selected picks. Missing coverage for an empty digest is returned by
the generation command; no new digest evidence column is proposed. Empty digests
remain distinguishable from generation failures in that command result. Scoring
replay uses stored snapshot vectors/parameters, never current upstream responses
or a user's subsequently changed taste.

### Concrete API/export contract proposed for approval

- `ScoreBreakdownDto`: append nullable `Snapshot` carrying the complete stored
  snapshot, plus the previously omitted raw scalar inputs. Preserve existing
  member names and legacy semantics.
- `WeeklyDigestDto`: append `CreatedAt`, `LastModifiedAt` and `ErrorMessage`.
- `WeeklyRecommendationDto`: append `UserId`, `WeeklyDigestId`, `CreatedAt` and
  `LastModifiedAt`; keep rating and familiarity behavior compatible with the
  existing feedback request.
- `TrackDto`: append normalized title and artist name so persisted catalog
  metadata referenced by picks is inspectable.
- `TasteSignalDto`: append `LastModifiedAt` and the audit fields listed below;
  its existing identifier/ownership/target fields are retained. Connection export
  DTO: append `Id`, `UserId`, `LastModifiedAt` and the same audit fields.
  `SubscriberDto`: append `LastModifiedAt` and the same audit fields; it already
  exposes email, timezone, delivery day/hour and next digest date.
- For exported account-owned AuditableEntity records (subscriber, connections,
  taste signals, digests and recommendations), expose `CreatedBy`, `LastModifiedBy`,
  `DeletedBy`, `DeletedAt` and `IsDeleted` alongside their identifiers and timestamps.
  Do not export `UserMusicConnection.EncryptedToken`, Identity hashes, refresh
  tokens, protection keys or upstream credentials. Shared artist/album/tag tables
  have no added Phase 6 writes under this design; referenced Track columns are
  covered by TrackDto. These explicit fields are the proposed Phase 6 boundary,
  not a claim that Phase 8a's full inventory has passed.
- `GET /api/export/my-data`: bump `ExportVersion` from `1.0` to `2.0`, remove the fixed
  1,000-digest truncation using stable bounded database pages, and preserve unknown
  snapshot fields. Inspectability through existing digest/data views uses the
  same mappings. Phase 8a still verifies the complete account inventory/deletion
  boundary independently.

No new request endpoint or feedback request field is proposed. Familiarity plus
rating is represented by the existing `AlreadyKnown` category and independent
`Rating`. Verify both request ordering directions; a later rating must not clear
familiarity. If this representation cannot preserve the required behavior, stop
and present an exact additional API/schema proposal before implementing it.

Audit fields added to digest/recommendation DTOs include the same set above;
export mapping must not omit them through reuse of the digest view DTO.

## Proposed feedback and retention

Retain the current Phase 1 conflict rule: reject Liked with ratings 1–3 and
Disliked with ratings 7–10. Ratings 4–6 are neutral unless a categorical like or
dislike is supplied. AlreadyKnown marks familiarity independently of rating;
AlreadyKnown with a high rating can reinforce taste, and with a low rating can
also reject that track. Replacement removes the prior recommendation's positive
contribution and recomputes materialization from current signals. Cross-digest
conflicts for the same track require a deterministic latest-feedback rule with
an ordinal identifier tie-break, rather than retaining a stale rejection forever.
Use `FeedbackGivenAt` then recommendation ID for the latest rating/category;
familiarity is accumulated independently from AlreadyKnown signals. A replacement
rating/category preserves an existing familiarity signal until it is explicitly
cleared or the account is deleted. Rebuild positive taste from current effective
feedback so a superseded like cannot remain through another digest's stale signal.

Do not adopt the proposed 12-month history or 30-day recording/backups retention
implicitly. Recommended initial policy is no automatic history deletion; retain
active seeds/feedback/familiarity until changed or account deletion, and use the
approved recording manifest's expiry. New persisted snapshots require an explicit
storage limit and reconciled headroom. Unknown or insufficient inventory stops
new snapshots while allowing account, feedback and deletion operations.

This policy and the storage-accounting mechanism are approved. A process-local counter
cannot establish shared usage. PostgreSQL synthetic sizes are now measured in the evidence
document; supported capacity and real-data size distributions remain not verified.

Generation requires a storage reservation supplied by an Infrastructure adapter
under an exclusive local batch lease. Include recordings, cache, derived database,
reports/copies, backups and partials. Require a reconciled inventory no older than
five minutes and revalidate revisions immediately before persistence. Reserve
actual serialized selected-pick bytes plus a configured database-overhead bound;
reject projected shared usage at or above 1,000,000,000 bytes as a temporary,
unresolved placeholder; set the real limit at build-order step 9 once hosting is
chosen. Verify actual stored
column sizes inside the transaction, rolling back if the reservation is exceeded.
An unconfigured overhead bound or unavailable inventory fails closed. The operator
supplies approved artifact roots; do not scan unrelated files or credentials.
No automatic schedule or concurrent acquisition is enabled in Phase 6. Database
feedback/account writes remain available even when generation is stopped.

## Generation flow and limits proposed for approval

1. Check user and existing canonical ISO-week digest. A retry returns the existing
   digest without querying a provider.
2. Read stored signals and recent effective track feedback; normalize identities
   and enforce known/disliked exclusions using both MBID and artist/title aliases.
3. Sum positive manual tag weights and weighted seed artist tag vectors in ordinal
   order. Each current positive artist signal contributes its seed vector; direct
   feedback tag nudges contribute their configured positive weight. Negative
   feedback contributes exclusions only. Persist the materialization inputs that
   affected each selected pick. Invalid/nonfinite inputs stop generation.
4. Discover through injected provider interfaces and merge all paths for duplicate
   tracks; hydrate bounded distinct artists. Reuse cached seed tag responses within
   the batch. Preserve gaps; do not fabricate evidence.
5. Score, choose at most one eligible track per artist, and select at most five
   picks. Persist snapshots and catalog references in one transaction after the
   storage reservation succeeds. Canonicalize WeekStartDate from ISO week; serialize
   generation for that user/week and verify concurrent requests return one digest.
6. Return the persisted digest plus nonpersisted coverage diagnostics. Rollback
   cancellation/failure without partial digests or catalog writes. No email is sent.

Explicit versioned configuration caps manual/feedback discovery seeds at 20 artists
and 20 tags, artist-discovery limit at 10, hydrated candidates at 400, serialized
snapshot size at 100,000 UTF-8 bytes/pick, and selected picks at five. Overflow
returns a limit result instead of silently discarding inputs. These are proposed
mechanical bounds, not provider entitlements or demonstrated supported capacity.
The caps are reviewed with the design and remain adjustable versioned configuration.

Read-only seed evidence acquisition precedes the database write transaction. Validate
the materialized signal/feedback revision again before commit; changed feedback
aborts the generation attempt instead of persisting a stale taste snapshot.

## Implementation and verification breakdown

Expected effort: several implementation passes across Domain, Application,
DataAccess and Infrastructure plus database verification; this is substantial
phase work. Proposed focused local commits after decisions/design review:

1. Pure baseline-A scoring, eligibility and versioned complete breakdowns. Check
   all benchmark seed genres, popularity neutrality, complete contribution sums,
   empty vectors, finite arithmetic, stable order and stored-input replay.
2. Cancellable bounded materialization and offline generation; atomic catalog and
   digest persistence. Check origin isolation, gaps, limits, cancellation,
   rollback, repeated/concurrent generation, canonical week dates and deduplication.
3. JSON persistence and export compatibility, feedback conflict materialization,
   storage policy and evidence/docs. Verify old/new/unknown-field payload round
   trips, EF model neutrality, actual PostgreSQL persistence and `pg_column_size`
   median/p95/max/total, export completeness and affected deletion paths. Reuse
   existing test projects and preserve legacy tests until contract changes are
   reviewed. Run the full suite, documentation check and configured commit hook.

If database checks cannot run or verification fails, report the limitation and
ask before committing implementation. Synthetic checks establish mechanics only:
**Mechanics only, no catalog conclusions.** Developer confirmation remains
separate from local verification. No pushing or deployment is authorized.
