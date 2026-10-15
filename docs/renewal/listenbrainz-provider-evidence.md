# ListenBrainz provider implementation evidence

## Provider resolution

2026-10-15: a scoped resolver binds discovery, candidate hydration and seed tags
as one provider bundle. Selection is recorded in generation configuration;
unknown identifiers fail closed. Last.fm remains the default during adapter
development. No provider UI or account linking has been added.

Verification: targeted Infrastructure provider/storage checks passed 10/10,
zero skipped. The sandbox cannot open the test-runner socket; the same command
passed with the required sandbox permission. Live calls were not involved.

## Transport and replay contracts

Official ListenBrainz API policy, metadata/core documentation, and
`metabrainz/listenbrainz-server` API source were checked on 2026-10-15. Artist
radio returns an artist-ID-keyed object of recording lists; tag radio returns a
recording list. Artist metadata is available at `/1/metadata/artist/` with
`artist_mbids` and `inc=tag`, so direct MusicBrainz calls need only resolve names.
A single anonymous public recording-metadata request confirmed `recording.name`
and artist credits. No account/history endpoint was requested; this establishes
response shape only, not coverage or recommendation quality.

Implemented fixed-host public GET requests, optional ListenBrainz-only token
headers, bounded parsing/caching, immutable hash-checked replay, and a shared
filesystem pacing gate. Usage approval is explicit; no token or failure can
enable fixtures or live mode automatically. Deployed hosts sharing an IP must
share the pacing directory, including any other MetaBrainz consumers.

Targeted transport/replay checks: 8 passed, zero skipped. Existing dependency
pruning warnings remain; no dependency changes were made.

Sources: [ListenBrainz API policy](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html),
[official endpoint implementation](https://github.com/metabrainz/listenbrainz-server/blob/master/listenbrainz/webserver/views/api.py),
[official metadata implementation](https://github.com/metabrainz/listenbrainz-server/blob/master/listenbrainz/webserver/views/metadata_api.py),
[MusicBrainz licenses](https://musicbrainz.org/doc/About/Data_License),
[field classification](https://musicbrainz.org/doc/MusicBrainz_Database).
Tags are supplementary MusicBrainz data; their attribution/noncommercial/sharealike
obligations still need deployment review. No license clearance is claimed.

## Integrated implementation and default (2026-10-16)

- ListenBrainz is the default in generation configuration, web settings and
  local Compose. Last.fm remains an explicit bundle/configuration option; its
  parser, fixtures, clients and legacy replay tests are retained.
- Artist names resolve conservatively through MusicBrainz; discovery uses
  ListenBrainz artist/tag radio with a full popularity range. The chosen radio
  mode is `medium`, a versioned acquisition policy, not a scorer preference.
  Upstream sampling remains upstream behavior; no unbiased population is claimed.
- Seed/candidate vectors use artist-scoped tag votes. Candidate artist metadata
  is batched; repeated requests share the generation-scope cache. Recording,
  release and artist tag scopes are not silently mixed. A matched enclosing
  artist identity can scope nested tags without redundant inner IDs; conflicting
  supplied IDs are rejected. Unsupported tagless candidates become gaps.
- Snapshots retain name-resolution and metadata references, tag entity identity,
  the selected provider and normalization policy. The scorer's arithmetic and
  weights are unchanged; old snapshot versions remain replayable.
- Version-2 ListenBrainz local-use manifests reuse existing approval, hash,
  expiry, path, size and credential checks. Version-1 Last.fm use is preserved.
- Live digest generation requires separate origin, mode, enablement and usage
  approval. The scoped client defaults to an 80-request network budget. Failures
  and empty supported coverage stop before persistence; no automatic provider
  fallback occurs. Scheduled QA remains Synthetic.
- Email credits derive from stored evidence, with MusicBrainz tag-data licensing
  identified. Historical Last.fm digests are not relabeled. Runtime credentials
  are kept in private configuration and never copied into evidence or exports.

Verification with pinned SDK 10.0.401:

| Suite | Passed | Failed/skipped |
|---|---:|---:|
| Domain, including scorer/benchmark checks | 117 | 0 |
| Application | 115 | 0 |
| DataAccess against disposable PostgreSQL | 88 | 0 |
| Infrastructure | 140 | 0 |
| Presentation | 56 | 0 |
| Total | 516 | 0 |

Packaged verification: all nine Compose lifecycle checks passed after rebuilding
the images, covering migrations, synthetic generation/capture/export, shared
private storage, restart, scheduling, unsubscribe, deletion/non-root execution,
and rejection of unapproved live/Production Worker operation. The credential
defaults check, local documentation links and private-token exclusion check also
passed. The supplied token was saved to the shared Development User Secrets
store; existing keys were preserved and runtime live mode was left disabled.

The first full run exposed legacy recorded tests relying on the old default;
they now explicitly select Last.fm. The repeated Worker/PostgreSQL lifecycle
test exercises the ListenBrainz default and checks stored provider/tag identity,
encrypted capture, restart, export and deletion. Unsupported live CLI origins
still fail closed unless explicitly enabled.

Two bounded public-catalog smokes used the private supplied token with a maximum
of 12 requests each. No account-history endpoint was requested and no raw
response recording was saved. The first, Jakuzi/rock, encountered a transport
gap and unsupported tags. After correcting redundant inner-ID handling, the
Jakuzi/synthpop check returned two artist candidates, two tag candidates, four
hydrated candidates and six seed tags with no gaps. This establishes a small
technical integration sample, not broad catalog coverage or recommendation fit.
See the reproducible [smoke command](listenbrainz-provider-runbook.md).

Remaining gates: developer confirmation, deployment-specific provider/license
review, shared-IP pacing/storage configuration, and separately authorized
quality evaluation. No production deployment or external delivery occurred.
Future per-user choice and upstream history/account linking remain deferred.
