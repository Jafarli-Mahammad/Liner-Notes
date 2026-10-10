# Phase 7 local Worker and email design review

Prepared 2026-10-11 against `Prism` implementation commit `886c432`.
The developer requested Phase 7 implementation and selected a working local
unsubscribe flow, including one opt-out column and matching API/export changes.
This document makes that scope concrete for architectural review. The written
design and subsequent implementation plan still need review before product edits.
Phase 6 remains locally verified with developer confirmation pending.

## Implemented, deferred and not done

| Previously selected scope | Status in this checkout and reason |
|---|---|
| Recorder-fed baseline-A generation and pure materialization | Implemented in the Phase 6 commits; quality and developer confirmation remain pending. |
| Complete versioned snapshots, unknown fields and stored-input replay | Implemented; no fresh Phase 7 verification is claimed. |
| Canonical ISO-week idempotency and atomic PostgreSQL persistence | Implemented; local email recovery has not been built. |
| Ordinal ties, one eligible pick per artist, up to five | Implemented; Phase 7 reuses persisted selections. |
| Independent familiarity, numeric feedback and effective feedback rebuilding | Implemented; no feedback-policy revision is proposed. |
| Complete export 2.0 and stable history pages | Implemented; opt-out/export 2.1 remains proposed. |
| PopularityPenaltyEnabled, its migration/settings API/UI/export | Deferred under the developer's earlier instruction to wait; absent from this checkout. It must not be silently implemented as part of Phase 7. |
| Temporary 1,000,000,000-byte storage stop | Implemented in another worktree's `32b661f`; not incorporated here. This checkout still enforces 80,000,000. The temporary value is unresolved; the real limit belongs to build-order step 9 after hosting selection. |
| Development manual QA environment | Implemented in another worktree through `ce0e8d8`; not incorporated here. Reconcile existing authorized follow-ups before relying on that frontend for Phase 7 verification. |
| Working local unsubscribe, Worker, renderer and sink | Not done; the developer selected the scope, and this concrete architectural design is awaiting review. |
| Full privacy/deletion audit and scheduled/external delivery | Deferred behind the two Phase 8 approvals; local mechanics do not establish these results. |

The refreshed session memory located the earlier decisions and worktree commits.
`git show 63f2b4a 32b661f` and `git log HEAD..ce0e8d8` verified the relevant
follow-ups exist but are not on this source checkout's current history. Their
code/test changes require a scoped prerequisite reconciliation in the subsequent
implementation plan; no worktree or history was changed during this design pass.

## Intended outcome and success criteria

An operator runs the existing Worker for one explicit subscriber and ISO week.
The Worker uses the Phase 6 offline generator, persists a digest, renders its
stored recommendations as HTML and plaintext, and captures a message in a local
filesystem sink. A restart reuses the persisted recommendations and recovers an
interrupted local capture without creating a second completed message.

Every message includes explanations from the persisted score breakdown, ordinary
listening/catalog links, text attribution and a functioning local unsubscribe
link. A short list keeps its actual size; an empty digest says there were no
eligible discoveries. Neither is padded. Local capture has a distinct status
from external delivery. **Mechanics only, no catalog conclusions.**

The developer's request covers local Phase 7 work. Live acquisition, external
email, scheduling, public deployment, purchases and the two Phase 8 approvals
retain their existing boundaries. Completing this slice leads to an explicit
proceed/narrow/revise reassessment; it does not start a later phase automatically.

## Source audit and reassessment

