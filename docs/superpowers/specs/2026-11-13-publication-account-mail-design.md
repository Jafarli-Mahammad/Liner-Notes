# Publication rights, account email and weekly delivery

**Date:** 2026-11-13
**Status:** approved by the developer on 2026-11-13; implemented with local verification and explicit external activation gates. See `docs/operations/account-email-evidence-2026-11-13.md`.
**Request:** solve the provider rights/attribution/public privacy gaps and the missing email confirmation, password recovery, external delivery and production scheduling identified in the publication-readiness review.
**Repository baseline:** `7b73e4c` on `Prism`.

## Outcome and boundaries

A visitor can read the service's actual privacy/source information, register, confirm their address, recover access, opt in to weekly email, receive saved weekly picks and unsubscribe. The production Worker performs bounded unattended work without duplicate generation or unsafe mail retries. New stored data participates in export, deletion, storage accounting and recovery.

The developer has no public domain, sender address or mail account yet (confirmed during this review). These are activation requirements, not prerequisites for implementation or local contract testing. No account creation, domain purchase, provider enquiry or external test message is included without its applicable authorization and a known recipient. Do not manufacture operator identity, a mailbox, rights approval or successful delivery.

Scoring weights, recommendation policy, upstream evidence requirements and canonical artwork identity stay unchanged. No custom ML, listening-history integration, new recommendation provider or general-purpose marketing system is included.

## Alternatives and recommendation

1. **Existing scheduler + durable dispatch + Resend HTTP adapter (recommended).** Reuses Identity, stored digests and schedule policy; HTTP is available through HttpClientFactory without a new vendor SDK. Provider retry keys and signed events support explicit delivery outcomes. Requires a verified sender domain for real activation.
2. **SMTP adapter.** Portable, but an interrupted SMTP send has an ambiguous result and generally lacks a portable idempotency/reconciliation contract. This does not remove the need for a verified sender and a delivery account. It is a future adapter, not a second implementation now.
3. **Mail provider owns scheduling and contacts.** Fewer local scheduling responsibilities, but duplicates private subscriber/consent state, couples product behavior to the vendor, and weakens existing deletion/eligibility control. Not recommended.

Resend is a proposed initial adapter, not an approved purchase or a dependency of Domain/Application. Any later adapter implements the same bounded transport/result interface.

## A. Rights, attribution and public information

Add public `/privacy`, `/sources` and `/support` pages with persistent navigation from account registration and the site shell. They must be readable without creating a guest profile or fetching music providers.

The privacy notice describes account/guest identifiers, taste, feedback, saved recommendations, email state, essential cookies, public catalog/feed boundaries, upstream requests, processor disclosure, retention, export, deletion and backup limitations. It must not promise immediate deletion of independent backups or provider-held copies. It must not imply private taste/feedback is a public feed. Include an operator name and private contact configured for the actual deployment; Development can identify itself as an unconfigured local instance. Production cannot publish fictitious/default operator information. Update the notice when external delivery is enabled so the processor and outbound data are accurately disclosed.

The source page distinguishes ListenBrainz evidence, MusicBrainz core metadata, supplementary tags and image-specific rights. Preserve score explanations from stored evidence. Add source/license links alongside existing source names in discovery and mail, and preserve existing per-image credits. Do not assert that Cover Art Archive access or a MusicBrainz identifier grants a blanket image license.

Keep Last.fm unavailable for public live generation unless its applicable approval is actually documented. Existing Production configuration already selects ListenBrainz. Audit public projections and mail templates, including historical data, so disabling acquisition is not mistaken for automatically clearing publication rights for every old record. A rights record lists allowed uses, source, evidence reference, review date and restrictions; unreviewed uses remain unavailable. This is a deployment review record, not an automatically granted license. Do not silently discard users' historical private data to resolve a publication gate.

Record the actual noncommercial/commercial deployment decision for MusicBrainz supplementary material. If the required rights cannot be established, stop the affected public use and report the remaining approval instead of changing the scorer or claiming attribution alone resolves it. Draft any needed enquiry locally; do not send it automatically.

Primary sources rechecked November 13:

