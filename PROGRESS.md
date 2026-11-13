# Project status

## Current implementation

Liner Notes has guest and account discovery, taste editing, catalog search,
recommendation history and feedback, data export and deletion, and a React UI.
ListenBrainz is the default provider; Last.fm remains available through the
provider abstraction. Recommendations persist deterministic score breakdowns.

### Publication/account email — 2026-11-13

The approved design is implemented in the repository: public privacy/source/support
pages, operator/rights gates, Identity confirmation and password recovery,
protected durable mail dispatch, a replaceable Resend adapter, authenticated
delivery events, suppression, account preferences and bounded Production scheduling.
Email metadata is covered by storage accounting, export 2.8 and account deletion.
External delivery is disabled by default. See [activation and operation](docs/operations/account-email.md)
and [verification/limits](docs/operations/account-email-evidence-2026-11-13.md).

Actual operator/contact, domain, sender verification, provider account/credentials,
tracking configuration, source-use decisions and real recipient-server delivery
are still activation gates. No provider enquiry, purchase, external message or
public deployment was performed. Developer acceptance remains pending.

### Production beta engineering — 2026-11-01

The approved beta design is implemented: clean-tree React images, guarded
Production configuration, private/resource-bounded Compose, separate database
roles, persistent keys, writer-agreed storage policy, serialized backup/restore
and compatible upgrades, bounded live weekly generation and operational probes.
The patched backend passed 909 tests with zero failures/skips. Local recovery
compared 29 tables and preserved authentication, protected tokens and export;
a separate recovery retained five real-provider picks and their score breakdowns.
The final real-provider browser journey passed without a cleanup warning. Scanning
caught framework and SDK-library advisories; the patched final web, Worker and
migration-tool images have zero high/critical findings. Final HTTPS/proxy,
compatible-upgrade and recovery acceptance passed; the real-history recovery
retained ten weekly picks and ten stored breakdowns across two weeks.
Mail delivery and automatic all-account scheduling
are excluded. VPS/domain selection, public TLS/firewall, off-host recovery, key
custody, delivered alerts and developer acceptance remain outstanding.
No production deployment, push or release publication has occurred. See
[the engineering evidence](docs/operations/production-beta-evidence.md) and
[the execution plan](docs/superpowers/plans/2026-11-01-production-beta.md).

### Residue quality renewal — 2026-11-03

Phase 1 (`5a91411`) is technically completed but is not personally validated or
formally accepted. Its baseline, evidence document, and private review are
preserved unchanged; listening is deferred until after Phase 5.

Phase 2 (consistently usable favorites) is implemented and locally verified:
fresh evidence is independent of acquisition windows, entity seed rotation is
fair, and owner-bound receipts/export include evidence coverage. 151 targeted
.NET cases and Playwright MCP application checks passed. Subjective quality is
unverified. See [Phase 2 evidence](docs/plans/residue/2026-11-03-phase-2-favorite-evidence.md)
and [the plan](docs/plans/residue/2026-11-02-recommendation-quality-renewal-plan.md).
Phase 3 (durable and reversible feedback) is implemented and locally verified:
compact lifetime votes retain learned artists, recent receipts supply context,
notes preserve musical recency, and repeated dislikes cautiously soften learned
preferences. 304 automated cases and 30 Playwright MCP checks passed; all 65
original receipts still replay exactly. No new persistence or provider was added.
See [Phase 3 evidence](docs/plans/residue/2026-11-03-phase-3-feedback-memory.md).
Subjective quality and developer confirmation remain outstanding.

Phase 4 (recording evidence and connections) is implemented and locally verified:
recording relationships survive missing tags with explicit limitations, direct
recording tags avoid unnecessary artist requests, and malformed recording tags
remain missing evidence. 497 automated cases passed; the frozen genre contract
is unchanged and 65 original receipts replay exactly. The limited live sample
retained all 30 candidates and demonstrated no tagless yield gain. Optional
credits and album routes are deferred; AcousticBrainz integration was rejected
for this phase. See [Phase 4 evidence](docs/plans/residue/2026-11-03-phase-4-recording-connections.md).
Developer confirmation, subjective quality and broader coverage remain open.
Phase 5 (coordinated acquisition and connected selection) is implemented and
locally verified. Exploration changes bounded public acquisition limits/routes;
selection rejects novelty-only padding, combines soft controls, diversifies by
canonical credits and recent exposure, and records replayable decisions and
shortfalls. The UI handles empty and short batches. 648 automated cases and 32
Playwright MCP checks passed; all 75 preserved historical receipts still replay
numerically, and five new browser receipts replay selection exactly. The fixed
sparse pools now show 16 supported picks instead of 80 padded picks, with unchanged
paired numerical scores; this is synthetic correctness evidence, not listening
validation. New large receipts remain within the existing storage cap, with
backward-compatible readers. See [Phase 5 evidence](docs/plans/residue/2026-11-03-phase-5-connected-selection.md).
Developer confirmation and subjective quality remain open.

