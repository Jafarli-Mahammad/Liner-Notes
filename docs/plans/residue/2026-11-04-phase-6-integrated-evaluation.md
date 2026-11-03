# Recommendation quality renewal: Phase 6 integrated evaluation

Date: 2026-11-04. Implemented and locally verified; developer confirmation pending.
Subjective quality remains unverified. No scoring or selection settings changed.

## Protocol and evidence boundaries

The developer authorized Phase 6 after Phase 5 (`d49c192`). The actual Phase 1
Domain implementation (`5a91411`) was built in an isolated worktree, with the
small [baseline adapter](../../../scripts/checks/residue_phase6_baseline.cs).
The [integrated evaluator](../../../scripts/checks/residue_phase6_evaluate.cs)
compares both rankers on each identical acquired/hydrated pool, using the same
taste vector, familiarity, numerical weights, batch identity and answers.
This isolates selection changes. Both rankers receive current hydration and
materialized inputs; this does not recreate the entire historical acquisition,
favorite or feedback pipeline. Those paths have separate contract, persistence
and controlled-browser verification.

The [manifest](2026-11-04-phase-6-evaluation-manifest.json) froze four development
and four held-out public examples before the single live evaluation. The examples
are evaluation inputs, never product defaults or founder tuning. The settings
are the existing Phase 5 factory policy: tag/novelty weights 1/0.2, five requested
picks, minimum affinity 0.1, individual/goal/exploration/vocal/history weights
0.35/0.25/0.35/0.15/0.15, two individual shared tags, three broad-tag slots,
and acquisition limits 3/6/10. Production maximum upstream requests remains 80;
the evaluation uses a stricter eight-dispatch allowance per preparation and an
80-dispatch global cap. No optimization or held-out-driven adjustment occurred.

Manifest SHA-256: `a9bffd295f7915a093dfd408ecb43a2eaed345c84672ac2e539d370770ae580e`.
Runtime serialized settings SHA-256: `55b14375e815443157dc4260854c4430db2255a523d49c00e3f395fd7de698a9`.
The pre-run freeze also records a Python canonical-JSON settings hash, which has
a different serialization and is not compared as if it were the runtime hash.
The manifest bytes and runtime settings were checked unchanged after execution.

The original evidence remains intact: 138 prior evidence/report files were
SHA-256 checked before and after. Only the review sheet's completion count was
read: 0 of 35 items rated. Founder notes and labels were not used. The personal
beta database was not started, mutated or evaluated in this phase. No emails
were sent, no accounts recruited, and no deployment or dependency update occurred.

## Same-pool live result

Eight profiles evaluated successfully using real public ListenBrainz responses.
The combined evaluation and acquisition comparison dispatched **54 HTTP requests**,
all returning HTTP 200, under the frozen cap of 80. Raw upstream bodies were not
persisted; private output retains response references, hashes, gaps, normalized
selection inputs and decisions. Candidate counts below are unique within each
profile, not deduplicated across profiles.

| Profile | Raw/hydrated | Dispatches | Baseline picks | Integrated picks | Common picks |
| --- | ---: | ---: | ---: | ---: | ---: |
| development-electronic | 12/12 | 6 | 5 | 5 | 5 |
| development-jazz | 3/3 | 2 | 3 | 2 | 2 |
| development-country | 6/6 | 3 | 5 | 5 | 5 |
| development-classical | 10/10 | 3 | 5 | 5 | 0 |
| heldout-pop | 20/20 | 6 | 5 | 5 | 3 |
| heldout-hiphop | 12/12 | 6 | 5 | 5 | 4 |
| heldout-chanson | 2/2 | 5 | 2 | 2 | 2 |
| heldout-trip-hop | 20/18 | 6 | 5 | 5 | 4 |

Across profiles, 85 raw candidates produced 83 hydrated candidates: 56 with
recording tags and 27 with artist tags. All 83 paired numerical final scores,
raw tag similarities and novelty values matched exactly. Integrated selection
replayed exactly for all eight pools. The baseline showed 35/40 requested slots;
the integrated policy showed 34/40, with 25 common selections. Two integrated
batches contained two picks each. The full considered-pool decisions record
34 selections, 12 insufficient connections, 12 artist-diversity exclusions and
25 candidates below the requested limit.

