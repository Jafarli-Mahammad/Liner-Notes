# Publication readiness review — 2026-11-13

**Reviewed revision:** `471bbeeb9d0841c965e32458fe74d4ebd413fff9` on `Prism`.
**Scope:** repository analysis and targeted local checks. No deployment, external mail, account creation, purchases, push or release publication was performed.
**Assessment:** the repository has a substantial locally verified beta foundation. The available evidence does **not** support declaring the public service ready. The advertised weekly-email product has missing implementation as well as deployment and acceptance gates.

This report distinguishes **missing implementation**, **missing operational setup**, and **unverified acceptance**. An absent record is not proof that an operator has never completed something elsewhere. The developer must confirm acceptance; this review does not provide that confirmation.

## 1. Decide what is being published

| Publication scope | What is needed |
| --- | --- |
| Source code / development announcement | The repository is already described as open source. Keep the active-development status and explicit limitations. Review source/license notices and actual CI before tagging a version. A source release is not evidence of a working hosted service. |
| Limited hosted discovery beta | Close B1–B7 below, explicitly disclose the absence of weekly delivery, define an initial user limit and support arrangement, and resolve account recovery if accounts are offered. |
| Weekly-email V1 | Close the beta gates **and** W1–W3. Prove the entire signup → confirmation → eligible schedule → saved picks → delivered email → unsubscribe journey. |

The smallest plausible launch is a deliberately limited on-demand beta. Do not market local email capture or manual per-user generation as a weekly delivery service.

## 2. What is already present

- The Domain/Application/DataAccess/Infrastructure/Presentation/Worker structure, provider abstraction, deterministic scoring and persisted recommendation explanations.
- Guest/account discovery, catalog search, taste editing, feedback, history, guest import, export and deletion paths in the React app and API.
- Identity-based authentication, password length and lockout settings, request rate limiting, trusted proxy configuration, HTTPS/HSTS support and guarded Production configuration. Their presence is not a penetration-test result.
- A production Compose topology, separate database roles, persistent key storage, bounded workers, release-image builds, backup/restore tooling, operational probes and systemd templates.
- Extensive backend and browser tests and historical recovery evidence. Existing tests should be rerun against the release revision rather than replaced with a new test framework.
- The November 9 QA findings have follow-up implementation evidence. They are not seven currently open bugs merely because the original audit remains in the repository. Developer acceptance is still pending.

Evidence: [architecture and product status](../../README.md), [current progress](../../PROGRESS.md), [production runbook](production-beta.md), [production evidence](production-beta-evidence.md), [QA follow-up](../../qa-review-2026-11-09/fixes/README.md).

## 3. Gates before a public hosted beta

### B1. Public hosting and domain setup — operational setup missing/unverified

