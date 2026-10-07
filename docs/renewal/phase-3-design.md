# Phase 3: honest transient ingestion

The developer requested Phase 3 execution in this session. The existing approved
phase contract specifies removal of invented evidence, explicit provenance,
preservation of discovery paths and unknown-seed gaps, using offline inputs only.
Implementation verification remains distinct from developer confirmation.

## Schema preflight and placement

`RawCandidateTrack` is used by the Infrastructure source/hydrator and their tests.
`CandidateTrack` is used by the pure scorer and synthetic benchmark tests. Neither
is an EF entity: there are no DataAccess mappings, DbSets, migration or snapshot
references to either type. Presentation/Worker do not consume the discovery or
hydration interfaces. Track/Artist and persisted ScoreBreakdown remain separate.

Use the existing Application recommendation models/interfaces and Infrastructure
LastFm model/client/fixture folders. Use existing Infrastructure.Tests and
Application.Tests folders. No new project, dependency, migration, public endpoint,
export contract or persistence. Scorer policy/formulas remain later work.

## Concrete in-memory contract changes

- Generic evidence records retain raw values, nullable parsed values and reasons
  such as missing, malformed, nonfinite and out-of-range. Provider-specific parsing
  lives in Infrastructure. Response references carry provider, method, request seed,
  origin (`Live`, `Recorded`, `Synthetic`), exact response SHA-256 and retrieval time.
  If no complete response was received, hash/time remain null. Synthetic payloads use
  a declared fixed epoch timestamp; they do not claim an upstream retrieval.
- Low-level Last.fm results become immutable list-compatible envelopes with a
  response reference and explicit gaps. Existing method names, parameters and
  result indexing/enumeration remain; return types gain metadata. Parsing is shared
  between HTTP and an explicitly injected in-memory recorded-response client.
- Discovery returns a list-compatible result plus gaps. Raw candidates retain all
  discovery path observations (seed, match evidence, response references, upstream
  positions in similarity/track responses, supplied track/artist attribution, raw rank
  and track listeners) instead of dropping later paths.
  The old single UpstreamScore becomes nullable; it is a compatibility projection
  only when there is one path, not adoption of a match aggregation formula.
- Hydration returns a distinct Application `HydratedCandidateTrack` snapshot:
  Domain Track/WeightedTagVector plus the raw candidate, complete tag observations,
  tag response reference, tag scope/normalization version and gap reasons.
  Artist-wide popularity remains explicitly unknown. Track listeners stay attached
  to each supplied track observation, never relabelled as artist listeners.
  This snapshot cannot accidentally enter the legacy scorer's non-nullable numeric
  CandidateTrack contract. A later reviewed pipeline must decide that conversion.

## Behavior

FixtureOnly is explicitly synthetic. Unknown fixture artist/tag keys return empty
data and a coverage gap; no invented Related A/B artists, tracks or generic tags.
Hybrid is retained as a configuration spelling, but no longer falls back to
fixtures on missing credentials, HTTP/provider failures or timeouts. Failures are
gaps with their original origin. No request can blend live/recorded/synthetic paths;
discovery/hydration reject mixed origins. Cached envelopes preserve original
timestamps, hashes and origin. Cache keys include mode/provider identity and full
canonical request; responses marked non-cacheable are not cached. Only supplied HTTP
freshness permits cache reuse; no default retention duration is adopted here.

Numeric match values must be finite in the documented 0–1 range. Invalid values
stay invalid, without clamping/default 0.5/0.70. Tag names/counts preserve missing
and invalid counts. Retain the existing stoplist, without tuning it from fixtures.
Normalize only valid positive descriptive counts by their observed maximum;
record this versioned derivation, without a fabricated minimum weight. If none
exist, the tag vector is empty with an explicit reason. Do not request top tracks
as an artist-popularity estimate during hydration.

Keep candidate identity/deduplication ordinal and deterministic; sort paths by
canonical seed/method/hash/position while retaining repeats. Do not choose a
new production representative-track policy: preserve the existing two-track
acquisition bound and upstream position, documenting its popularity-ordered pool.
Unknown or malformed artist/track identity produces a gap rather than Unknown
Artist. Cancellation propagates; logs use constant reasons, never exception text
or credential-bearing request URLs.

Recorded replay uses caller-supplied byte snapshots, verifies their exact hashes,
and has no HTTP/credential access or file discovery. Real replay approval and
held-out access remain gates for Phase 4/5. Phase 3 tests use fabricated bytes only.

## Verification and change groups

1. Transient metadata/evidence and immutable result snapshots; no-schema checks.
2. Fail-closed client/shared parser and in-memory replay; unknown fixture gaps.
3. Complete discovery paths and honest hydration; cancellation and origin isolation.
4. Focused regressions, full offline suite, evidence, changelog and progress.

The existing hydration assertion that a top track implies artist popularity must
be replaced with a stronger scope assertion (artist popularity null, original
track listeners retained). The existing hybrid-fallback assertion must assert an
empty failure gap and no synthetic substitution. These changes implement the
explicit Phase 3 contract; no tests are removed. Other existing expectations are
retained or strengthened. Add independent missing/invalid/path/provenance cases,
including baseline failing checks before fixes where existing interfaces permit.
Run the existing Domain benchmark suite to ensure scorer behavior is untouched.

Expected effort: several focused implementation/check units. All testing remains
offline and any result is labelled `Mechanics only, no catalog conclusions.`