Coverage gaps included two missing valid artist-tag votes, one missing direct
recording-tag response, one route with no supported tracks and one missing
recording metadata item. These are coverage limits; HTTP success does not mean
complete evidence. Fewer picks, different picks, or more recording-tag coverage
do not establish better music or useful discoveries.

## Acquisition under matched allowances

One development and one held-out profile were declared in advance. Each arm
used a fresh cold client/cache and the same eight-dispatch allowance, current
provider adapter and seed inputs. Development runs baseline-first; held-out
runs integrated-first. These are bounded acquisition-limit comparisons, not a
reconstruction of the old provider adapter. Requests were paced by the existing
shared MetaBrainz gate; no retries were added by the evaluation harness.

| Profile | Acquisition arm (limit) | Dispatches/allowance | Raw/hydrated | Seconds |
| --- | --- | ---: | ---: | ---: |
| development-jazz | legacy-fixed-limit (10) | 3/8 | 9/7 | 8.22 |
| development-jazz | coordinated-limit (3) | 2/8 | 3/3 | 7.30 |
| heldout-pop | coordinated-limit (10) | 6/8 | 19/19 | 6.71 |
| heldout-pop | legacy-fixed-limit (10) | 6/8 | 20/19 | 6.37 |

The narrower jazz arm reduced dispatches and available candidates together.
Both pop arms used limit 10 yet returned different raw candidates. Upstream
sampling and sequential timing are confounders; no causal coverage or latency
improvement is claimed from these four observations. Equal allowances do not
mean equal actual costs. Factory request limits were not relaxed to fill slots.

