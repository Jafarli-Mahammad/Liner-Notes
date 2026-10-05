# Renewal contracts and pending proposals

The 2026-10-06 user-supplied renewal plan takes precedence over older specifications.
Only Phase 1 is authorized. This file preserves future constraints; it does not authorize execution.
See [PROGRESS](../../PROGRESS.md) for current decisions and gates.

## Specification reconciliation

The six directories in PROGRESS replace older five-layer examples: EF/Identity/repositories belong
in DataAccess; provider clients/cache/email in Infrastructure; the host is Presentation, not Web/Api.
.NET 10 is targeted; EF Core 10 remains intended, while checked-in packages are 9.x.
Infrastructure specification §§1, 5.4, 6.2, 7.1, 8.2 still identify integration/import requirements,
but ListenBrainz and both providers' username/history imports are deferred. Spotify APIs are out of V1.
§13 deployment and §15 hardening remain requirements subject to approval; CI is not explicitly specified.
The prior soft-delete design is insufficient for physical account deletion.
User exports must cover taste/account data, not disclose hashes, refresh secrets or protection keys;
the exact versioned export boundary must be reviewed before changing that API.

## Phases 2–6: not approved

2. Ship synthetic fixtures and a recording script. A single real run needs a separate approved
manifest: endpoints, seeds, budget, pacing, size estimate and local gitignored output path.
Sequential requests, no bursts, provisional one request/second, stricter server limits respected;
stop on rate-limit responses. Never log secrets or credential-bearing URLs.
Freeze development data and 16 held-out profiles (two per benchmark genre) before tuning;
held-out seeds must be disjoint from development seeds. Report candidate overlap.
Neither held-out outcomes nor corpus statistics may inform tuning.

3. Application orchestrates signals → materialization → discovery → hydration → pure ranking →
persistence. Infrastructure owns provider details. Preserve authentic evidence, missingness,
provenance and attribution URLs; remove fabricated metadata. Schema/interfaces require review.

3A. Exercise Worker → offline generation → persisted digest → HTML/plaintext → local sink,
including explanations, unsubscribe, short/empty lists, retries and recovery. Then reassess
end-to-end behavior, security, evidence, storage, terms, suitability of artist tags for track discovery,
and remaining cost/complexity. Explicitly decide whether to proceed, narrow or revise before Phase 4.

4. Compare identical pools at three and five picks: popularity distribution, diversity, novelty,
coverage, stability, determinism and explanation integrity. Tune only on real development recordings;
freeze configuration before held-out scoring. Changes prompted by held-out results retire that set
to diagnostics and require a fresh confirmation set. Without recordings: **Mechanics only, no catalog conclusions.**
No weights, stoplists, IDF statistics or scoring alternatives may be promoted on synthetic data alone.

5. Complete approved export, deletion, scheduling, unsubscribe, recovery and truthful explanations.
Database atomicity and email failure windows need separate verification.

6. Repair approved Docker/Compose and CI; verify hosting/email choices. No purchases, provisioning,
public deployment or external sending without approval.

## Track-selection proposals: adoption requires approval

Weekly tie-break: SHA256(Encode(version, userId, ISOYear, ISOWeek, canonicalTrackKey)), using
versioned length-prefixed UTF-8, canonical GUID format and supplied ISO week. Sort hash bytes
ascending, canonical key only on collision. Persist policy version and digest week for replay.
No clock, random values or GetHashCode. It avoids permanent alphabetical preference, not proof of fit.

Alternative: highest upstream-ranked eligible track per artist, skipping known/disliked tracks.
[artist.getTopTracks](https://www.last.fm/api/show/artist.getTopTracks) documents popularity order
and rank (checked 2026-10-06); other providers are not verified. Preserve order/provenance.
This adds within-artist popularity preference and cannot silently replace popularity-neutral policy.

## Scoring proposals: adoption requires approval

A: ordinary cosine + configurable novelty. B: best-seed cosine + novelty, preserving winning seed.
C: B + authentic supplied match evidence. D: separately evaluate IDF cosine before combinations.
For C, deduplicate by provider/seed/method; M(c) = max valid finite supplied match over paths.
Missing stays missing and gives no bonus. Persist `max-valid-match-v1`, evidence and winning path;
ties by canonical seed. Do not pool incomparable provider scores; blending remains deferred.
For D, each distinct artist counts once; d(t) = 1 + log((A+1)/(df(t)+1)); unseen vocabulary uses 1.
Apply sqrt(d) to both vectors. Per-tag contribution is
w(t)*d(t)*c(t)*u(t)/(sqrt(sum d*c²)*sqrt(sum d*u²)).
Report artist/tag counts, genre balance, missing labels and overlap. Development-only balanced
subsampling and leave-one-genre-out sensitivity must report metric/ranking changes.
Prefer aggregate A/df counts, version/hash and composition summaries over duplicated corpus;
aggregates still count as derived data. Retain actual factors in per-pick explanations.

## Storage and retention proposals

Shared inventory covers recordings, HTTP cache, derived database data, reports/copies/backups.
Count uncompressed bytes conservatively, category totals, headroom and inventory age.
Acquisition fails closed at projected 80,000,000 bytes or unknown accounting; bound responses.
At threshold, stop acquisition/enrichment and alert. Continue account, feedback, unsubscribe,
delete and delivery-state writes. Pause new derived recommendation snapshots if headroom is
insufficient/unknown; serve existing snapshots. Evict only under approved expiry policy;
never silently delete recommendation history. Count derived feedback without duplicating metadata.
Initially only Worker/recording execution acquires data, never concurrently; reconcile inventory
and conservative headroom before each batch. Reassess cross-process coordination at 3A.
Measure complete breakdown and provenance UTF-8 and PostgreSQL pg_column_size: median, p95, max,
total. Current sizes are not verified. Estimate at up to 260 picks/user-year plus all categories.
Retention proposal: response headers plus 24h cache maximum, 30d recordings (explicit extensions),
12mo recommendations/explanations, active seeds/feedback/familiarity until changed/deleted,
30d maximum backups included in deletion disclosures. None adopted. Adjust enrollment/retention
to measured capacity; expired recordings require another approved run for replay.

## Attribution and enquiry: draft only

[Last.fm terms](https://www.last.fm/api/tos) checked 2026-10-06: storage cap/caching (§4.3.4),
rate limits without a universal numeric quota (§4.4), attribution/public-page approval (§2.7).
UI needs artist/track links and prescribed button; local button hosting permission is unresolved.
Text-only email attribution acceptability, Azerbaijan/EEA conditions, redistribution and benchmark
research permission remain unresolved. No automatic email third-party requests, including hosted images.

Draft subject: Liner Notes — API use, benchmark publication, attribution, and page approval

Hello Last.fm team,

I am developing Liner Notes, an open-source discovery project, from Azerbaijan. It uses Last.fm
candidate/tag data with transparent deterministic re-ranking and does not own the similarity data.
Please clarify territorial consent/hosting/transfers under §§2.3/2.5; whether recorded responses
could ever be redistributed (current policy: local/uncommitted); written public-page approval and
local attribution-button hosting under §2.7; text-only attribution and ordinary artist/track links
in HTML/plaintext emails without automatic third-party requests; and whether publishing benchmark
methodology/aggregate results requires research permission without publishing responses.
We plan shared inventory, acquisition stopping at 80 MB, retention and degradation controls below
the stated 100 MB cap. Repository: **URL must be confirmed by the developer before sending**.
Thank you.
