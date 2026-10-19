# Optional taste onboarding: implementation evidence

The developer authorized implementation on 2026-10-19, starting from local commit
`63a3789`. This supersedes the design's earlier documentation-only gates. The
[execution plan](../superpowers/plans/2026-10-19-optional-taste-onboarding.md)
records the concrete policy choices. Developer confirmation, live coverage,
recommendation quality and deployment readiness remain unverified.

## Implemented behavior

Guests and accounts use the existing listener profile and revision. Setup saves
independently of generation, including an empty setup. All three optional questions
appear together without preselected answers, followed by tags, typed favorites and
review. Back, Skip and Edit retain accepted data. The client prevents overlapping
step saves and fences late responses after navigation or an account change.
Unsaved authentication-transition drafts require an explicit review before use.

Skipped input opens Recent recommendations; usable examples put personal results
first. Soup means all styles are welcome, coexists with examples and admits a
rotating genre acquisition window. Questions alone cannot establish musical
closeness. Existing accounts are not automatically sent through setup again.

Questionnaire codes and step are in private `ListenerProfiles.OnboardingJson`.
They participate in revision fencing, export, attachment, import and profile
deletion/expiry. Import previews differing question answers, keeps answered account
preferences and fills unanswered ones. Broad taste is merged explicitly. Existing
name seeds and previously saved custom tags remain editable by their owner; new
unknown tag searches never create arbitrary provider seeds. Old v1 request hashes
remain replayable; legacy-seed requests use a separate v2 intent shape.

The original local vocabulary contains genres/styles, moods and sound descriptions,
canonical IDs and aliases. It is maintained as project source under the repository
license; no external taxonomy or held-out evaluation data was copied. It is not
exhaustive and does not imply provider coverage. Maintain canonical IDs and aliases
when extending it; removals need a compatibility/data migration decision.

## Bounded scoring and evidence

Saved tags have no product count limit. Every selection remains visible and
exportable. Existing request-body and managed-storage admission still apply;
rejection preserves already accepted state. Generation rotates bounded acquisition
windows by completed batch count rather than always taking the same prefix.
Entity preparation, candidate counts, public caches and upstream request budgets
remain bounded independently of saved selections.

`canonical-entity-max-v2` uses the whole tag profile for scoring. For groups larger
than five tags, the group's per-tag weight is divided by the square root of its
size, so merely selecting many tags does not add unbounded group influence.
Historical v1 materialization snapshots remain replayable. A large score snapshot
stores matched taste weights plus the remaining squared magnitude, full-input
hash/count and acquisition window; replay retains the original cosine denominator
without copying every private signal into every pick. This is a numerical replay
projection, not a recoverable archive of every old unmatched selection.

The versioned selection policy prioritizes goal, then exploration, then supported
vocal preference, then baseline score and ordinal identity. Goal choices prioritize
unfamiliar artists, stronger supported overlap, or lower overlap among supported
candidates; mixture retains baseline ordering. Close/surprise use supported overlap
ordering. Vocal preference uses explicit recording-level tags only; missing,
conflicting or artist-scoped evidence remains unknown. Both/no preference are
neutral. Stored answers, policy version and plain-language reasons explain the
selection. The same policy applies to onsite and weekly preparation. Popularity
remains neutral; no model or external similarity algorithm was built here.

Track favorites preserve recording identity, disambiguation, ordered credits and
available release hints in an immutable private selection snapshot. A release
track position is never used as recording identity. Favorite recordings are seeds
and are suppressed from their owner's results. Direct recording similarity and
tags use capability interfaces; missing capability is an explicit coverage gap.

Album similarity uses credited-artist discovery regardless of whether album tags
exist. Stored evidence retains original album and queried artist identities, the
provider response path and configured reduced influence. The existing
`DiscoveryTaste:AlbumArtistFallbackInfluence` default is 0.35; validation requires
an influence strictly between zero and one. Multiple credits share that configured
budget. Direct paths take precedence over duplicate fallback paths. The reduced
similarity component, formula version/hash and disclosure survive replay; novelty
retains its existing configured treatment. Album-scoped tags remain independent.

## Public feed boundary

Recent is a separate projection of eligible, currently published shared catalog
music. It stores only public recording ID, title, credits, origin, attribution,
metadata expiry and its own feed sequence. It has no recipient, recommendation,
batch, job or source-row foreign key, no recipient counter or personal explanation,
and no recommendation event timestamp. The API returns no metadata-expiry time.

