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