Phase 6 was authorized and its integrated evaluation is implemented and locally
verified (2026-11-04). Frozen development/held-out public profiles used 54/80
allowed HTTP dispatches; all 83 same-pool numerical scores matched the actual
Phase 1 Domain binary. Integrated selection showed 34/40 slots versus 35/40,
without treating shorter batches as quality proof. All 980 tests and 61
Playwright MCP assertions passed, including account claim, feedback, isolation,
restart, export and deletion. Historical receipts and new selection receipts
replay exactly. No policy coefficients changed; zero completed listening ratings
means subjective quality remains unverified. See [Phase 6 evidence](docs/plans/residue/2026-11-04-phase-6-integrated-evaluation.md).
Developer confirmation, representative live capacity and release gates remain open.

## Current documentation

- [Developer setup](README-Developer.md) and [contributing](CONTRIBUTING.md)
- [Architecture](docs/architecture/README.md)
- [Live local operation](docs/plans/local-live-run.md)
- [Storage and recovery](docs/plans/guest-discovery-storage.md)
- [Production operation](docs/operations/production-beta.md)
- [Production engineering evidence](docs/operations/production-beta-evidence.md)
- [Provider operation and release gates](docs/plans/listenbrainz-provider-runbook.md)

Completed investigation reports, execution plans and design captures were
removed during repository cleanup. Their original records remain in Git history.
Retained evidence documents distinguish local verification from production and
recommendation-quality claims.

## Remaining renewal work

1. **Developer confirmation:** review locally verified phase evidence and confirm the implementation.
2. **Historical Phase 4 pilot:** commit `f26d14e` records completed live acquisition and A–D analysis on 2026-10-08; complete blind ratings, formula adoption, and developer confirmation were not established. New acquisition still requires its applicable approvals. See [Phase 4 evidence and run procedure](docs/plans/phase-4-evidence.md).
3. **Historical pilot evaluation decision:** the original delivery plan's real-data evaluation needs separate authorization, an approved development and held-out manifest, frozen scoring configuration, and documented comparison steps. Recommendation quality remains unverified. See [renewal contracts](docs/plans/contracts.md).
4. **Provider release gates:** obtain written Last.fm public-page approval and verify applicable territorial, consent, recording, attribution, and hosting requirements. The request remains a draft; confirm the repository URL before sending it. See [Phase 3 references](docs/plans/phase-3-evidence.md#authoritative-documentation) and the [release checklist](docs/plans/phase-9-release-checklist.md).
5. **Production operations:** choose hosting and delivery arrangements, validate storage limits against the chosen host, and complete the [Phase 9 release checklist](docs/plans/phase-9-release-checklist.md). Production deployment and external email delivery have not been performed.
6. **Phase 7 deployment checks:** inspect or convert any existing captured-email files and confirm persistent key-store access and backup protection before relying on encrypted capture. See [Phase 7 evidence](docs/plans/phase-7-evidence.md).
7. **External CI:** The Qodana scan workflow and configuration have been removed. GitHub Actions CI has reported failures; a successful rerun and GitGuardian incident classification remain pending. See [PR review evidence](docs/plans/2026-10-19-pr-review-fixes.md) for the historical Qodana setup and findings.

## Phase implementation status

This table tracks the original delivery plan; Residue renewal phase statuses are above.

| Phase | Current status | Evidence |
|---|---|---|
| 1. Baseline and security | Implemented and locally verified; developer confirmation pending | [Phase 1](docs/plans/phase-1-evidence.md) |
| 2. Harness mechanics | Implemented and locally verified; mechanics only | [Phase 2](docs/plans/phase-2-evidence.md) |
| 3. Honest ingestion | Implemented and locally verified; provider gates remain | [Phase 3](docs/plans/phase-3-evidence.md) |
| 4. Pilot tooling | Tooling locally verified; historical acquisition/analysis recorded, complete listening evaluation unverified | [Phase 4](docs/plans/phase-4-evidence.md) |
| 5. Real-data evaluation | Not authorized; recommendation quality unverified | [Renewal contracts](docs/plans/contracts.md) |
| 6. Recommendation generation and feedback | Implemented and locally verified; quality and capacity unverified | [Phase 6](docs/plans/phase-6-evidence.md) |
| 7. Worker and local email capture | Implemented and locally verified; deployment-specific checks remain open | [Phase 7](docs/plans/phase-7-evidence.md) |
| 8. Privacy and scheduling | Implemented and locally verified; developer confirmation and external delivery remain open | [Phase 8a](docs/plans/phase-8a-evidence.md), [Phase 8b](docs/plans/phase-8b-evidence.md) |
| 9. Reproducibility and release preparation | Implemented and locally verified; external CI and release gates remain open | [Phase 9](docs/plans/phase-9-evidence.md) |

## Product and evidence constraints

- V1 remains popularity-neutral. Do not claim a new recommendation algorithm or ownership of upstream similarity data. Synthetic fixtures and pilot mechanics do not establish recommendation quality.
- Real recordings and evaluation reports require separate approval, stay local, and must remain gitignored and untracked. Do not tune against held-out data.
- Last.fm and ListenBrainz remain provider-boundary concerns. Live acquisition needs explicit configuration and usage review. Listening-history imports remain deferred. Streaming search links are not Spotify API integration.
- The temporary 1,000,000,000-byte managed-storage stop is not a VPS-capacity decision. Hosting, provisioning, public deployment, and external email delivery are not authorized by this progress record.
- Product decisions and status live here; verified preferences and gotchas live in [MEMORY](MEMORY.md). User-visible changes belong in [CHANGELOG](CHANGELOG.md).