Completion publishes catalog metadata in the same transaction as successful picks
and job completion. Rollback leaves no feed item; retry cannot publish duplicates.
Repeated music does not bump position. Reads use local public data only, recheck
eligibility and never acquire upstream data. Expired metadata is hidden and
reclaimed during publication; at most 1,000 entries remain. Empty and synthetic
states are explicit. Deleting a private profile leaves the independent public
music record intact. Music overlap can still permit inference; this is not a
claim of stronger anonymity.

Both additive migrations check the existing managed-storage ledger before schema
changes and rebase after them. The new table has insert/update/delete/TRUNCATE
accounting hooks. Downgrades refuse to silently discard onboarding, canonical
recording selections, public feed data or release hints. The reviewed privacy
inventory covers every new column.

## Provider verification and limits

Official references checked on 2026-10-19:

- [MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API): recording search,
  identities/credits, meaningful User-Agent and one-request-per-second pacing.
  [Data licensing](https://musicbrainz.org/doc/About/Data_License) distinguishes
  core metadata from supplementary tag data; existing acquisition gates remain.
- [ListenBrainz similar recordings](https://labs.api.listenbrainz.org/similar-recordings)
  and its [official implementation](https://github.com/metabrainz/listenbrainz-server/blob/master/listenbrainz/labs_api/labs/api/similar_recordings/base.py)
  establish recording IDs, algorithm selection and returned recording metadata.
  The adapter pins the advertised 180-day session-based variant, bounds returned
  candidates, shares the existing conservative pacing/cache controls and never
  sends authentication tokens to the Labs host.
- The official [recording metadata example](https://github.com/metabrainz/listenbrainz-server/blob/master/listenbrainz/testdata/mb_metadata_cache_example.json)
  establishes distinct recording/artist/release-group tag scopes.
  [ListenBrainz terms](https://listenbrainz.org/terms-of-service/) retain the
  existing usage-review boundary. Broad vocal-tag coverage was not measured.
- [Last.fm direct track similarity](https://www.last.fm/api/show/track.getSimilar)
  exists, but this change does not add that adapter capability. Last.fm stays
  selectable; unsupported canonical/direct recording evidence produces coverage
  gaps, never credited-artist substitution.
  [Existing Last.fm terms](https://www.last.fm/api/tos) and release gates remain.

No live music acquisition, real-data evaluation, provider permission request,
dependency upgrade, remote CI run, deployment or push was performed. All runtime
music used for verification was synthetic. Existing dependency audit warnings
were reported by the build and remain outside this task's remediation scope.

## Verification

The final verification passed on 2026-10-19. Commands use the
pinned SDK, disposable loopback PostgreSQL databases and installed Playwright/
Chromium. No runtime credential appears in committed files or command arguments.
The Playwright MCP's configured Chrome binary was absent; local Chromium was used
for the browser checks. Serena and Codex-Mem were also used for repository context.

- Full .NET suite: **669 passed, zero failed/skipped**, across Domain 148,
  DataAccess 128, Application 137, Presentation 75 and Infrastructure 181.
  This includes the existing Domain benchmark fixtures, historical replay,
  adapter isolation, migrations/accounting, ownership/export, transaction
  rollback, recording snapshot lifecycle and old request-key compatibility.
- Existing guest browser suite: **12 passed**, including stale search responses,
  local and explicit lookup, generation, feedback, account attachment/import,
  cooldown, return visits and account/guest deletion.
- Optional onboarding browser suite: **nine passed**, including skipping every
  input without a generation job, question optionality, aliases without upstream
  typing calls, soup, recording favorites, return visits, recipient-free feed,
  account attachment, mobile layout and a large saved tag profile.
- The large synthetic setup/generation/export journey used **271 selected tags**,
  completed in **4,867 ms**, and its largest exported score breakdown was
  **28,250 bytes**, below the retained 100,000-byte admission limit. This is a
  single local synthetic journey, not live-provider latency or capacity evidence.
- EF reports **no model changes since the last migration**. Changed C# files were
  whitespace-formatted, browser scripts pass syntax checks and `git diff --check`
  passes. PostgreSQL tests exercised both additive migrations and reconciled
  managed accounting after the new writes.
- Raw verification output is disposable under `/tmp/onboarding-*.log` and
  `/tmp/onboarding-browser-results.json`. Browser screenshots are under `/tmp`.
  Reproduction commands are in the existing synthetic runbook and execution plan.
- No provider quality or production capacity conclusion follows from these checks.
