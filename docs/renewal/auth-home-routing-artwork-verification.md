# Account, Home, routing and artist-photo verification

Implemented and locally verified on 2026-11-02; developer confirmation is pending.

## Causes and behavior

Account credentials were held only in a JavaScript module. Reloading discarded
both bearer tokens and identity; there was no account cookie or startup restore.
Live testing also found that Identity's replacement of an existing refresh token
was lost under the context's global NoTracking default. Registration inserted a
valid token, but a later login returned a new token while retaining the old hash.
The service now explicitly updates an existing refresh credential within the
same coordinated transaction; first insertion still uses Identity.
A protected HttpOnly, SameSite Strict cookie now restores the existing database-
backed, rotating refresh session. Its expiry follows the configured refresh
lifetime. Tokens remain out of browser storage. Logout fences the refresh token
and clears both cookies. API authorization remains bearer-based. The readable
cookie contains only a presence marker, never credentials. Browser Web Locks
serialize rotation across tabs where supported; the server still rejects replay.
Temporary bootstrap failures show recovery rather than assuming guest identity.
Session traffic has a separate 60/minute limit; password attempts retain 10/minute
and Identity lockout. Configured Data Protection key paths now apply in every
environment; default development keys remain in the normal per-user key store.

Home always used the anonymous publication feed. It now reads the existing owned
history overview and batch APIs for signed-in users, newest batch first and rank
order within each batch, capped at four cards and four batch reads. It links to
History and displays a personal empty state. Anonymous publication behavior stays
available. No discovery runs merely because Home is viewed.

Development launch paths had a leading slash. They are now relative. The reported blank screen was not reproduced in the current browser
build before the change; the original malformed path was confirmed in the launch
configuration and remains invalid as a launch target. The host now redirects root GET/HEAD paths consisting solely of repeated slashes to `/`,
preserving the query string; other paths and mutations are not rewritten.

Taste rendered artist selections as blank symbols and never requested photos.
Its persisted projection also omitted canonical identities. The projection now
returns those identities. A provider adapter resolves canonical MusicBrainz URL
relationships to a Commons file or Wikidata image, reads Commons license metadata,
and downloads only a validated JPEG/PNG from allowlisted Wikimedia hosts. It
rejects redirects, restrictive licenses and private/no-cache/no-store images.
Acquisition runs in a bounded background queue with pacing, backoff, size limits,
negative caching and the existing aggregate metadata-memory budget. Cache lifetime
is bounded by metadata permissions and image freshness, with a one-hour maximum
image heuristic when no directive is supplied, consistent with recording artwork.
Photos expose source/credit/license details; unavailable images show initials.
No artist names are guessed into image URLs and no website scraping is used.

## Local evidence

Playwright MCP was launched with installed `/usr/bin/chromium-browser` because the
bundled connector's configured `/opt/google/chrome/chrome` does not exist.
The normal development profile was exercised on port 5081. Mutation journeys used
a separate host with the same private live configuration and its public origin
set to that port. The existing host on 5080 and existing accounts were preserved.
New isolated verification accounts used real provider data, without fabricated
recommendations or resetting storage.

- Registration and actual sign-in, page refresh, persistent HttpOnly cookie,
  backend restart, reopening in a fresh browser context, and concurrent tabs.
- Five real ListenBrainz picks persisted; Home showed its four most recent picks.
  Home refresh, History navigation, detail pages and direct refresh passed.
- Logout survived refresh and replaying the revoked cookie was rejected. A second
  account saw an empty Home and received 404 for the first account's batch. Removing
  its browser credential returned sign-in. Backend token expiry used controlled-clock
  PostgreSQL checks rather than waiting multiple days.
- Empty personal Home and private-API error handling passed controlled regression
  tests; the public feed was not used as a fallback for authenticated failures.
- Root `//`, repeated-slash root with query, and direct Taste/History/Account
  routes rendered without JavaScript exceptions.
- Real catalog selections for Lin Pesto, Jakuzi and Radiohead survived save and
  reload. Radiohead's Commons photo decoded with Raph_PH / CC BY 4.0 attribution.
  Lin Pesto has no linked Commons/Wikidata photo source; Jakuzi's linked Wikidata
  item has no P18 photo. Their initials fallback is intentional. Controlled tests
  also cover missing images, image-load errors, temporary failures and refresh.
- No page exceptions in the successful live journeys. Console inspection found
  the existing missing favicon and expected pending/unavailable artwork HTTP
  responses; navigation cancels superseded artwork requests normally.

Automated verification: 72 frontend Playwright tests; 87 focused Infrastructure
provider/artwork tests; 61 PostgreSQL coordination/history tests. Presentation
verification passed 108 tests with 34 explicit PostgreSQL skips in the general
run, followed by 25 passing focused PostgreSQL security/controller checks covering
refresh expiry, replay, security stamps, password reset,
lockout, deleted accounts and independent session/password rate limits.

## Limits

This is local HTTP verification, not public HTTPS/production acceptance. IDE
automatic browser opening was not directly exercised; relative launch settings
and actual root HTTP/browser behavior were checked. Backend
expiry is covered with controlled-clock PostgreSQL tests; natural multi-day
expiration was not waited out. Existing access JWTs retain their bounded lifetime
after logout; their refresh session is revoked. History still exposes the existing
latest-ten-session window. Artist photos are memory-cached and reacquired after
restart, rather than introducing new persistent personal data or schema changes.
Existing package/analyzer warnings remain outside this task's scope.

Provider references checked during implementation:
[MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API),
[ListenBrainz metadata](https://listenbrainz.readthedocs.io/en/latest/users/api/metadata.html),
[Commons Imageinfo API](https://www.mediawiki.org/wiki/API:Imageinfo), and
[Commons reuse requirements](https://commons.wikimedia.org/wiki/Commons:Reusing_content_outside_Wikimedia).
