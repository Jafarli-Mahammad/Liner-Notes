# Local live application verification — 2026-10-23

The developer requested the actual application on this machine with real
provider data. Earlier synthetic browser results did not satisfy that request.

## Runtime and startup

- The `Liner Notes` launch profile enables the private local live configuration.
- `bash scripts/run_live.sh` selects the pinned SDK, initializes missing local
  state once, and starts **http://localhost:5080/**.
- The system `/usr/bin/dotnet` could not resolve SDK 10.0.401; the installed
  `$HOME/.dotnet/dotnet` could. The launcher handles that case.
- An older app instance occupied port 5080 and returned 404 for discovery. That
  identified process was stopped before the live application claimed the address.
- The live database and inventory persist outside `/tmp`; existing User Secrets
  and earlier databases were preserved. Git and Docker contexts exclude the
  private `.linernotes/` directory.
- Re-running initialization against its matching database/inventory succeeded
  without resetting retained state. The initializer rejects a retained database
  with missing matching state or pending migrations.

## Actual provider and browser evidence

The existing bounded ListenBrainz smoke returned two artist candidates, two tag
candidates, four hydrated candidates, 27 seed tags and no provider gaps.
It made no account-history requests and saved no raw response recordings.

Playwright MCP then exercised the running application's real HTTP path:

1. MusicBrainz found Radiohead through the UI's explicit online lookup.
2. ListenBrainz generated five real picks, including music by Weezer, Foo Fighters,
   Soundgarden, Tame Impala and The Smiths.
3. Separate real lookups selected the album **OK Computer** and recording
   **Karma Police**, alongside a selected style; generation succeeded with all
   three saved input kinds.
4. Feedback survived a page reload. Guest export contained live evidence and no
   synthetic evidence. The frontend displayed **Live discovery**.
5. A Massive Attack request generated five real picks and published five eligible
   entries in Recent recommendations. Feed origins were `Public`; response keys
   contained no recipient, user, profile, score or batch fields.
6. A database query confirmed zero synthetic catalog publications in the dedicated
   live database.

## Corrections discovered during live verification

ListenBrainz metadata sent `Cache-Control: public, max-age=0, s-maxage=120`.
The client originally ignored `s-maxage`, preventing permitted shared catalog
retention. It now applies the shared lifetime before the browser lifetime and
continues to reject private/no-cache/no-store responses.

The feed previously required selected tracks to exist in a preloaded catalog.
The provider now exposes an independent public core metadata projection. The
existing coordinated catalog importer stores that metadata before private
scoring. This projection excludes tags, seed queries, user identities and scores;
it carries real source/hash/expiry information and CC0 attribution. Completed
recommendations can use those eligible catalog rows for the public feed.

The rules were checked against the official [ListenBrainz API documentation](https://listenbrainz.readthedocs.io/en/latest/users/api/),
[recording metadata contract](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html),
[MusicBrainz licenses](https://musicbrainz.org/doc/About/Data_License),
[MusicBrainz rate limits](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting)
and [RFC 9111 shared-cache semantics](https://httpwg.org/specs/rfc9111.html#cache-response-directive.s-maxage).

## Regression result

The final PostgreSQL-backed solution run passed **695 tests**, with zero failures
or skips: Domain 148, DataAccess 130, Application 137, Presentation 80 and
Infrastructure 200. Ten added cases cover shared-cache policy precedence and
public metadata eligibility/privacy. Existing isolated cache fixtures were updated
to supply their explicit catalog dependencies. The browser auth/request regression,
launcher shell syntax check and `git diff --check` also passed.

The app remains a loopback host in Development for local HTTP/IDE use. This result
does not establish public deployment, external email delivery, recommendation
quality or developer acceptance of every behavior. See the
[live operation guide](local-live-run.md) for startup and retained-data handling.