| Concern | Evidence in this checkout | Consequence for Phase 7 |
|---|---|---|
| Worker behavior | `src/Worker/Program.cs` registers three layers and calls `host.Run()`; it registers no job. | Add an explicit one-shot command and predictable exit results. |
| Offline generation | `GenerateDigestCommandHandler` returns generated/existing/stopped; an existing digest returns before provider/storage reads. | Reuse this command; recovery reads persisted data without reacquisition. |
| Recorded inputs | `RecordedLastFmApiClient` accepts immutable supplied responses; no production recording loader exists. | Recorded mode needs explicit bounded input adaptation and approval/hash/expiry validation. Default demonstration uses synthetic fixtures. |
| Local email | No email interface, renderer or sink exists under `src/`. | Introduce Application contracts and Infrastructure implementations; keep the Worker a composition root. |
| Unsubscribe | `User` has schedule fields but no subscription state; no controller implements unsubscribe. | Add the one opt-out column selected by the developer and an idempotent local endpoint. |
| Digest lifecycle | Existing values are Pending, InProgress, Sent and Failed; status is stored as a string. | Append LocalCaptured; leave Sent/SentAt reserved for actual external delivery. |
| Delivery claims | Older entity/blueprint comments associate a unique weekly digest row with idempotent delivery. | That constraint prevents duplicate digest rows; it does not prove unique email delivery. Correct affected comments during implementation. README already qualifies delivery as unverified. |
| Explanation | `WeeklyRecommendation.WhyThisPick()` delegates to the stored breakdown; DTO mapping already exposes it. | Render that stored explanation; do not rescore or generate prose. |
| Shared storage | `LocalGenerationStorage` validates six artifact categories and a five-minute inventory; digest writes alter the database revision. | Count message copies and partial writes, explicitly reconcile between generation and capture, and honor the previously selected temporary cutoff after prerequisite reconciliation. |
| Coordination | Generation takes a PostgreSQL session advisory lock and a local exclusive file lease. | Define lock ownership for capture; avoid recursively acquiring generation's lock through another connection. |
| Data inspection | Export 2.0 contains subscriber and digest state; security internals are excluded. | Export opt-out and LocalCaptured state, and document local message files as additional account-owned copies. |
| Dependencies | Runtime targets net10.0; Worker Hosting is 9.0.2, EF is 9.0.2 and Npgsql EF is 9.0.4. Rider MCP confirmed project references. | Use the existing packages/shared framework; no EF upgrade or email-provider SDK is part of this slice. |
| Quality and tags | Generation uses provisional A and candidate artist tag evidence. | Artist tags describe artists, not necessarily individual tracks; suitability and recommendation quality remain not verified. |
| Cost and permission | This proposal writes local files; no email service is selected. | Local mechanics require no email-service purchase. Real storage capacity, hosting cost and provider permission remain not verified. |

Codebase-memory MCP supplied project/symbol navigation, checked against source.
Its inferred layer edges are not treated as proof of C# assembly dependencies.
Rider MCP supplied project/dependency inventory; no new build or implementation
test run is claimed by this design audit. The recorded Phase 6 full-suite result
is 337 passed, as reported in its evidence, not a fresh Phase 7 result.

## Approaches considered

1. **Recommended: complete local slice with persistent opt-out.** A one-shot
   Worker, bounded file sink, local unsubscribe endpoint and one migration meet
   the requested end-to-end scope. Delivery recovery is demonstrated locally;
   scheduling and external-provider failure windows stay in Phase 8b.
2. **Narrowed preview slice.** Render local previews and defer functional
   unsubscribe. This requires fewer contracts but would leave the original
   Phase 7 unsubscribe requirement incomplete. The developer selected approach 1.
3. **Scheduled production sender.** This introduces provider selection, cost,
   scheduling and external-send recovery. Those are later-phase decisions and
   are not selected by the current local-slice request.

## Boundaries and proposed contracts

- **Domain:** append `DigestStatus.LocalCaptured = 4`; add
  `User.EmailUnsubscribedAtUtc`; enforce idempotent opt-out and a local-capture
  transition that does not populate `SentAt`. Scoring and selection policy stay
  under the Phase 6 contract.
- **Application:** add a MediatR command for local digest capture; email-renderer,
  local-sink, protected-unsubscribe-token and capture-store interfaces. The
  command coordinates generation, persisted reads, subscriber eligibility,
  rendering, capture and lifecycle updates. No filesystem, EF or HTTP behavior
  belongs in its handler.
- **DataAccess:** implement capture ownership and state updates, locked opt-out
  changes and the one-column migration. Reuse existing repositories and unit of
  work; final eligibility/state checks run transactionally. A distinct capture
  lease serializes local capture for a digest; do not wrap generator invocation
  in its own generation advisory lock.
