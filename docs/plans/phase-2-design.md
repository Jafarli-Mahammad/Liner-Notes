# Phase 2 mechanics design and review items

Execution requested by the developer on 2026-10-07. This implements the approved
Phase 2 contract only. Implementation verification is not developer confirmation.

## Placement and scope

Use existing folders/projects: harness helpers and tests in
`tests/Application.Tests/Common/`, synthetic benchmark checks in
`tests/Domain.Tests/Fixtures/`, recorder and its fake-input checks in `scripts/`.
No production scorer, ingestion, database, dependency, API or export change.
The harness references Application/Domain only and has no provider or credentials.
Recorder implementation uses Python's standard library; it is not run against Last.fm.

Proposed change groups: (1) profile lock and fixture-only harness; (2) metrics and
mechanics checks; (3) recorder, manifest validation and storage guard; (4) evidence,
changelog and progress. Expect several focused implementation/verification units.
All generated locks, manifests, responses and reports remain under ignored
`recordings/`; no such artifacts are committed. This document is a design proposal,
not a generated lock or an approved recording manifest.

## Membership approved by the developer on 2026-10-07

Membership is metadata only: no artist response, tag frequency, ranking or outcome
has been inspected. Artist identities use NFC, trim and invariant lowercase,
without accent removal, alias inference or MBID guessing. Each row is one profile;
semicolon separates profiles and comma separates literal artist seeds.

| Set / genre | Profile 1 | Profile 2 |
|---|---|---|
| Development / Metal | Agalloch, Alcest | Metallica, Slayer |
| Held-out / Metal | Wolves in the Throne Room, Panopticon | Judas Priest, Iron Maiden |
| Development / HipHop | Billy Woods, Armand Hammer | Drake, Future |
| Held-out / HipHop | Open Mike Eagle, Quelle Chris | Kendrick Lamar, J. Cole |
| Development / Electronic | Skee Mask, Autechre | Calvin Harris, deadmau5 |
| Held-out / Electronic | Aphex Twin, Squarepusher | Avicii, Disclosure |
| Development / Jazz | Pharoah Sanders, Alice Coltrane | Miles Davis, John Coltrane |
| Held-out / Jazz | Sun Ra, Albert Ayler | Herbie Hancock, Wayne Shorter |
| Development / Pop | Julia Holter, Weyes Blood | Taylor Swift, Carly Rae Jepsen |
| Held-out / Pop | Kate Bush, Björk | Dua Lipa, Robyn |
| Development / Indie | The Murder Capital, Fontaines D.C. | Arctic Monkeys, The Strokes |
| Held-out / Indie | Protomartyr, Preoccupations | The National, Interpol |
| Development / Classical | Sarah Davachi, Kali Malone | Ludovico Einaudi, Max Richter |
| Held-out / Classical | Éliane Radigue, Pauline Oliveros | Nils Frahm, Ólafur Arnalds |
| Development / RegionalScene | Altın Gün, Gaye Su Akyol | Alim Qasimov, Fargana Qasimova |
| Held-out / RegionalScene | Erkin Koray, Selda Bağcan | Vaqif Mustafazadə, Aziza Mustafa Zadeh |

Six founder profiles preserve the existing clusters: Jakuzi; Son Feci Bisiklet;
M.O.O.N., Perturbator, Jasper Byrne; Paweł Błaszczak; Heaven Pierce Her; Darren Korb.
The five singleton profiles deliberately remain sparse. Pilot additional seeds must
be development seeds, selected in the separately approved concrete pilot manifest.

The developer approved both the membership table and offline legacy-test replacement
in this session. Membership hash:
`de78f92afd725f4dd22da6446a9c41f5ca78f6959eacf14753eb092902b62c40`.
The local generated artifact is `recordings/phase2-membership-2026-10-07/profile-lock.json`;
the committed helper reconstructs the same reviewed metadata and verifies that hash.

Synthetic mechanics reuse the existing eight genre benchmark vectors. Separate
missing, invalid, tied, empty and exclusion cases are explicitly synthetic. Synthetic
metadata uses isolated identities and cannot masquerade as a real split.

Hash a sorted, length-prefixed UTF-8 representation of version, profile IDs, sets,
genres and seed identities. Validate counts, unique IDs, all eight genres and disjoint
real seed membership. A review reference plus the expected hash is required before
any harness ranking. Copy all inputs into immutable snapshots. Test-only review
references validate lock mechanics and do not approve a different real split.