Provider references checked for this run: [ListenBrainz API usage and limits](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html),
[recording metadata](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html),
[similar recordings](https://labs.api.listenbrainz.org/similar-recordings), and
[MusicBrainz data licensing](https://musicbrainz.org/doc/About/Data_License).
No new endpoint, redistribution policy or commercial-use claim was introduced.

## Controlled beta workflows and privacy

**61 assertions passed through Playwright MCP**, using installed Chromium,
three isolated browser contexts and a disposable PostgreSQL database. The host
was explicitly Synthetic, with provider fixtures and email delivery disabled.
These are mechanics checks, not live music-quality findings.

- Canonical catalog search, favorite exclusion, onboarding, asynchronous job
  polling and an honest four-pick shortfall; a separate two-pick account and a
  zero-pick unconnected guest.
- Guest history survives registration and claim; the guest cookie is revoked.
  Account identity takes precedence when an unrelated controlled guest cookie
  is present. Foreign batches return 404 and the other account's exported
  discovery data stays unchanged.
- Like, note-only edit, reversal and already-known feedback. Musical revisions
  change on musical edits and remain unchanged on note-only edits.
- Repeated generation suppresses history; removing a favorite makes that one
  recording eligible while history remains excluded. The five generation jobs
  displayed 4, 2, 0, 0 and 1 picks, completing in approximately 1.2–2.3 seconds
  including browser polling. This excludes live upstream latency.
- A 390px reduced-motion view displays the stored shortfall without overflow.
  Account JSON exports include stored selection evidence.
- Restarting only the disposable host with the existing database and keys
  preserves protected sessions, taste revisions, batches and account exports.
  Both accounts then return 204 on deletion and 401 on authenticated lookup;
  the guest session is cleared.

A first QA deletion reported incomplete archive cleanup because the temporary
copies directory had shared permissions. Securing the disposable directories
with mode 0700 and rerunning produced 204, rather than weakening the cleanup
assertion. No product workaround or privacy guarantee was inferred from it.
Browser credentials stayed inside browser memory/cookies, never in the saved
MCP result. The local host and its disposable database were removed afterward.

## Compatibility, migrations and rollback

The full solution passed **980 tests, zero failures and zero skips**, including
196 PostgreSQL DataAccess tests, 176 Domain, 174 Application, 142 Presentation
and 292 Infrastructure tests. Existing migration/populated-legacy, storage,
account erasure, cache ownership, receipt codecs, feedback and Worker suites ran.
Two old Worker lifecycle fixtures used rock/darkwave tastes while their provider
fixture now supplies electronic recording tags. Updating those seed tags restored
the intended relevant-pick setup; their non-empty, provenance, export and erasure
assertions remain in place. No production logic or migration changed.

The original 65 Phase 1 receipts and ten controlled receipts replay numerically
with zero error. The post-maintenance 55-receipt snapshot also replays with zero
error. Five fresh account-export receipts replay both numerical scores and full
selection exactly. The original fixed sparse-pool protocol also passes: 16
profile/acquisition cases, 80 paired numerical comparisons, 16 supported shown
picks rather than the former 80 padded picks. This is correctness evidence.

Existing plain/gzip readers and the current Brotli reader remain covered, as do
the 100KB stored/4MB expanded bounds and the combined 1,000-favorite/400-candidate
receipt case. No new stored format is introduced here. Phase 5's rollback limit
still applies: an old whole binary cannot read new Brotli receipts. Retain the
current reader and roll back selection policy only if needed; never downgrade
the database or discard receipts to make an old binary start. Pure old-policy
selection was executed against the captured pools; no destructive database
rollback or public deployment rollback was attempted.

## Performance and calibration decision

The [local performance probe](../../../scripts/checks/residue_phase6_performance.cs)
uses the actual old Domain binary and current Domain with the same prepared
synthetic candidates, empty evidence, numerical settings, five warmups and 30
measurements. Both measured paths execute scoring plus final selection. It
excludes input normalization, favorite-affinity preparation, codecs, database,
network and job orchestration. The new selector also constructs its decision
receipt and counterfactual ranks, which the old selector does not provide.

| Candidates | Baseline median/p95 ms | Integrated median/p95 ms | Baseline/integrated allocated bytes per run |
| --- | ---: | ---: | ---: |
| 10 | 0.0648/0.0790 | 0.0937/0.1226 | 37971/57675 |
| 100 | 0.6435/0.7658 | 0.7167/0.8142 | 338051/424931 |
| 400 | 2.0838/4.6980 | 4.3486/8.0070 | 1338851/1636003 |

The 400-candidate pure operation is slower and allocates more locally. This is
not an end-to-end capacity finding; it does not justify speculative optimization
or changing relevance settings. The live evaluator also captures per-pool
acquisition times (3.85–15.80 seconds), dispatch timings and experimental ranker
timings. Its ranker timings have different evidence-construction work between
arms and are excluded from comparative performance conclusions. The separate
matched empty-evidence probe above supplies the bounded comparison.

The React production build passed. No subjective listening comparison was run,
since there are no completed ratings. There is no evidence here to calibrate
weights toward musical enjoyment, so the frozen settings are retained.

## Reproduction and remaining limits

Private artifacts are under ignored `diagnostics/results/residue-phase6-20261104/`
with no raw provider bodies. Key files: `freeze.json`, `live-evaluation.json`,
`fixed-pool-comparison.json`, `performance.json`, historical and final controlled
replay JSON, `solution-tests-final.log`, the two parsed browser results and
`final-integrity.json`. Public tables above are aggregate evidence; private
candidate lists and account-export receipts are not committed.

Build the baseline adapter against an isolated `5a91411` Domain project in a
scratch console project targeting net10.0, with `PHASE6_BASELINE` defined. Copy
`residue_phase6_baseline.cs` as its Program.cs with the leading `#:` file-app
references removed; ProjectReference the isolated `src/Domain/Domain.csproj`.
Do not compile it against the current selector or reimplement the old ranking
formula. Then run:

```sh
dotnet run --file scripts/checks/residue_phase6_evaluate.cs -- docs/plans/residue/2026-11-04-phase-6-evaluation-manifest.json /absolute/path/to/Runner.dll --live
dotnet run --file scripts/checks/residue_phase6_performance.cs -- /absolute/path/to/Runner.dll
dotnet test 'Liner Notes.sln' --no-restore -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false
```

The full suite requires the existing local PostgreSQL test connection via
`LINER_PHASE1_POSTGRES`; do not put its value in reports or command history.
The two `residue_phase6_browser*.cjs` snippets run sequentially through the
Playwright MCP code tool, with a restart of only the controlled host between
them. They assume the disposable fixture host at localhost:5100 and the current
React build. Never run them against a personal or deployed account.

Live results vary between executions. Held-out inputs are now observed and must
not silently become a reusable tuning set. Any future coefficient experiment
needs a new frozen evaluation split and listening evidence. Actual enjoyment,
long-running production capacity, representative multi-user live behavior,
external email delivery, cross-browser coverage and public release rollback
remain unverified. Phase 6's engineering evaluation is complete locally;
developer acceptance and subjective quality are separate open gates.
