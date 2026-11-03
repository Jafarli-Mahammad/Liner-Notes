# Project status

## Current implementation

Liner Notes has guest and account discovery, taste editing, catalog search,
recommendation history and feedback, data export and deletion, and a React UI.
ListenBrainz is the default provider; Last.fm remains available through the
provider abstraction. Recommendations persist deterministic score breakdowns.

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
Phase 3 has not begun and requires separate authorization.

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
3. **Phase 5 decision:** real-data evaluation needs separate authorization, an approved development and held-out manifest, frozen scoring configuration, and documented comparison steps. Recommendation quality remains unverified. See [renewal contracts](docs/plans/contracts.md).
4. **Provider release gates:** obtain written Last.fm public-page approval and verify applicable territorial, consent, recording, attribution, and hosting requirements. The request remains a draft; confirm the repository URL before sending it. See [Phase 3 references](docs/plans/phase-3-evidence.md#authoritative-documentation) and the [release checklist](docs/plans/phase-9-release-checklist.md).
5. **Production operations:** choose hosting and delivery arrangements, validate storage limits against the chosen host, and complete the [Phase 9 release checklist](docs/plans/phase-9-release-checklist.md). Production deployment and external email delivery have not been performed.
6. **Phase 7 deployment checks:** inspect or convert any existing captured-email files and confirm persistent key-store access and backup protection before relying on encrypted capture. See [Phase 7 evidence](docs/plans/phase-7-evidence.md).
7. **External CI:** The Qodana scan workflow and configuration have been removed. GitHub Actions CI has reported failures; a successful rerun and GitGuardian incident classification remain pending. See [PR review evidence](docs/plans/2026-10-19-pr-review-fixes.md) for the historical Qodana setup and findings.

## Phase implementation status

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