## Legacy test change approved by the developer on 2026-10-07

Replace the body of
`tests/Infrastructure.Tests/Spikes/FounderSeedCoverageSpikeTests.cs` with an offline
fixture-only isolation check, using an outbound-rejecting handler even when a dummy
credential is supplied. Remove credential discovery and tracked report writes from
this test. Keep the test class and verify every existing founder seed is exercised;
do not replace unsupported coverage assertions with new catalog claims. Ordinary
tests must never start recording. The separate recorder has no test entry point.

## Harness and metrics

Accept explicitly synthetic candidate snapshots and an injected pure ranking
callback. Require the profile lock before invoking that callback. Reject real or
mixed provenance in Phase 2. Unknown fixture profiles are coverage gaps. Do not
implement/adopt production formulas A–D in this phase. Validate returned scores,
all normalized contributions and separately named components; canonical output
supports future pinned-runtime determinism checks. Known/disliked track exclusion,
ordinal ties and unpadded top three/five are harness contracts.

Implement coverage, concentration, overlap/stability, novelty, popularity missingness
and collapse diagnostics, per-seed yield, explanation reconciliation and blind
rating arithmetic using fabricated inputs with analytically known expectations.
Use denominators, empty-list rules and tolerances from the approved contracts.
Do not infer musical quality or choose a formula. Every mechanics report is labelled
`Mechanics only, no catalog conclusions.`

## Recorder and storage

Require a concrete approved manifest and hash, current verification references,
profile/protocol hashes, pinned code revision, exact seed/endpoint budgets,
retention, ignored/untracked containment and complete shared inventory. Phase 5
additionally requires the Last.fm reply/explicit waiver reference. Draft or changed
manifests fail before credential lookup or HTTP. No automatic retries/resume.

Acquire one writer lease; reserve maximum response plus metadata before every
call. Inventory includes recordings, cache, derived data, reports, copies, backups
and partial files. Unknown, stale, externally changed inventory, exact threshold,
overflow, oversized responses and cancellation stop acquisition. Shared usage
must remain strictly below 80,000,000 bytes; per-run limits apply additionally.
Request identity encoding and reject compressed responses; retain sanitized provenance, timestamps,
hashes and ledger, never credential-bearing URLs. Record gaps without parsing
truncated content. Endpoint expansion is canonical, depth one and budget bounded.
Held-out recording content stays sealed; no statistical output before freeze.

Verification: fake transports/inventories only; independent expected metric values;
lock mutation and split leakage checks; path/overwrite/manifest rejection; exact
storage boundaries, cancellation and concurrent writer checks; safe offline .NET
suite. Live recorder execution and real scoring remain Phase 4/5 approvals.

Popularity diagnostics use empirical midrank percentiles, including ties, with
percentile at least 0.90 defining the highest decile. This mechanics convention must
be included in the future frozen protocol before real scoring; it does not amend
any acceptance threshold. Recorder JSON adds concrete `profile_lock_path` and
`protocol_path` fields, both absolute ignored/untracked files under `recordings/`.
The approval digest is SHA-256 of sorted compact UTF-8 JSON with the nested
`approval.approved_manifest_sha256` set to null, avoiding a self-referential hash.

## Authoritative provider checks (2026-10-07)

- [Terms](https://www.last.fm/api/tos): 100 MB aggregate reasonable-use cap,
  header-based caching, server-enforced limits without a universal numeric allowance,
  and written approval before public pages using API data.
- [artist.getSimilar](https://www.last.fm/api/show/artist.getSimilar): artist, limit,
  API key, optional autocorrect; documented match range 0–1.
- [artist.getTopTracks](https://www.last.fm/api/show/artist.getTopTracks): artist,
  limit/page/API key; popularity order, track-scoped listeners in the example.
- [artist.getTopTags](https://www.last.fm/api/show/artist.getTopTags): artist/API key,
  optional autocorrect; no documented limit parameter or guaranteed numeric tag
  count in its sample. The recorder preserves raw data and does not invent either.

No Last.fm API call was made. One request/second remains conservative proposed
pacing, not a verified allowance. Provider authorization and concrete manifests
remain separate gates. Python 3.14.7 and the installed .NET 10 runtime were used;
no package or framework versions were changed.
