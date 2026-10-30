# Original-account discovery reliability — 2026-10-31

This follow-up supersedes the unresolved original-profile findings in
`DISCOVERY_RUNTIME_INVESTIGATION.md`. Developer confirmation remains pending.

## Confirmed causes

The unchanged revision-21 profile had two independent failure boundaries:

1. **Feedback identity was discarded.** `EffectiveTasteInputs` creates positive
   artist seeds from liked/rated recommendations. These had artist names but no
   canonical identity, despite the recommendation having a recording ID.
   `MaterializeDiscoveryTaste → SharedSeedPreparation → ISeedTagSource.GetAsync`
   therefore called MusicBrainz artist search. The recorded service response was
   HTTP 503 with `{"error":"The MusicBrainz web server is currently busy. Please
   try again later."}`. This was not an empty or malformed artist result.
2. **Complete receipts exceeded the size guard after successful scoring.**
   Operation `49da8dd3-247b-4952-b0c7-64fd9c36299a` first retried a MusicBrainz
   search failure. Its next attempt acquired and validated the evidence, then
   `PrepareDiscoveryBatch` rejected a receipt with `snapshot_limit`, before the
   private batch transaction. A subsequent measured receipt was 105,437 bytes
   against the 100,000-byte limit. None of that failed operation's picks persisted.

The first private probe captured a 503 searching one feedback artist; the actual
operation above captured a 503 searching a different feedback artist. They are
separate requests. Exact URIs, timestamps, response headers and operation/trace
joins are retained privately under `/tmp/liner-original-profile-trace/`; feedback
artist names and account identifiers are not copied into this report.

