# ListenBrainz default provider plan

Date: 2026-10-15. Approved by the developer, with implementation continued on
2026-10-16. This document records the approved design; current verification and
remaining rollout gates are in [implementation evidence](listenbrainz-provider-evidence.md).

## Requested outcome

Make **ListenBrainz** the main/default provider. Keep **Last.fm** implemented
and usable. Later let users choose between them, with ListenBrainz the default.
Provider choice means discovery and supporting tag evidence; it does not
require an upstream account.

Stage A retains manual artist/tag onboarding. Account linking, listening-history
imports, upstream feedback submission and playlist sync are separate features.
Liner Notes continues deterministic scoring and feedback re-ranking of upstream
candidates; it does not build similarity data or run ListenBrainz's engine.

## Current implementation

- `src/Infrastructure/DependencyInjection.cs` binds `IRecommendationSource` to
  `LastFmRecommendationSource`, `ICandidateHydrator` to `LastFmCandidateHydrator`,
  and `ISeedTagSource` to `BatchTagCachingLastFmClient`.
- `GenerateDigestCommandHandler` consumes all three. Replacing discovery alone
  would leave Last.fm tag requests in the ListenBrainz path.
- `IRecommendationSource` already supports artist-name and tag seeds. There is
  no ListenBrainz adapter. Provider enums and `UserMusicConnection` do not
  establish account linking or a recommendation-provider preference.
- Evidence carries provider, method, request identity, origin, response hash,
  retrieval time, discovery paths, tags and coverage gaps. Reuse this trail.
- `GenerationConfiguration.Scoring()` accepts Synthetic/Recorded only; recorded
  generation requires an approved, unexpired manifest. A live HTTP adapter does
  not establish live digest generation support.
- `DigestEmailRenderer` hard-codes Last.fm attribution. Audit web copy, exports,
  snapshots and evaluation scripts alongside DI.
- Phase 5 quality evaluation remains separately unauthorized. Fixture results
  establish mechanics, not ListenBrainz quality or catalog coverage.

## Approach choices

| Approach | Tradeoff | Recommendation |
|---|---|---|
| Replace discovery alone | Small change; retains hidden Last.fm tag dependency and release gates | Reject |
| Resolve a complete provider bundle | Switch discovery, candidate hydration and seed tags together; prepares user choice | Adopt |
| Require an upstream account for personal recommendations | Adds identity/import work and excludes manual-seed users | Defer |

## Stage A: provider support and default transition

### 1. Confirm endpoint, evidence and usage contracts

Official documentation reviewed on 2026-10-15. This is documentation evidence,
not successful live requests, coverage measurement or release approval.

Proposed manual-seed path:

1. Resolve artist names to MusicBrainz artist MBIDs through bounded artist
   search. Define exact-name/alias acceptance and ambiguity rules before coding.
   Ambiguous or unresolved names become gaps, not a silent namesake selection.
   Cache successful and negative resolutions.
2. Use ListenBrainz `GET /1/lb-radio/artist/{seed_artist_mbid}` and
   `GET /1/lb-radio/tags` for discovery. Validate mode, limits, ordering and broad
   popularity-range parameters before fixing a versioned policy. These upstream
   choices may influence popularity; do not claim a statistically neutral pool.
3. Batch `/1/metadata/recording/` for titles, artist credits and tags. Prefer
   artist-scoped tags for candidate and seed vectors to match current semantics.
   Use MusicBrainz artist tags for seed hydration where needed, subject to the
   license gate below. Never silently mix artist/recording/release tag scopes.
4. Preserve recording MBIDs and correct response provenance. Omit unresolved
   candidates with explicit gaps. Do not invent missing titles, tags or counts.