- **Infrastructure:** implement deterministic HTML/plaintext rendering, MIME
  serialization, local file capture, token protection and explicit recorded-input
  adaptation. Reuse the evidence parser and immutable replay client. A token's
  purpose is unsubscribe only and contains no email address or taste data.
- **Presentation:** add a thin local unsubscribe controller using Application
  commands; expose the opt-out field through profile/data inspection and export.
- **Worker:** parse explicit local options, validate configuration, create a fresh
  DI scope, invoke the command, report bounded result codes and exit. No periodic
  scheduler or automatic discovery of subscribers is introduced in this slice.

Reuse existing projects and test assemblies. Proposed additions belong to
`Application/Features/Digests/Commands/`, `Application/Features/Subscribers/Commands/`,
`Application/Common/Interfaces/`, `Infrastructure/Email/`,
`Infrastructure/RecommendationSources/LastFm/`,
`DataAccess/Persistence/`, `Presentation/Controllers/` and the existing Worker.
These are the concrete new folder areas for review against the older blueprint.

## Schema, API and export changes selected for review

One SQL column: `Users.EmailUnsubscribedAtUtc`, nullable PostgreSQL
`timestamp with time zone`, default null. Existing accounts remain eligible for
this explicit local capture until opted out. Repeated unsubscribe retains the
first UTC timestamp. Account deletion removes it with the User row. Unsubscribe
does not delete the account, feedback, taste data or recommendation history.

Append the nullable property to `SubscriberDto` and its mapping. Export becomes
**2.1**, reflecting this additive subscriber field and the new digest enum value.
Existing enum numeric values remain unchanged; LocalCaptured is stored in the
existing status column. `SentAt` remains null for newly captured local digests.
No new delivery table, recipient ledger or persisted token is proposed.

Proposed local routes:

- `GET /api/digests/unsubscribe?token=...` validates the protected capability and
  renders a small confirmation form. GET does not mutate account state.
- `POST /api/digests/unsubscribe` accepts that capability as form data, executes
  idempotent opt-out, and renders a generic confirmation without account details.

The endpoint requires no login: the protected capability authorizes only opt-out
for its account. Invalid/oversized/tampered tokens cannot change any account;
deleted-account and repeated requests return a generic result. Suppression is
checked on each local capture attempt and immediately before committing the file.
Tokens and their query strings are excluded from application logs.

The hyperlink opens a confirmation page; clicking its form performs opt-out.
This is a functional local unsubscribe flow, not a claim of RFC 8058 one-click
provider support. Production unsubscribe enforcement and delivery races still
require Phase 8b verification.

Worker and Presentation use an isolated unsubscribe Data Protection purpose and
the same persisted key material. Reuse the existing DataProtectionKeys storage
through a dedicated provider, without changing the Identity/JWT configuration.
Restart verification must prove cross-host token validation and isolation from
authentication/reset capabilities. Key material remains security data excluded
from public export. Local capture and the unsubscribe route are explicitly
enabled for development with a configured loopback application origin.

## Operator invocation and evidence input

The Worker accepts an explicit user ID, ISO week, local sink root and local
application origin; missing or inconsistent options stop before writes. It offers
separate generation/capture and inventory-reconciliation actions. Reconciliation
is an explicit operator operation: generation never silently refreshes inventory.
For a newly generated digest, reconcile the changed database inventory before
capture; a stopped capture leaves the persisted digest available for a later run.

Synthetic mode must be explicitly selected and labelled as a fixture demonstration.
Recorded mode takes exact approved manifest/input paths and an independently
provided approved digest. It rejects directory discovery, held-out/evaluation-only
inputs, path escapes, symlinks, expired manifests, changed hashes, origin mixing,
missing approval references and oversized input before constructing immutable
responses. Approval for an acquisition run is not inferred to permit arbitrary
user-profile use. Only a recording explicitly authorized for this local generation
is eligible; without one, recorded invocation stops. No recorder is invoked.

The demonstration uses fabricated account/fixture inputs in a disposable local
database. It reads no existing real recordings, imports no listening history and
does not send a real subscriber's data anywhere. No new demonstration account is
inserted into the application database.

## Rendering and local capture