Requests used `GET https://musicbrainz.org/ws/2/artist/?fmt=json&limit=25&query=...`,
with a URL-encoded quoted `artist:` expression. The captured 503 advertised
`Retry-After: 0` and a positive remaining quota. The application applied its
existing minimum 30-second provider backoff. Shared pacing and retry transitions
were recorded; the provider's response does not identify which internal
capacity/rate-limit check rejected it. There is no evidence of malformed name
encoding. Later paced searches resolved all three named artists with HTTP 200.
MusicBrainz documents both the [search syntax](https://musicbrainz.org/doc/MusicBrainz_API/Search)
and [503 throttling/capacity responses](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting).

The historical operation `32c4d24e-87a5-48ef-8b4b-4401c507786b` retained only the old
generic error. Its exact underlying exception remains unrecoverable; the current
trace establishes the two failures above without inventing its lost history.

## Jakuzi / Lin Pesto comparisons

Eight sequential, real-provider cases used separate disposable profiles except
the explicitly labeled original-account case. The artist-only cases used the
existing canonical selections, with no original-account edits and no mocks.

| Case | Operation | Outcome |
|---|---|---|
| Son Feci Bisiklet | `a872c3c8-387d-4a64-910d-6f43b76e1486` | Completed; 5 picks |
| Jakuzi | `d9922358-7007-4858-b88b-d948b8d4da9e` | Completed; 2 picks |
| Lin Pesto | `f112a70a-0551-4f93-849a-d77f39a69f7a` | Completed; 3 picks |
| Son Feci + Jakuzi | `df17882b-562d-4da9-b48e-c41b50bc7609` | Completed; 5 picks |
| Son Feci + Lin Pesto | `a1cc42d7-2657-417f-829b-54edb40b2d65` | Completed; 5 picks |
| All three | `292c4d0d-e3c7-408b-bc84-b731a582ee39` | Completed; 5 picks |
| Original profile, revision 21 | `49da8dd3-247b-4952-b0c7-64fd9c36299a` | Retried 503; subsequently failed `snapshot_limit`; no batch |
| Jazz/electronic control | `ee0f44b2-b0cf-48aa-93bd-4850c8b314b7` | Completed; 5 picks |

Canonical artist acquisition used ListenBrainz `metadata/artist`, `lb-radio/artist`
and recording/artist hydration endpoints. Tag controls used `lb-radio/tags`.
Requests retained their existing candidate limits, popularity range, shared
pacing and evidence validation. The two smaller single-artist results reflect
limited supported candidates; no source failure was converted into a partial
successful acquisition.

Jakuzi and Lin Pesto individually and together returned usable data. The original
account's earlier successful revision-17 receipt also already contained all three
artists. The demonstrated difference is the complete original input, including
positive feedback and its larger evidence receipts. The feedback-free control
avoided the extra artist-name searches and remained below the receipt limit.
The temporal clue does not establish that adding either artist broke discovery.

## Changes

- **Preserve validated identity:** feedback seeds carry their known recording ID
  through the provider-neutral seed request. The ListenBrainz adapter accepts a
  canonical artist only when fresh recording metadata has one unique credited
  artist and an exact name match. Fresh artist tags and recording provenance are
  retained, and canonical acquisition reuses that identity. Missing, mismatched
  or ambiguous credits retain the existing strict search fallback; temporary
  failures remain retryable. Shared seed storage is versioned to v3 so identity
  survives replay. No positive feedback is removed or reweighted.
- **Lossless complete receipts:** `ScoreBreakdownJson` preserves existing JSON for
  receipts up to 100,000 bytes. Larger receipts use a versioned gzip envelope,
  SHA-256 validation and a 4 MiB expansion bound. The persisted record must still
  fit 100,000 bytes. EF reads restore the complete original score object;
  explanations, exports and replay receive all original fields. Discovery and
  weekly admission measure that same stored representation. No evidence trimming,
  scoring change, quota bypass, schema migration or dependency was introduced.
- **Useful diagnosis:** worker logs and .NET activities identify the exact
  operation and attempt. The UI distinguishes receipt-limit failures from a
  provider outage. Exact private HTTP captures came from a launch-only .NET
  diagnostic observer, not global logging of tastes or authorization headers.

Changed areas are the Application seed contracts/materializer/preparation and
receipt codec, the ListenBrainz adapter/shared seed codec, the EF score converter,
the discovery worker diagnostics and one frontend error message. Domain scoring
code is unchanged.

## Original-account verification

A backend diagnostic first completed operation
`5c248d12-659a-41e2-a41e-3b1cc6f74b90` into batch
`9e45d076-8a1c-4952-be6e-bc9001f37426`, with five persisted picks. All five full
receipts replayed exactly; one 101,948-byte receipt stored losslessly in 19,146
bytes. This diagnostic was not treated as a browser click.

The actual signed-in Playwright MCP browser then recorded its displayed batch and
track IDs, clicked **Find my next favorites**, and submitted revision **21**:

- POST 202 operation: `8d4d9dcb-4bbd-42c4-9db4-5caf9f13e5e2`.
- Completed result: `a9e53adc-17c1-4112-9040-ea02b3970050`.
- Worker: prepared handoff followed by acquisition attempt 1; 95 correlated
  upstream responses were HTTP 200, with no MusicBrainz name searches.
- PostgreSQL: five new recommendation rows; two ordinary and three packed full
  receipts. The frontend fetched this exact batch and replaced all five titles.
- The original 17 persisted taste signals remain byte-for-byte unchanged, and
  profile revision remains 21. Existing batches and feedback were not rewritten.

The live requests used the existing providers and real data. No browser routes
or provider responses were mocked for this verification.

The local host was then stopped and restarted, and the browser performed a full
page reload. After normal re-authentication (the UI keeps its login token in
memory), it read **the same batch and five track IDs**, with no new submission.
Four covers decoded successfully at widths 500, 500, 500 and 498 pixels. The
Stockholm remix correctly used the unavailable fallback: Cover Art Archive
returned 404 for both its release and release-group cover. Pending states used
the existing retry schedule while fresh public metadata was revalidated. The
unavailable card's HEAD count did not increase when scrolled away and back.

## Tests and relevant limits

Passed: 154 Application, 152 Domain, 271 Infrastructure, 171 PostgreSQL DataAccess
checks, 11 targeted Presentation checks and five Playwright recovery regressions.
The identity regression and oversized-receipt regression failed before their
respective fixes. Tests cover missing/ambiguous identities, transient errors,
shared backoff, no partial acquisition, retry exhaustion, lease/age expiry,
complete receipt round trips, future fields, corruption, expansion and stored
size bounds, PostgreSQL persistence and deterministic replay. The frozen
eight-genre score/output contract remains unchanged.

Genuine provider outages can still exhaust the bounded retry budget; complete
source validation is deliberately retained. Receipts exceeding either storage
bound fail clearly without saving a partial batch. All readers, including the
weekly Worker, must use the updated codec before reading new packed receipts;
rolling back to an older reader requires decoding those envelopes first. Existing
JSON receipts remain readable without rewriting them. No deployment or push was
performed.
