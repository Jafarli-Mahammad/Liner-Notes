# Liner Notes renewal progress

## Current status

The locally verified implementation scope is complete for Phases 1–4, 6–9. Phase 5, real-data recommendation evaluation, has not been authorized. These statements describe implementation and local verification; they do not establish catalog quality, external approvals, production readiness, or developer confirmation for each phase.

Phase 9 is the latest completed implementation. Its pinned build inputs, non-root web and Worker images, local Compose topology, and PostgreSQL-backed CI are implemented. The recorded local verification passed 478 .NET checks, 32 offline Python tests, four helper test methods, and all eight container lifecycle groups. GitHub Actions execution and remote scanning are not verified. See [Phase 9 evidence](docs/renewal/phase-9-evidence.md) and the [local runbook](docs/renewal/phase-9-local-run.md).

## Remaining work

- **Guest discovery: design and sequential execution approved; C1–C7 mechanics locally verified.**
  The bounded synthetic catalog probe passed 15 checks, and the baseline .NET
  suite passed 516 tests with zero skips; the additive profile/batch foundation
  subsequently passed 526 .NET checks with zero skips. Profile ownership cutover
  subsequently passed 529 .NET checks with zero skips. Guest lifecycle mechanics
  subsequently passed 541 .NET checks with zero skips. Local catalog/search mechanics
  subsequently passed 564 .NET checks with zero skips. Canonical album taste and
  bounded public preparation subsequently passed 579 .NET checks with zero skips.
  Durable job/admission mechanics subsequently passed 592 .NET checks with zero skips.
  C8 shared-cache/history, batch preparation/persistence and submission/runner, explicit lookup and discovery endpoint/weekly adapter mechanics and provider/storage boundary checks passed 643 .NET checks with zero skips;
  account writes, reclamation, maintenance hosting and guest activity are integrated.
  Website consumers are opt-in and reject concurrent local email capture while
  its writer integration remains open. The website flow and constrained-host
  measurements remain unfinished.
  Three isolated SQL measurements support
  continuing with the profile/batch foundation; they do not prove real catalog
  coverage, API latency, or VPS capacity. C8–C10, integrated generation/deletion/quota races and the guest product flow remain.
  A live catalog acquisition manifest is not approved. See the [evidence](docs/renewal/guest-discovery-evidence.md),
  [design](docs/superpowers/specs/2026-10-16-guest-discovery-design.md), and
  [execution plan](docs/superpowers/plans/2026-10-16-guest-discovery.md).

- **Confirm the ListenBrainz default.** The adapter, coherent provider selection,
  approved replay and explicitly gated live generation are implemented; Last.fm
  remains available. The full local suite passed 516 checks, and a bounded
  catalog smoke returned four hydrated candidates. Developer confirmation,
  deployment-specific usage/license review and recommendation-quality evaluation
  remain open. Per-user choice and history imports are deferred. See
  [provider evidence](docs/renewal/listenbrainz-provider-evidence.md) and
  [operations](docs/renewal/listenbrainz-provider-runbook.md).

1. **Review and confirm the local implementation.** Developer confirmation is still recorded as pending for the phase evidence, including the latest Phase 9 work. Review the evidence linked below and update this status when confirmed.
2. **Run the live Phase 4 pilot.** Approve a concrete run manifest and reconcile the current shared-storage inventory first. Then record the frozen pilot inputs and complete the data-shape checks and blind ratings. No live pilot has been run; fixture results establish tooling mechanics only. See [Phase 4 evidence and run procedure](docs/renewal/phase-4-evidence.md).
3. **Decide whether to authorize Phase 5 real-data evaluation.** It requires a separate decision to proceed, an approved manifest covering development and held-out recordings, frozen scoring configuration, and the documented comparison and formula-choice steps. Recommendation quality remains unverified. See [renewal contracts](docs/renewal/contracts.md).
4. **Resolve Last.fm use and attribution gates before release.** Written public-page approval is missing. Territorial conditions, consent and transfer requirements, recording/research permissions, attribution placement and hosting, and text-only email attribution remain unresolved or unverified. The request is still a draft; confirm the repository URL before sending it. No universal numeric request allowance is documented in the current project notes. See [Phase 3 references](docs/renewal/phase-3-evidence.md#authoritative-documentation) and the [release checklist](docs/renewal/phase-9-release-checklist.md).
5. **Make production operating decisions.** Choose hosting and delivery arrangements, replace the temporary 1 GB shared-storage cutoff with a supported capacity, and settle operational decisions and release gates in the [Phase 9 checklist](docs/renewal/phase-9-release-checklist.md). Production deployment and external email delivery have not been performed.
6. **Verify deployment-specific Phase 7 security conditions.** Existing captured-email files or copies have not been inspected or converted. Confirm persistent key-store access and backup protection for any deployment before relying on encrypted capture. See [Phase 7 evidence](docs/renewal/phase-7-evidence.md).
7. **Complete external execution checks.** Run the GitHub Actions workflow and remote scanning; they have not been verified from this checkout. Local CI startup and cleanup were exercised as recorded in [Phase 9 evidence](docs/renewal/phase-9-evidence.md).

## Phase implementation status

| Phase | Current status | Evidence |
|---|---|---|
| 1. Baseline and security | Implemented and locally verified; developer confirmation pending | [Phase 1](docs/renewal/phase-1-evidence.md) |
| 2. Harness mechanics | Implemented and locally verified; mechanics only | [Phase 2](docs/renewal/phase-2-evidence.md) |
| 3. Honest ingestion | Implemented and locally verified; mechanics only | [Phase 3](docs/renewal/phase-3-evidence.md) |
| 4. Pilot tooling | Implemented and locally verified; live pilot remains open | [Phase 4](docs/renewal/phase-4-evidence.md) |
| 5. Real-data evaluation | Not authorized; recommendation quality unverified | [Renewal contracts](docs/renewal/contracts.md) |
| 6. Recommendation generation and feedback | Implemented and locally verified; quality and capacity unverified | [Phase 6](docs/renewal/phase-6-evidence.md) |
| 7. Worker and local email capture | Implemented and locally verified; deployment-specific checks remain open | [Phase 7](docs/renewal/phase-7-evidence.md) |
| 8. Privacy and scheduling | Implemented and locally verified; developer confirmation and external delivery remain open | [Phase 8a](docs/renewal/phase-8a-evidence.md), [Phase 8b](docs/renewal/phase-8b-evidence.md) |
| 9. Reproducibility and release preparation | Implemented and locally verified; external CI and release gates remain open | [Phase 9](docs/renewal/phase-9-evidence.md) |

## Active product and evidence constraints

- V1 remains popularity-neutral. Do not claim a new recommendation algorithm or ownership of upstream similarity data. Synthetic fixtures and pilot mechanics do not establish recommendation quality.
- Real recordings and evaluation reports require separate approval, stay local, and must remain gitignored and untracked. Do not tune against held-out data.
- Last.fm and ListenBrainz remain provider-boundary concerns. ListenBrainz is the configured default; Last.fm is retained for future user choice. Live acquisition requires explicit configuration and usage review. Listening-history imports remain deferred. Streaming search links are not Spotify API integration.
- The temporary 1,000,000,000-byte storage stop is not a supported capacity decision. No purchase, provisioning, public deployment, or external email delivery is authorized by this progress record.
- Product decisions and status live here; verified preferences and gotchas live in [MEMORY](MEMORY.md). User-visible changes belong in [CHANGELOG](CHANGELOG.md).