- [MusicBrainz data license](https://musicbrainz.org/doc/About/Data_License): core and supplementary data have different licenses.
- [MusicBrainz pacing](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting) and [ListenBrainz API](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html): pacing and meaningful User-Agent remain required.
- [Cover Art Archive](https://coverartarchive.org/): availability is not itself a per-image reuse license.
- [Last.fm terms](https://www.last.fm/api/tos): fetch failed with HTTP 402; current terms and any actual public-use approval remain unverified. No permission will be invented.

## B. Account confirmation and recovery

Use ASP.NET Identity's purpose-separated token providers and existing persistent Data Protection keys. Confirmation expires after 24 hours; password reset after one hour. Tokens are never returned by public APIs or written to logs. Require explicit POST submission to consume a link so a mail scanner's GET cannot change an account or password. Confirmation can return an idempotent already-confirmed response without changing consent.

Add request/resend and consume endpoints for confirmation and password reset. Unknown, deleted and ineligible accounts receive the same request response as eligible accounts. Bound requests by IP and normalized account target, apply cooldowns and use bounded durable dispatch rather than sending HTTP inside a managed database transaction. Correctness tests must include the account-specific throttling path, not only an in-memory IP limiter.

Registration still permits onsite discovery without verification. It atomically records account creation and the confirmation dispatch when mail is enabled. A mail transport outage does not pretend the user is confirmed; the account page shows pending confirmation and permits bounded resend. Weekly mail requires a confirmed address, explicit opt-in, no unsubscribe and no delivery suppression. Confirmation never turns opt-in on automatically.

The browser gains confirm/reset routes, a forgot-password link and confirmation status/resend controls on Account. Account responses expose verification state and delivery preference without exposing any token. Provide explicit weekly opt-out/preferences in Account as well as the email capability link.

Successful password reset uses Identity's existing password validation and security-stamp rotation. Invalidate refresh sessions and reject previously prepared login snapshots; test the current coordinated-authentication path rather than bypassing it with direct updates. Match frontend password help to the actual policy.

Use a configured canonical origin to generate all links, never the incoming Host header or an arbitrary return URL. Carry capabilities in the browser fragment where possible, remove them from the address bar after parsing, and submit in POST bodies. Preserve `no-referrer`, no-store and query-safe logging.

## C. Durable mail dispatch and transport

Add an application mail interface and immutable outbound request/result DTOs. The Infrastructure adapter uses HttpClientFactory to call the fixed HTTPS Resend API. Credentials remain in private runtime configuration; redirects cannot forward credentials elsewhere. Production delivery is opt-in and refuses missing/placeholder origin, sender, API key or webhook secret. Development defaults to the existing protected local capture/contract fake, never an unsolicited external send.

Create a bounded account-owned dispatch store for confirmation, reset and weekly messages. Store purpose, account/digest identity, immutable request fingerprint, retry key, state, provider message ID, attempts, deadlines and outcome timestamps. Protect any retained recipient/message/capability material with persistent Data Protection; keep operational capabilities out of user-visible projections and exports. Frozen payload retries must not create a new token, body or key for the same dispatch. Reserve bytes before storing a payload and clear capability-bearing payloads on terminal state/expiry; retain only the necessary bounded outcome metadata.

The lifecycle distinguishes queued, attempted, provider-accepted, recipient-server-delivered, retryable, failed, cancelled and ambiguous outcomes. A successful send API response means acceptance, not inbox delivery. Keep `AcceptedAt` and `DeliveredAt` separate. Preserve the existing meaning that a local capture is not an external send; do not mark a capture delivered or recapture a completed external digest. Document `SentAt` as external submission acceptance if it is retained for compatibility, with delivery confirmation separately exposed.

Use one stable dispatch idempotency key and byte-equivalent payload for retries. Resend retains these keys for 24 hours. Transient retries therefore have a shorter bounded retry horizon. After that horizon an ambiguous request is quarantined for reconciliation, not resent with a new key. A known provider message ID can be queried for reconciliation; a request with no known ID is not assumed unsent. No exactly-once delivery guarantee is claimed across an external network.

At dispatch, recheck the active account, current target address, purpose and suppression; weekly messages also require current confirmation, opt-in and no unsubscribe. Use the existing account email lease to serialize dispatch against account deletion and unsubscribe, with a consistent lock order. Update unsubscribe to participate in that order. Persist an attempt before network I/O and its result afterward; do not hold a database transaction or the global storage write coordinator across network I/O. Connection/lease loss cancels or fences the attempt. Deletion/unsubscribe after provider acceptance cannot recall mail already handed off; state and privacy copy must say so accurately.

Preserve text/HTML alternatives, stored explanations and provider attribution. Use the public discovery/account routes instead of `/manual-test`. Include a normal unsubscribe link and RFC-style one-click unsubscribe headers/POST support for weekly mail; GET only displays confirmation. Account-security mail is separate from weekly opt-in and must not offer an unsubscribe that disables essential recovery. No open pixels, click rewriting or engagement-event collection; disable provider tracking during activation.

Add authenticated delivery-event ingestion. Verify the raw bytes, timestamp tolerance and signature before parsing. Apply idempotent state transitions and tolerate duplicated/out-of-order events without changing a delivered message back to queued. Bound unknown events and avoid retaining full vendor event payloads. Hard bounces/complaints suppress future sending to the affected account/address; events must match a known dispatch and cannot recreate a deleted account. The endpoint is exempt from browser CSRF only because its provider signature is required, not because it is anonymous.

Primary transport contracts checked November 13: [send API](https://resend.com/docs/api-reference/emails/send-email), [24-hour retry keys](https://resend.com/docs/dashboard/emails/idempotency-keys), [verified sending domain](https://resend.com/docs/dashboard/domains/introduction), [events](https://resend.com/docs/webhooks/event-types), [signature verification](https://resend.com/docs/webhooks/verify-webhooks-requests), [Svix verification contract](https://docs.svix.com/receiving/verifying-payloads/how-manual), [usage limits](https://resend.com/docs/api-reference/rate-limit), [terms](https://resend.com/legal/terms-of-service), [pricing](https://resend.com/pricing). No paid plan is selected. Request rate and daily/monthly allowance are configurable operating limits; provider headers/429 override local assumptions.

## D. Production scheduling

Reuse `WeeklySchedulePolicy`, `WeeklyScheduleStore` eligibility, advisory lease and generation commands. Keep the existing synthetic explicitly-listed local scheduler for QA; add a separate explicit Production mode selecting bounded due eligible accounts without a hand-maintained UUID list. Production options cannot accidentally enable synthetic capture or bypass live-provider gates.

Extend the current stopped-writer operator model with a bounded scheduler/dispatch command and systemd timer, rather than adding a competing service framework. The command shares the operator lifecycle lock with backup/restore/upgrade and a database lease with other scheduler instances. Multiple machines require a documented shared coordination arrangement before use.

Each run initializes missing slots, processes only due accounts, generates/reuses the saved user/week digest, then enqueues one unique weekly dispatch. Advance the due slot only after the durable queue record exists; a transport retry reuses that digest. Preserve the existing UTC schedule semantics and label them explicitly; this work does not silently reinterpret the existing TimeZone field or rewrite old schedules. Bound catch-up work, users, dispatch attempts and runtime, and report remaining backlog and per-account failures. A retry and a second week must both be tested.

Prioritize expiring security messages over weekly mail while bounding all work and preventing starvation. Enforce configured provider budgets across scheduler restarts, honor Retry-After and expose quota/backlog/ambiguous-send conditions through the existing operational status. A failed user must not indefinitely monopolize every bounded tick.

## E. Persistence, privacy and migration

Any new dispatch/suppression fields require a reviewed EF migration, schema-inventory entry, coordinated-storage accounting, owner-scoped export and hard-delete coverage. Public policy/configuration records must be identified as shared operational metadata. Export meaningful dispatch metadata and safe message content when retained; explicitly exclude encrypted token/capability material and credentials. Do not hide private musical content merely by classifying a whole payload as operational.

Apply schema changes only in the stopped-writer upgrade process after a verified backup and storage witness transition. Existing compatible-only upgrade automation must continue to refuse unreviewed schema changes. Verify historical digests, score JSON, authentication and local captures survive; no broad data rewrite or implicit migration on web startup.

Default security payload expiry matches the token deadline (24 hours for confirmation, one hour for reset). Weekly dispatch payloads expire after seven days; clear protected bodies as soon as delivery is terminal. Retain safe outcome metadata for 90 days, then prune it in bounded maintenance. Digest uniqueness/completion state must still prevent a pruned dispatch from causing a resend. Persist account/address suppression until the user deletes the account or an explicit reviewed recovery clears it; automatic pruning cannot silently re-enable a bounced address. Make the notice match these actual settings. Deleted accounts cancel pending dispatch and remove owned payloads/outcomes. Independent backups and mail provider retention are disclosed separately; application deletion cannot promise their immediate erasure.

## Proposed implementation and verification boundaries

This is substantial multi-part work, not a small UI patch. Implement sequentially in focused commits, with verification attached to each slice:

1. **Public information and source attribution:** public config projection, `/privacy`, `/sources`, `/support`, shared footer/source links, rights matrix and release gate. Browser checks verify anonymous access without profile/provider side effects, visible attribution and no private/configuration leakage.
2. **Durable dispatch foundation:** persistence/migration, storage accounting, export/deletion and lifecycle; meaningful Domain/Application/PostgreSQL tests for retries, owner isolation, quota and deletion races.
3. **Confirmation and recovery:** Identity orchestration, API and React flows; test successful, invalid, expired and reused capabilities, throttling, enumeration-safe responses, recovery stamp/session invalidation and eligibility.
4. **External transport and unsubscribe:** Resend adapter, production renderer, signed events, suppression and reconciliation; mock HTTP contract tests plus PostgreSQL concurrency/restart tests. Local protected capture is not counted as delivered mail.
5. **Production schedule and operations:** bounded due selection, timer/operator integration, budgets and health; overlap, restart, retry, second-week, backup/upgrade-lock and outage acceptance.
6. **Integrated acceptance and documentation:** browser signup → captured confirmation → login/taste → scheduled digest → simulated provider acceptance/event → unsubscribe; export/deletion, restarted host and recovery checks. Update CHANGELOG, PROGRESS, operating procedures and the readiness report with exact outcomes. Real recipient-server delivery is a separate activation check once a verified domain/account/recipient exists.

Primary existing paths: `src/Presentation/Controllers/AuthController.cs`, `src/Presentation/Services/AuthService.cs`, `src/Presentation/Filters/DiscoveryWriteFilter.cs`, `src/Presentation/Controllers/UnsubscribeController.cs`, `src/Presentation/ClientApp/src/features/account/`, `src/Infrastructure/Email/`, `src/Application/Features/Digests/Commands/RunScheduledTick/`, `src/DataAccess/Persistence/Repositories/WeeklyScheduleStore.cs`, `src/DataAccess/Persistence/AccountEmailLease.cs`, `src/Worker/Program.cs`, `src/Worker/ProductionWorkerOptions.cs`, `scripts/ops/production.py`, `deploy/compose.production.yaml`, `deploy/systemd/`, and the corresponding existing test projects.

Before backend implementation verification, install the repository-pinned SDK in a task-local/user-local tool directory or use a verified matching SDK container; do not change `global.json` to the installed older SDK. Recheck current stable releases and compatibility before adding any package. No package is required merely to call the HTTP send endpoint. New cryptographic protocol code, if needed, must use the documented verification contract and published test vectors; prefer a verified compatible existing implementation when it is already available. Reference [Identity confirmation/recovery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-10.0) and [RFC 8058 one-click unsubscribe](https://www.rfc-editor.org/rfc/rfc8058), including DKIM coverage of the required headers during real delivery acceptance.

## Acceptance limits

Repository implementation can be completed without purchasing a domain. Public activation still needs a real operator/contact, canonical HTTPS origin, verified sender domain, accepted delivery-account terms, credentials, disabled tracking, webhook configuration, current source-use decisions and a developer-controlled recipient. Missing legal permission cannot be solved by setting an approval flag. These requirements remain explicit rather than being reported as completed by a local fake or a draft notice.