The account-based `/1/cf/recommendation/user/{user_name}/recording` endpoint is
experimental and may have no generated recommendations. It is deferred.
Sources: [core endpoints](https://listenbrainz.readthedocs.io/en/latest/users/api/core.html),
[metadata API](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html),
[recommendations API](https://listenbrainz.readthedocs.io/en/latest/users/api/recommendation.html),
[MusicBrainz search](https://musicbrainz.org/doc/MusicBrainz_API/Search).

Before live acquisition or rollout:

- Record applicable ListenBrainz/MetaBrainz terms, privacy, attribution, retention
  and redistribution obligations for the exact fields consumed. Public listen
  data being CC0 does not establish the license of MusicBrainz-derived tags.
  MusicBrainz distinguishes core and supplementary data; confirm field
  classification and obligations. Verify deployment/service-use conditions.
- Send an identifying application/version/contact User-Agent. Follow
  ListenBrainz's documented one-request-per-second ceiling and response rate
  headers, tightening further when necessary. MusicBrainz has a separate
  source-IP limit of one request per second. Coordinate budgets across web and
  Worker processes; independent process limiters can exceed a shared-IP budget.
- Bound response size, retries, concurrency, timeouts, pagination and caches.
  Handle 429/backoff and transient errors without retry storms. Keep credentials
  out of URLs, logs, request identities and stored evidence.

Sources: [ListenBrainz API policy](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html),
[ListenBrainz terms](https://listenbrainz.org/terms-of-service/),
[MusicBrainz rate limits](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting),
[MusicBrainz data license](https://musicbrainz.org/doc/About/Data_License).
Recheck before coding. No new package is assumed necessary; verify current
stable releases and compatibility if adding one.

### 2. Add provider resolution while retaining the current default

Introduce a provider identifier and Application-level resolver returning a
coherent bundle: `IRecommendationSource`, `ICandidateHydrator`, `ISeedTagSource`.
Concrete clients, caches, HTTP contracts and provider policies stay in
Infrastructure. Both composition roots use identical resolution rules.

Initially select by operator configuration and retain Last.fm as the default
while building the adapter. Register implementations explicitly; do not depend
on DI registration order. Unknown identifiers fail validation. Do not initialize
or call the unselected provider, including its seed-tag service.

Resolve once per generation attempt. Persist selected provider and ingestion
policy/version with generation configuration/evidence. Represent a metadata
supplier separately from the recommendation provider where needed. Provider
selection is distinct from external-account connection records.

### 3. Implement the ListenBrainz bundle and replay

Add clients, options, parser, discovery, hydration, seed-tag handling and bounded
batch caching under `src/Infrastructure/RecommendationSources/ListenBrainz/`.
Use HttpClientFactory and cancellation throughout. Share parsing between HTTP
and immutable replay, following the existing Last.fm pattern without changing
its provider-specific parser into a misleading generic parser.

Preserve raw evidence and missing reasons. Listen counts are not unique
listeners; upstream rank is not similarity. Unavailable similarity stays
missing. Keep tag scope and count meaning explicit. Extend evidence narrowly
only when required for faithful representation; preserve old snapshot readers.

Keep the deterministic scorer, weights and popularity-neutral ranking unchanged.
Version any normalization change separately and sanity-check the benchmark seed
set. Ensure persisted normalization metadata describes the policy actually used,
including any hard-coded snapshot defaults that need correcting.

Provide explicit Synthetic, Recorded and Live client modes. Offline/replay runs
reject HTTP, missing recordings and mixed origins. Never replace live errors
with synthetic data. Approved real recordings remain private, gitignored,
expiring and bounded by existing recording rules.

### 4. Verify complete generation and attribution

- Exercise recorded-mode generation, approved manifest validation, storage
  reservations, persisted breakdowns, feedback, history, export, deletion,
  scheduling and encrypted local email.
- Preserve Last.fm code, parser, replay, configuration and tests. Keep historical
  provenance and explanations intact.
- Derive attribution from stored evidence, including required metadata supplier
  credit. Today's default must not relabel an old Last.fm digest.
- Keep feedback and already-known suppression valid across providers using
  recording MBIDs and existing normalized aliases. Check identity changes and
  duplicate editions without rewriting historical records.
- Do not broaden the Synthetic/Recorded generation guard incidentally. If live
  generation is needed, review an explicit Live policy covering origin checks,
  bounded writes, evidence persistence and failures. Preserve replay expiry.

### 5. Validate and change the default

After usage/endpoint checks, explicitly approved bounded acquisition, and
developer acceptance, switch both hosts, sample settings, Compose and runbooks
to ListenBrainz. Identify existing explicit Last.fm settings: changing a code
default does not override deployed configuration.

Recommended cutover: future generations without an explicit provider choice use
ListenBrainz. Historical digests remain unchanged. Existing user/week digests
are returned as before, not regenerated or emailed again. Retain documented
operator rollback to Last.fm, subject to its own usage gates.

Do not automatically fall back to Last.fm on failure or sparse coverage. Return
honest coverage/stop reasons and retain scheduling retry semantics. No hidden
Last.fm calls may remain in the ListenBrainz bundle.

## Stage B: future user choice (deferred)

Add a dedicated preference accepting `listenbrainz` or `lastfm`. Users without
a preference receive ListenBrainz; saving a choice makes it explicit. This
setting is not an upstream username, credential or connection.

Add ownership-checked settings commands/queries and a selector showing
**ListenBrainz (default)** and **Last.fm**. Explain that changes affect future
digests. Preserve taste signals, feedback and history. Show provider
unavailability explicitly rather than silently substituting one.

Read preference within the generation input snapshot and resolve one bundle.
Coordinate updates with existing input-revision checks so concurrent preference
changes cannot commit against stale inputs. Preserve user/week idempotency
across changes and retries. Include new columns in the data inventory, export
and account deletion; test cross-user access denial.

Add the migration/API/UI only when this stage is authorized. Resolve Last.fm's
public-use/attribution gates before exposing it. Account linking and listening
imports require their own design.

## Proposed commits, checks and effort

Estimates are focused engineering days, excluding external permissions,
recordings and developer review.

| Commit | Scope | Verification | Estimate |
|---|---|---|---|
| 1 | Endpoint/evidence contract and resolver; retain default | Both hosts, invalid provider, coherent bundle, no unselected-provider calls | 1–2 days |
| 2 | Clients, parsers and immutable replay | Ambiguity, missing metadata, batching/cache, retry/cancellation, offline HTTP rejection | 2–4 days |
| 3 | Discovery, seed tags, hydration and mapping | Artist/tag/mixed seeds, sparse responses, normalization, deduplication, unchanged scorer benchmark | 2–3 days |
| 4 | Recorded end-to-end flow and attribution | Both providers, snapshots/export, old digests, feedback/deletion/email, expiry/storage guards | 1–2 days |
| 5 | Separately reviewed live generation, if needed | Origin/failure boundaries, approved smoke acquisition, request/storage budgets | 1–3 days |
| 6 | Validated default cutover and rollback docs | Both hosts, packaged settings, no Last.fm dependency, idempotency, rollback | 1 day |
| Later | Preference, migration, API and selector | Ownership, concurrent edits, export/deletion, next-digest semantics | 2–4 days |

Stage A: **7–12 days without new live generation; 8–15 days with it**.
Sparse tags, ambiguous names, usage restrictions or poor coverage may change
the design and estimate. Do not promise parity with Last.fm before measuring.

Run relevant Domain/Application/Infrastructure checks per commit, then affected
DataAccess/Presentation/Worker integration checks. Before cutover run the full
required suite and packaged lifecycle checks with the pinned SDK and disposable
PostgreSQL. Report unavailable/skipped verification. Update CHANGELOG when
user-visible behavior is implemented; this document changes no runtime behavior.

Define coverage, tag completeness, latency, request and failure budgets before
the pilot. Freeze seeds/configuration before acquisition. Separate development
data from held-out quality evaluation; Phase 5 still requires authorization.
Fixture checks and a smoke run cannot establish recommendation quality.

## Acceptance and remaining gates

- Both adapters remain operable; unset preference uses ListenBrainz only after
  cutover validation. No Last.fm calls occur on the ListenBrainz path.
- Picks carry honest provenance and stored explanations. Historical Last.fm
  digests, feedback, history, export and deletion continue working.
- Record usage/license decisions, approved live evidence, deployment checks and
  developer confirmation separately from local test results.
- Provider choice UI and account linking/import remain deferred. Implementation
  review/authorization for those deferred features and release readiness remain open.
