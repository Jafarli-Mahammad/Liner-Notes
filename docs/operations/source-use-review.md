# Source-use review for public activation

Last checked: 2026-11-13. This is the implementation's source/rights matrix, not a
third-party approval or legal opinion. The operator must record the actual use,
reviewer/date, permission/license reference and restrictions before setting the
Production source-use fields. Retain evidence privately without credentials or
personal provider-account data. No enquiry was sent as part of this work.

| Material/use | Evidence and conditions | Current implementation / activation status |
| --- | --- | --- |
| MusicBrainz core catalog names, identifiers and relationships | [CC0 core data](https://musicbrainz.org/doc/About/Data_License); distinguish the [database components](https://musicbrainz.org/doc/MusicBrainz_Database) | Canonical metadata/provenance retained. Published core license does not license unrelated images or supplementary tags. |
| MusicBrainz supplementary tags and normalized tag weights | [CC BY-NC-SA 3.0 supplementary data](https://musicbrainz.org/doc/About/Data_License); identify source/adaptations and preserve applicable attribution/share-alike restrictions | Source page and mail identify MusicBrainz and normalized weights, with license/source links. Operator must record actual noncommercial use or an actual commercial license reference. Software remains AGPL; no private user data is made public by this notice. |
| ListenBrainz candidate/connection evidence | Review the actual endpoints and supplied data under [ListenBrainz API operation](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html) and applicable MetaBrainz terms | Default provider remains swappable and bounded. Existing `LiveUseApproved` and new rights record are required attestations; an HTTP response or configuration flag is not permission. Account history remains outside this slice. |
| Last.fm data/public use | [API terms](https://www.last.fm/api/tos); current fetch returned HTTP 402, so current terms and existing public-page approval questions remain unverified | Public live generation stays disabled. No permission was manufactured or requested by email. Private historical records are retained under existing ownership controls; external weekly delivery refuses unreviewed/unknown/Last.fm evidence. Enabling the adapter later requires the actual relevant approvals and placements. |
| Artist photographs | Image-specific Commons file, author credit and license, plus canonical artist association | Existing per-image source/credit/license controls and cache admission remain; placeholders cover unavailable/ineligible images. No artist-name image substitution. |
| Recording covers | [Cover Art Archive](https://coverartarchive.org/) states that images remain copyrighted by their respective owners | Canonical identity alone is not a reuse license. Production recording-cover routes return placeholders/404 without `PublicService:CoverArtUseReference`. Any reference must identify actual allowed uses/rights evidence; the field itself grants nothing. |
| Listening-service links | External service searches or previously stored validated direct links | No audio, playlist synchronization or similarity API integration is asserted. Production email direct links are restricted to expected Spotify/YouTube hosts; otherwise use service search. |
| Application and bundled assets | AGPL source and existing generated third-party font/dependency notices; MediatR 12.5.0's approved exception | No dependency/license policy was changed. Keep notices with clean release artifacts. |

Public pages are `/sources`, `/privacy` and `/support`. Individual discovery
sources are linked; mail credits come from stored score evidence rather than a
new provider call. The public feed remains an allowlisted projection of public
CC0 catalog metadata, without private profile, feedback or listener identifiers.

Operational constraints still apply: [MusicBrainz pacing](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting)
and [ListenBrainz response limits/User-Agent](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html)
require coordinated use across processes sharing the upstream identity/IP.
Existing pacing/cache requirements were preserved, not waived for scheduling.

Before activation, resolve any territorial/consent, publication/export, provider
hosting or text-only mail conditions that apply to the chosen use. If a required
permission cannot be established, leave that use disabled. Do not change scoring,
invent missing evidence, relabel copyrighted covers as CC0, or delete private
history to make a release checklist look complete.
