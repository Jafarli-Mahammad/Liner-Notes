# Guest discovery catalog sources and acquisition policy

Checked: 2026-10-16. This is a source-policy record, not a live acquisition approval or legal clearance for a particular deployment.

## Source decisions

| Data | Source and verified documentation | Decision |
| --- | --- | --- |
| Artist identifiers/names/aliases and release-group identities/credits | [MusicBrainz core data](https://musicbrainz.org/doc/MusicBrainz_Database#License), [data license](https://musicbrainz.org/doc/About/Data_License) | Use the core CC0 fields as the preferred local search metadata. Record source/version/integrity even where attribution is not legally required. Do not infer that tags share this license. |
| Artist and album tag assignments | MusicBrainz classifies tags and genre associations as supplementary CC BY-NC-SA 3.0 data on the same official data page | Keep tag provenance and applicable attribution/license separate from core metadata and the application's AGPL license. Deployment use and redistribution still need the existing usage review. A transformed weight is not automatically unrestricted data. |
| Initial bulk search artifact | [Official MetaBrainz dataset descriptions](https://metabrainz.org/datasets) describe PostgreSQL and JSON exports | Prepare a filtered compact artifact outside the VPS where possible. A source URL, exact snapshot/hash, selected fields, byte/call limits and permitted use belong in its concrete acquisition/import manifest. No full dump has been downloaded. |
| Explicit artist/album resolution | [MusicBrainz search documentation](https://musicbrainz.org/doc/MusicBrainz_API/Search) documents artist and release-group indexes, aliases and artist-credit/disambiguation fields | Use a deliberate bounded search action. Resolve a canonical selection; do not silently choose the first ambiguous name. Keystroke search remains local. |
| Album metadata and tag evidence | [ListenBrainz metadata API](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html#get--1-metadata-release_group-) documents release-group IDs, artist credits and separate artist/release-group tag sections | Extend the existing provider with this verified metadata capability in the album stage. Query only required inclusions. Preserve whether tags came from the release group or artist. Endpoint documentation does not prove catalog coverage. |
| Candidate discovery | [ListenBrainz miscellaneous API](https://listenbrainz.readthedocs.io/en/latest/users/api/misc.html#get--1-explore-lb-radio) documents LB Radio prompt/mode output; the existing adapter implements bounded artist/tag prompts | Keep upstream candidate discovery behind the current provider abstraction. No album-similarity endpoint is established by the metadata API. Indefinite retention/redistribution of every generated radio response was not established by this check; do not enable such a cache by assumption. |
| Last.fm data | [API terms, clauses 4.3.4 and 4.4](https://www.last.fm/api/tos) state a 100 MB aggregate data cap absent prior consent, caching according to HTTP headers, and service-defined rate limits | Do not use Last.fm to populate a generic 256 MiB catalog/cache. Its existing public-use/attribution and acquisition gates remain. No universal requests-per-second allowance was verified. No provider switch or package change is part of C1. |

The selected path is MusicBrainz core metadata for names/identities, source-specific supplementary evidence for taste, and bounded existing-provider candidate acquisition. A compact local search database and prepared recommendation readiness are separate datasets and measurements.

## Pacing, caching and credentials

- [MusicBrainz pacing documentation](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting) currently describes an average one request per second per source IP unless agreed otherwise. Share the application's existing cross-process pacing directory across hosts/containers on that IP.
- [ListenBrainz API documentation](https://listenbrainz.readthedocs.io/en/latest/users/api/#rate-limiting) requires no more than one call per second per application and observing response limit/reset headers. Retain the conservative existing pacing and honor backoff; concurrent jobs do not increase the source allowance.
- Use an identifying User-Agent. Provider credentials remain operator configuration, never onboarding fields, query parameters, committed artifacts or user exports.
- Retention rules must be explicit per source/endpoint. Respect `no-store`, `no-cache`, age and expiry conditions; short-lived HTTP caching does not authorize permanent raw-response storage.
- A live-derived cached response retains its original origin, hash, method, identity and acquisition time. It is not an approved recording merely because it is on disk.
- Evaluation-only/held-out recordings remain ineligible for product preparation. No account listening-history collection is introduced.

## Compact artifact contract

The product importer will accept a versioned manifest binding source snapshot, field policy, normalization version, origin, counts, hashes, license/attribution, expiry/refresh rules and quotas. It must validate IDs, aliases, credited-artist relationships, evidence scopes and conflicting records before atomic publication. Cache eviction cannot invalidate retained private selections or historical explanation evidence.

The C1 resource tool intentionally accepts only `liner-catalog-probe/v1` with `origin: synthetic` and `source: generated-fixture`. Rows have canonical UUID `id`, artist/album `kind`, display `name`, bounded `aliases`, `artist_ids`, and bounded `{name, weight}` tags. It refuses claimed live/public provenance. This is a reproducible probe format, not a product import-approval mechanism.

The probe keeps an on-disk SQLite identity/reference index with a 2 MiB cache instead of retaining the entire catalog in RAM. It publishes output without overwriting existing files. Its temporary SQL schema is random and restricted to a loopback database whose name starts `liner_catalog_probe_`. It has no HTTP client.

## Open gates

- A concrete real catalog acquisition manifest and exact snapshot have not been approved or run.
- Real tag coverage, album evidence yield, useful candidate yield and recommendation quality remain unverified.
- Deployment-specific supplementary-data use/redistribution and permanent candidate-cache policy still require the existing review. These do not prevent independent synthetic implementation and verification.
- The real importer still needs coordinated reservations, import-overlap/WAL measurements, and full source enforcement. C1's conservative input checks and isolated schema-size check are not the production storage guard.
- Before enabling any real source, reconcile the actual shared inventory and `GenerationStorage__StopBytes`; no current runtime setting is enlarged by this record.

## Explicit identity lookup verification (C5)

Rechecked 2026-10-16 against [MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API),
[search documentation](https://musicbrainz.org/doc/MusicBrainz_API/Search), and
[data licensing](https://musicbrainz.org/doc/About/Data_License). The separate rate-limiting
page returned HTTP 429 during this check; the main API page independently states the
one-request-per-second limit and meaningful User-Agent requirement. The existing shared
MetaBrainz pacing gate supplies a minimum 1.05-second interval across local processes.
The service describes non-commercial use as free and requires commercial usage review;
no commercial subscription or purchase is introduced.

Artist and release-group searches use fixed MusicBrainz endpoints and escaped literal
queries. Release-group results supply canonical identities, disambiguation, first-release
date and all returned artist credits. Search-response tags are deliberately excluded:
core names/identifiers and supplementary tag assignments have different licenses.
The product adapter rejects recorded evaluation input as product lookup data. Synthetic
lookup is explicitly labeled synthetic and never becomes fallback data for a live request.

The response client now propagates the actual permitted shared-cache deadline. No-store,
no-cache, private, or already-expired responses do not grant a durable cache lifetime.
C8 must check that deadline before public publication. There has been no live search
acquisition to establish current endpoint cache headers or real result coverage. HTTP
client logging is disabled for this client so explicit query text is not copied into
outbound request logs. Operational logs must not enable query strings or credentials.

Product catalog storage reuses Artist/Album identities; Album.ReleaseGroupMbid is separate
from the historical release MBID. Versioned search projections and all-credit links support
local search and atomic publication. Import artifacts are bounded JSONL, hashed before
writes, validated with a bounded line reader, and staged in at most 250-record transactions.
A committed cursor supports restart. The active publication pointer changes only after all
records have committed. A failed import retains the previous searchable publication.
All public managed writes still require admission; C5's default admission remains closed
until C7. No live source manifest or source acquisition is approved by these code changes.
