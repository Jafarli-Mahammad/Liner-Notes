# Frontend refinement — 2026-11-01

Implemented and locally verified; developer acceptance and production deployment remain pending.

The existing palette, fonts, editorial layouts and album sleeves remain the design reference. No packages, backend code, scoring rules or authentication contracts changed.

## Changes and causes

- Compact recommendation cards explicitly hid every service except Spotify. The shared `MusicLinks` component now renders Spotify, Apple Music, YouTube and Bandcamp on every personal pick and Home feed card. Compact cards use labeled icon buttons; featured picks keep text labels. Inline SVG marks have hollow outlines, consistent dimensions, keyboard focus and 44px targets. Existing validated direct links remain preferred; searches encode artist and track together.
- Home had no request timeout. A stalled response now becomes a recoverable error after 20 seconds. Its delayed skeleton reserves space for the sleeves, titles, artist names, service controls and attribution.
- Personal-page bootstrap and saved-batch retrieval used text-only or empty-state presentation. They now use lightweight placeholders; account/taste loading uses field shapes and history uses rows. Lazy navigation has a delayed top indicator. Feedback saves and style searches expose local busy states. Existing generation, catalog lookup and account-operation status/error handling remain in place.
- Artwork previously revealed on the load event and could wait indefinitely if the image GET stalled after HEAD succeeded. Covers now decode before a short opacity reveal. A subtle delayed mark sits over the existing gray sleeve while pending; definitive unavailable responses stop it. A 20-second image watchdog uses the existing bounded retry policy, including Retry-After and the ten-minute overall budget. No image proxy, extra provider requests or cache-policy changes were introduced.
- History explicitly orders summaries newest first and marks detail reads busy. Failed reads no longer retain a loading caption. Home remains prominently positioned and public; History contains the listener's private batches.
- Sign-in keeps the existing two-panel composition and all validation/security behavior. Refined field surfaces, a quiet coral edge, small introductory labels, a password visibility toggle and specific submission labels make the forms fit the rest of the app.

Loading reveals wait 180ms; the artwork mark waits 350ms. Reduced-motion mode disables these animations and image transitions.

## Verification

`npm run build:local` passed type checking, production bundling and local asset publication. `LINER_BROWSER_EXECUTABLE=/usr/bin/chromium-browser npm test` passed **65 tests**, including eight new refinement regressions. Existing artwork, navigation, identity isolation, recovery, feedback and account checks remain enabled. Desktop/tablet/mobile checks cover 1440, 768 and 390px; long-text reflow also covers 320px. Accessibility checks include WCAG AA, keyboard focus and reduced motion. Delayed, unavailable, broken, failed and stalled image/feed responses are explicitly exercised.

Tests now scroll artwork frames before asserting lazy-loaded images. The real Home network assertion allows only GET and artwork HEAD reads and still rejects writes and external requests. The responsive-menu test waits for the desktop breakpoint before narrowing again, avoiding coalesced resize events.

The existing real-provider browser journey also passed: guest taste, five generated picks, feedback, history, registration preserving guest progress, export, sign-out, login restoring picks, and disposable-account deletion. See [the journey record](live-journey.json).

A separate QA host read the same real guest history, restarted, reopened all five recommendations and returned an identical owned batch. That guest was cleared. Four eligible public feed entries were also unique, descending by sequence and identical across a QA-host restart. See [owned history](history-restart.json) and [public feed](public-feed-restart.json).

The feed is stored in PostgreSQL and populated transactionally from eligible public catalog metadata, independently of private account records. It intentionally deduplicates recordings and orders first publication by sequence; repeating an existing recording does not move it to the front. Expired/ineligible public metadata is excluded. No persistence defect requiring a backend change was established.

## Visual evidence and limits

Account and Discover screenshots use controlled contract fixtures, not claims about provider results. Home screenshots use the local public API and may show sleeves while artwork resolves. Compare [mobile sign-in](account-anonymous-390.png), [desktop sign-in](account-anonymous-1440.png), [mobile Discover](discover-controlled-390.png), [desktop Discover](discover-controlled-1440.png), [Home](home-live-1440.png) and [loading](home-loading-test.png).

Playwright MCP was attempted but cannot launch: its configured `/opt/google/chrome/chrome` is absent, and creating that system path requires an OS password. Browser verification used the installed Chromium through Playwright directly. Superdesign was not used because these bounded changes reuse existing components and tokens rather than explore a new composition.

The real account-deletion response still reports incomplete cleanup of some local email copies; the UI surfaces that existing operator-cleanup warning. No backend cleanup change or production validation is claimed. The local host is available at http://localhost:5080/.