**Evidence:** the [production runbook](production-beta.md) and its [remaining gates](production-beta-evidence.md#deployment-dependent-and-unverified) still require a VPS, domain, off-host repository and alert destination. The topology and [Caddy configuration](../../deploy/Caddyfile) already exist.

**Needed:** choose the host and domain, set the public origin and private runtime configuration, configure DNS, certificate issuance/renewal, firewall and IPv6 exposure, persistent volumes and operator access. Account for the combined web/database/proxy/OS resource needs, not just individual container limits.

**Close when:** the reviewed image set runs on the intended host; external HTTPS, redirects, cookies, deep links, private database exposure and reboot recovery are checked. Record the deployed revision and immutable image IDs. Public deployment still requires explicit authorization.

### B2. Provider/data/artwork rights — release decision missing

**Evidence:** the [provider runbook](../plans/listenbrainz-provider-runbook.md), [source policy](../plans/guest-discovery-sources.md) and [release checklist](../plans/phase-9-release-checklist.md) retain unresolved public-use and attribution gates. `LiveUseApproved` is configuration, not a legal permission document. Last.fm is optional and is not the default production provider.

**Needed:** document the actual enabled data routes and their approved uses, retention, redistribution and attribution conditions. Include MusicBrainz supplementary tags, ListenBrainz-derived evidence, artwork and exported data. Keep exact artwork provenance and decoded-image checks; do not fill missing covers with name-matched substitutes. If Last.fm remains disabled, its specific approval gate is conditional on enabling its data, not an automatic blocker for every ListenBrainz-only deployment.

**Current source verification:** MusicBrainz distinguishes CC0 core data from supplementary data under CC BY-NC-SA 3.0; a separate deployment/licensing decision is needed for the actual use. Its API guidance describes an average one request/second per source IP unless otherwise agreed. ListenBrainz requires clients to follow rate-limit headers and send an identifiable User-Agent. These facts do not establish that this project's complete use is approved. Sources checked November 13: [MusicBrainz data license](https://musicbrainz.org/doc/About/Data_License), [MusicBrainz pacing](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting), [ListenBrainz API](https://listenbrainz.readthedocs.io/en/latest/users/api/index.html).

Last.fm's terms page could not be fetched in this review (HTTP 402). Its public-page approval requirement is recorded in the repository but **not independently reverified here**. Obtain current terms and the real approval reference before enabling the affected use. No universal Last.fm numeric request allowance is asserted.

**Close when:** the release has a provider/data/asset decision record, actual attribution placements and any required permission references. Review the public feed and JSON export as well as the private discovery page.

### B3. Public privacy information and support — product/operational gap

**Evidence:** [Account](../../src/Presentation/ClientApp/src/features/account/Account.tsx) offers export/deletion; [privacy design documentation](../plans/guest-discovery-privacy.md) exists. The reviewed [browser routes](../../src/Presentation/ClientApp/src/main.tsx) expose no dedicated privacy, terms or support page, and the navigation does not provide a public privacy notice. A repository design document is not an accessible notice for ordinary visitors.

**Needed:** publish a concise notice describing account/guest data, cookies, public versus private recommendation information, provider requests, retention, backups, deletion limitations and operator contact. Define a support route and how failed deletion/cleanup is handled. Decide applicable service terms and jurisdiction with appropriate review; this report makes no legal-compliance determination.

**Close when:** visitors can reach this information before signup, actual configuration matches its claims, and the operator has verified export/deletion and backup-retention behavior on the intended deployment. Existing privacy controls should be retained, not rebuilt.

### B4. Independent recovery, key custody and delivered alerts — operational acceptance missing

**Evidence:** [production evidence](production-beta-evidence.md) records local encrypted recovery, not off-host recovery or independent credential custody. [Timer templates](../../deploy/systemd/linernotes-status.timer) and status/backup commands already exist.

**Needed:** configure the off-host encrypted repository, backup schedule, key custody, access protection, retention and tested notification destination. Include PostgreSQL/WAL, disk/inode headroom, OOM, storage admission failures, job backlog and TLS expiry in the operating checks. Choose recovery point/time targets; local fixture timing is not a guarantee.

**Close when:** restore into a clean independent environment using the retained keys; verify login, tokens, persisted picks, explanations, export and deletion; trigger a failure and receive its alert. Verify reboot starts the intended services/timers. Record who can recover the service if the original host is lost.

### B5. Representative live capacity and abuse budget — acceptance missing

**Evidence:** [resource evidence](../plans/guest-discovery-capacity.md) uses a small largely synthetic catalog and explicitly excludes live-provider latency claims. [November discovery repair notes](../../MEMORY.md) show that successful preparation previously did not guarantee final persistence. Current fixes must be included in new release evidence.

**Needed:** define the first cohort size and acceptable generation/queue latency, then measure cold and warm real-provider requests, catalog search, accumulated history, simultaneous guest/account work, provider backoff/outage, disk exhaustion and restart recovery. Verify web and Worker share pacing coordination on the production IP. Measure successful persisted batches and explanations, not only HTTP acceptance or prepared candidates.

**Close when:** a representative bounded run reports success/failure counts, p50/p95 latency, queue wait, upstream requests, CPU/memory, physical disk and recovery results. Set a justified admission/cohort limit. The managed storage quota is not a complete VPS capacity plan.

### B6. Current release verification and security evidence — acceptance missing

**Evidence:** [CI](../../.github/workflows/ci.yml) defines locked restore/build/tests, PostgreSQL checks, production image scanning and recovery. The historical exact-image acceptance in [production evidence](production-beta-evidence.md) refers to an earlier source tree, not reviewed HEAD. The separate browser suites exist, but the CI workflow has no explicit command running the general React Playwright regression suite.

**Needed:** obtain green CI for the intended release SHA; rebuild and scan the exact current web/Worker/migration images; rerun required PostgreSQL suites, proxy/browser and recovery acceptance. Add or explicitly schedule the existing frontend regression suite as a release gate, including its host/browser prerequisites. Review the status of the historically mentioned secret-scanning incident rather than assuming either a confirmed leak or a resolution. A default-secret checker is not a complete repository-history secret scan.

**Environment limit found today:** `global.json` pins SDK `10.0.403`; this machine has `10.0.113` and `8.0.132`. `dotnet --version` fails SDK resolution, so backend tests, migrations and .NET dependency audit were not rerun. Install the pinned SDK for release checks; do not change the project pin merely to make this review pass. GitHub CLI is absent, so remote CI was not checked with it; the current remote run status remains unverified.

**Close when:** attach current-SHA test results without unexpected skips, current package/container/secret scan results and clean-tree image identities. Keep MediatR 12.5.0's approved licensing exception. No dependency removal, package upgrade or exploitable dependency path is proposed or established by this report.

### B7. Recommendation quality and developer acceptance — acceptance missing

**Evidence:** [renewal Phase 6](../plans/residue/2026-11-04-phase-6-integrated-evaluation.md) and [PROGRESS](../../PROGRESS.md) distinguish numerical replay and integration correctness from listening quality. Completed subjective listening ratings and representative release acceptance remain open in those records.

**Needed:** conduct a small documented listening review across representative tastes, including sparse/ambiguous inputs, and confirm the complete public user journey personally. Agree on what makes the initial beta useful and what failures are acceptable. Do not tune on held-out profiles or repurpose private exploratory listening notes.

**Close when:** the developer accepts the running release and records the evaluation limits. A small exploratory beta may explicitly defer stronger quality claims; a claim that the recommendations are proven better requires evidence that does not currently exist in these records.

## 4. Missing implementation for weekly-email V1

### W1. Email verification and account recovery

**Confirmed code gap:** [registration](../../src/Presentation/Services/AuthService.cs) sets `EmailConfirmed = false` (line 67). [WeeklyScheduleStore](../../src/DataAccess/Persistence/Repositories/WeeklyScheduleStore.cs) requires `identity.Users.EmailConfirmed` for eligibility. The [AuthController](../../src/Presentation/Controllers/AuthController.cs) exposes register/login/refresh/session/logout/me, with no public confirmation or password-reset endpoints; [AuthForm](../../src/Presentation/ClientApp/src/features/account/AuthForm.tsx) has no corresponding user journey.

New ordinary signups therefore cannot become eligible through the exposed confirmation flow, because that flow is absent. There is a `ResetPasswordAsync` service helper; that does not supply token issuance, delivery, a recovery endpoint or recovery UI.

**Needed:** secure confirmation issuance, delivery, consumption, expiry and resend handling; a complete password-recovery flow with bounded requests and safe token handling. Verify that confirmation changes eligibility, unsubscribe removes eligibility, and expired/reused tokens fail. Decide password-change/session-revocation behavior. Do not bypass confirmation by marking arbitrary registrations verified.

**Launch relevance:** confirmation blocks weekly service; self-service recovery or an explicitly accepted recovery policy should be resolved before broad account signup. An intentionally guest-only beta can defer account work if accounts are actually excluded from that launch.

### W2. External email transport and delivery lifecycle

**Confirmed code gap:** [LocalEmailOptions](../../src/Infrastructure/Email/LocalEmailOptions.cs) requires a loopback origin. [LocalEmailSink](../../src/Infrastructure/Email/LocalEmailSink.cs) publishes encrypted local captures. The [README](../../README.md) and production contract explicitly exclude external delivery. Local capture must not be counted as delivered mail.

**Needed:** choose and verify an affordable provider's current terms/limits, implement transport behind the existing application boundaries, configure sender/domain authentication and public links, and handle retry/idempotency, delivery failures, bounces and suppression as supported by that provider. Preserve consent, no tracking pixels, unsubscribe and data minimization. Verify the unsubscribe experience from a real delivered message and assess applicable mailbox-provider requirements before implementation.

**Close when:** a real opted-in confirmed test account receives the correct digest; delivery status is truthful; duplicate execution does not produce duplicate messages; unsubscribe stops future sends; failed delivery is visible and recoverable.

### W3. Production weekly scheduling

**Confirmed code gap:** scheduling logic and synthetic scheduled QA exist, but [ProductionWorkerOptions](../../src/Worker/ProductionWorkerOptions.cs) permits bounded explicit live `generate` operations, and the [production runbook](production-beta.md) explicitly excludes automatic all-account scheduling. Backup/status timers are not a weekly digest scheduler.

**Needed:** authorize and wire the existing scheduling logic into an appropriate production execution model. Reuse its eligibility/idempotency/lease behavior; address time zones, missed runs, overlap, retry, unsubscribe/deletion races and the shared provider budget. Couple delivery to the saved digest rather than recomputing different picks on a mail retry.

**Close when:** an unattended scheduled run selects confirmed opted-in users, persists one correct weekly digest each, delivers only eligible mail, survives restart and does not duplicate completed work. Prove a second week and a retry, not just one manual `generate` call.

## 5. Documentation and launch polish

These are smaller work items, not justification for a redesign:

- Reconcile stale release documents: the [Phase 9 checklist](../plans/phase-9-release-checklist.md) and [provider runbook](../plans/listenbrainz-provider-runbook.md) still describe the Worker as Development-only, while the current production worker and operations runbook support explicit live generation. Retain the distinction that all-account scheduling is still excluded.
- Make one current release checklist link to the exact evidence for each gate; do not reuse old image or test counts as evidence for today's revision.
- Confirm a first-time visitor understands the product and has a path from an empty public feed into discovery. Verify source attribution, fallback artwork, keyboard/screen-reader use and supported mobile/browser behavior on the release host. Existing browser tests and QA evidence provide a starting point; no new browser audit was run here.
- Audit launch promises: per-user provider choice and listening-history import remain deferred; many service links are searches rather than canonical direct playback links ([MusicLinks](../../src/Presentation/ClientApp/src/components/MusicLinks.tsx)). Match the wording to actual behavior. These features need not block a clearly scoped beta.
- Choose a version/release note and supported release policy. [SECURITY.md](../../SECURITY.md) currently supports the latest `Prism` development commit, not a stable release line. Retain the AGPL and bundled dependency/asset notices.

## 6. Checks performed in this review

| Check | Current result | What it establishes |
| --- | --- | --- |
| `python3 -m unittest discover -s scripts/ops -p 'test_*.py'` | 14 passed | Operator helper regressions; not actual hosted recovery |
| `python3 -m unittest discover -s scripts/checks -p 'test_production*.py'` | 11 passed | Production-checker regressions; not execution of all acceptance harnesses |
| `python3 -m unittest discover -s scripts/checks -p 'test_phase9*.py'` | 10 passed | Phase 9 tooling/contracts/docs regressions |
| `python3 scripts/checks/secret_defaults.py` | Passed | Default credential policy check |
| `python3 scripts/checks/renewal_docs.py` | Passed | Existing documentation contract check |
| `npm run typecheck` and `npm run build` in ClientApp | Passed | Current frontend types/build/license-generation path using installed dependencies; no fresh `npm ci` performed |
| `npm audit --omit=dev --json` and `npm audit --json` | Both report zero advisories | Current npm registry audit for this dependency tree; not an assurance about .NET, container or application security |
| Backend build/tests/migrations/.NET audit | Not run: pinned SDK unavailable | Prior passing backend records remain historical evidence |
| Remote CI | Not verified; `gh` unavailable | No claim that latest remote CI passes or fails |
| Browser, live provider journey, container scan, public TLS, delivered mail, off-host restore, load test | Not run in this review | Require separate acceptance runs |

No application behavior, dependencies or deployment configuration were changed for this analysis. The report and workflow observation are documentation-only changes.

## 7. Suggested work order and effort

Effort is relative, not a delivery-date promise. External permissions, provider onboarding and infrastructure choices can dominate elapsed time.

| Order | Focused work package | Relative effort | Verification/commit boundary |
| --- | --- | --- | --- |
| 1 | Agree beta versus weekly scope; reconcile release gates and rights decisions | Small–medium, external decisions variable | Documentation/decision commit; actual approval references and current checklist |
| 2 | Public privacy/support information and account recovery policy | Medium | Focused UI/API changes only as authorized; notice review and account/export/deletion acceptance |
| 3 | Provision beta host, independent backups, key custody and alerts | Medium, environment dependent | Operator configuration/runbook evidence; external HTTPS, reboot, clean-host restore and delivered alert |
| 4 | Restore pinned build environment; rerun exact-release CI, scans, browser and live capacity gates | Medium | Current-SHA evidence; justified initial cohort limit; developer acceptance |
| 5 | If weekly V1: confirmation/recovery, mail adapter and scheduler, as separate slices | Large | Separate focused commits and tests for account lifecycle, delivery and scheduling; final two-week/retry journey |

**Release decision:** do not announce the weekly-email service as ready yet. A limited on-demand beta is the narrower path, after its actual deployment, rights, privacy, recovery, capacity and acceptance gates are closed. None of the findings calls for custom ML, a new recommendation engine, payments, social features or a broad architectural rewrite.