Render recommendations in persisted rank order. HTML escapes titles, artists,
tags and explanations; URL construction escapes path/query components and allows
only configured loopback application links and ordinary HTTPS listening/catalog
links. Reject header control characters. Both alternatives include the actual
list size, readable explanations, source attribution and the unsubscribe link.
Unsupported/legacy score snapshots remain inspectable through existing mapping;
rendering does not replay a formula or invent absent evidence.

HTML uses inline styles and system fonts. No scripts, remote images, embedded
players, remote fonts, CSS imports or tracking requests are permitted. Plaintext
contains complete readable text and ordinary URLs. MIME uses UTF-8
`multipart/alternative` with plaintext first, HTML second, CRLF serialization and
encoded text parts. A stable message identity derives from the persisted digest
ID and the explicitly configured local message domain.

Capture one final `<digest-id>.eml` under the explicit ignored local sink root.
Use a bounded temporary file in that same filesystem, flush before publishing,
and publish without overwriting an existing final file. The final message carries
the digest ID, renderer version and a fingerprint of the persisted render inputs.
Fingerprint the subscriber ID/recipient, digest ID/week, ordered track fields,
stored score/explanation inputs and configured template/link settings. Exclude
mutable lifecycle fields, feedback and audit timestamps so successful capture or
later feedback does not invalidate its own receipt. Validate the embedded
unsubscribe capability against the same account separately.
An existing file is verified against those inputs and bounded parsed content
before being accepted as the capture receipt. Conflict or malformed/truncated
content stops recovery; it is never overwritten silently. No extra HTML, text,
receipt or score-report copies are generated by default.

The initial configurable bound is 100,000 rendered UTF-8 bytes per alternative
and 400,000 serialized MIME bytes per message, including tokens and headers.
Reject overflow rather than truncate explanations. These are mechanical bounds,
not measured real-data size distributions.

## Failure windows, cancellation and recovery

| Interruption | Expected result on rerun |
|---|---|
| Before digest persistence | Phase 6 rollback/no partial digest; a rerun may generate. |
| After persistence, before local write | Load the existing digest; no provider access or reranking. |
| During temporary write | No completed message; bounded partial remains accounted for, and a new attempt uses a new partial path. |
| After final publication, before DB state update | Verify the existing final message and reconcile LocalCaptured; do not create another final message. |
| After LocalCaptured update | Verify the final message and return already captured; no regeneration or second write. |
| LocalCaptured but final message missing/conflicting | Return an integrity stop requiring operator review; do not silently recreate it. |
| Opt-out/deletion before final eligibility check | Stop local capture; do not publish a new final message. |

Rendering/configuration/integrity failures are permanent stops. Transient local
I/O failures receive at most three attempts, with cancellable delays of 250 ms
and 1 s. Each attempt checks subscriber eligibility and existing final files;
each database retry uses a fresh scope/transaction and inspects durable capture
state first. Never wrap file publication inside an EF automatic retry delegate.
Cancellation propagates before publication; after publication, recovery verifies
the artifact before reconciling database status. Store bounded reason codes in
the existing error field rather than raw exceptions, paths, tokens or recipients.

A local file and SQL commit do not share a transaction. The guarantee is one
verified completed artifact per digest in this participating local workflow,
with explicit recovery for the publication/state-update gap. External email
exactly-once delivery is not established by this mechanism.

## Storage and privacy boundaries

Final `.eml` messages are derived-data copies in the existing `copies` category.
Temporary messages must also appear in exactly one declared category; when they
share the sink directory, count them there rather than declaring overlapping
roots. Reject symlinked roots/ancestors, tracked targets, path escapes and unknown
accounting. The sink path is an explicit ignored local location, not a default
tracked output directory. Include exact MIME bytes and the worst-case outstanding
partial bytes in the reservation before writing; account for failed partials
until an operator removes them. No automatic history/partial expiration is adopted.

After reconciling the previously authorized storage follow-up, new derived files
stop at projected usage at or above **1,000,000,000 bytes (temporary/unresolved)**.
This source checkout currently enforces 80,000,000; that drift must not be mistaken
for a new capacity decision. The real limit remains a build-order step 9 decision
after hosting selection. Historical Phase 2/4 acquisition run caps remain bounded
by their exact manifests. Existing
digest inspection and opt-out/deletion/state-recovery writes continue at that
threshold. Sink capture shares the configured exclusive artifact lease with
participating local writers. Revalidate database/artifact revisions before file
publication, holding the final subscriber check against opt-out/deletion while
publishing. Operators keep recording and other artifact writes exclusive; this
does not establish distributed coordination for arbitrary nonparticipating writers.

Local message files contain an account recipient, recommendations and a protected
unsubscribe capability. They are account-owned copies. The local run procedure
must provide ownership-based inspection/export of message content and removal on
account deletion for its configured sink, while excluding token/key internals from
public export. File cleanup failures must be reported honestly; SQL deletion alone
must not claim that copies or backups were erased. The full table/column, backup
and physical-deletion audit remains Phase 8a. No copied account data is committed.

## Verification and proposed focused commits

Expected effort: substantial work across all composition/persistence boundaries,
approximately four focused implementation commit sets and an end-to-end evidence
pass. This is a new local subsystem, not a small Worker registration fix.

First reconcile the already authorized Phase 6 status/cutoff and manual-QA
follow-ups into the chosen implementation checkout, with their existing scope and
verification preserved. This is a separate prerequisite commit set; it does not
authorize the deferred popularity setting. Then use these Phase 7 commit sets:

1. **Persistent local opt-out:** Domain state, one additive migration, protected
   token and endpoints, profile/export 2.1 and local-copy ownership boundaries.
   Verify populated-database migration, idempotence, token tampering/cross-host
   validation, deletion behavior, export fields and local-copy failure reporting.
2. **Renderer and sink:** Application abstractions, HTML/plaintext/MIME and bounded
   local capture. Verify 0/1/3/5 picks, escaping, Unicode, link/header validation,
   multipart decoding, zero remote resources, input fingerprints, conflicts,
   storage limits, symlinks and partial-write accounting.
3. **Worker orchestration and recovery:** Explicit one-shot options, offline input
   adapter and database lifecycle/lease behavior. Verify repeats/concurrency,
   every failure window above, cancellation, expired/unauthorized/mixed recordings,
   fresh inventory requirements and zero outbound attempts even with credentials.
4. **Reassessment and evidence:** Disposable PostgreSQL demonstration through the
   actual Worker composition, local browser unsubscribe check, scoped/full .NET
   verification, existing documentation/security checks, CHANGELOG and PROGRESS.
   Measure captured MIME and aggregate storage bytes; qualify synthetic evidence.

Reuse existing test projects, adding only the exact Worker reference needed by
the end-to-end test assembly after plan review. Preserve existing test contracts
except the explicitly reviewed additive export/schema expectations. For future
implementation use modern-csharp-coding-standards and efcore-patterns; use Rider
MCP for diagnostics and Playwright MCP for the local unsubscribe flow. Read those
skills when implementation is authorized. No implementation tests were added or
run during this design pass.

Each verified authorized commit stays focused and local. If verification cannot
run or fails, report it and ask before committing. Local verification is separate
from developer confirmation. Evidence ends with an explicit developer decision
to proceed, narrow or revise before Phase 8 or 9.

## Authoritative references refreshed 2026-10-11

- [Last.fm terms](https://www.last.fm/api/tos): clauses 2.7, 4.3.4 and 4.4 cover
  attribution/public-page approval, the 100 MB cap/caching, and discretionary rate
  limits. Text-only email attribution acceptability remains **not verified**.
- [artist.getTopTags](https://www.last.fm/api/show/artist.getTopTags): artist-scoped
  community tags, not a claim of verified per-track descriptors.
- [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host):
  host lifecycle and dependency injection for the existing Worker.
- [ASP.NET Core Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction?view=aspnetcore-10.0)
  and [configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0):
  purpose isolation and shared persisted key configuration.
- [RFC 2046](https://www.rfc-editor.org/rfc/rfc2046): multipart alternatives represent
  the same message, ordered from plaintext to HTML.

Provider territorial/consent questions and written public-page approval remain
open. No email provider, price or external sending contract is adopted here.
