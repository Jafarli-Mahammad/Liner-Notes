# Liner Notes renewal progress

## Current status

Phases 1–4 and 6–9 have locally verified implementations. Phase 5 real-data recommendation evaluation is not authorized. Local implementation evidence does not establish recommendation quality, external provider approval, production readiness, or developer confirmation.

Phase 9 includes pinned build inputs, non-root web and Worker images, local Compose topology, and PostgreSQL-backed CI. Recorded local verification passed 478 .NET checks, 32 offline Python tests, four helper test methods, and all eight container lifecycle groups. GitHub Actions execution and remote scanning have not been verified. See [Phase 9 evidence](docs/renewal/phase-9-evidence.md) and the [local runbook](docs/renewal/phase-9-local-run.md).

## Guest discovery

The guest-first discovery design and sequential implementation were approved. The first onsite flow now supports artist, album, and style-tag selection; immediate synthetic recommendations with persisted explanations; explicit catalog lookup; feedback; export; registration with guest data preservation; explicit import into an existing account; and guest data deletion.

The implementation passed 646 .NET checks with zero skips and 12 synthetic browser checks. The browser run covered stale search responses, artist and album selection, explicit lookup, generation, return visits, feedback, export, registration/import, cooldown, sparse results, and deletion. The first synthetic batch took about 4.4 seconds on the development machine. This is fixture evidence only; it does not measure live-provider latency or VPS capacity.

Provider selection uses registered polymorphic bundles. Application workflows do not depend on Last.fm or ListenBrainz implementations. Persistence-facing discovery contracts are in Application and EF Core/PostgreSQL implementations are in DataAccess. A database change still requires a schema and data migration, but should not require rewriting product workflows.

Remaining guest-discovery work:

- Measure memory, CPU, disk use, latency, and overload behavior under the constrained-host profile. No VPS-capacity claim has been established.
- Bounded public cleanup is now implemented and PostgreSQL-verified: each maintenance pass removes at most 100 expired evidence entries, 100 catalog records with their search terms, and ten empty expired publications. It preserves the active catalog, fresh lookups, resumable imports, and canonical identities used by private taste/history. Seven catalog integration checks passed after this change.
- Weekly digest persistence, scheduler writes and unsubscribe updates now use discovery storage coordination when its runtime is enabled. The PostgreSQL regression generated a weekly digest, advanced its schedule, unsubscribed, and then successfully created another guest profile while verifying storage integrity. Optional local email capture remains a separate integration gate: both composition roots reject its concurrent use with the guest runtime. No external email transport is configured.
- A live catalog acquisition manifest is not approved, and no real catalog has been imported. Catalog coverage, recommendation quality, and live-provider latency remain unverified.

See the [guest discovery evidence](docs/renewal/guest-discovery-evidence.md), [storage notes](docs/renewal/guest-discovery-storage.md), [design](docs/superpowers/specs/2026-10-16-guest-discovery-design.md), [execution plan](docs/superpowers/plans/2026-10-16-guest-discovery.md), and [synthetic local runbook](docs/renewal/guest-discovery-local-run.md).

## Provider status

ListenBrainz is the configured default; Last.fm remains available behind the provider abstraction. Live use is explicitly gated. Per-user provider choice and listening-history imports are deferred. Developer confirmation, deployment-specific usage and license review, and recommendation-quality evaluation remain open. See the [provider evidence](docs/renewal/listenbrainz-provider-evidence.md) and [operations guide](docs/renewal/listenbrainz-provider-runbook.md).

## Remaining renewal work

1. **Developer confirmation:** review locally verified phase evidence and confirm the implementation.
2. **Phase 4 live pilot:** approve a concrete run manifest, reconcile shared-storage inventory, freeze inputs, and complete data-shape checks and blind ratings. No live pilot has been run; fixtures establish mechanics only. See [Phase 4 evidence and run procedure](docs/renewal/phase-4-evidence.md).
3. **Phase 5 decision:** real-data evaluation needs separate authorization, an approved development and held-out manifest, frozen scoring configuration, and documented comparison steps. Recommendation quality remains unverified. See [renewal contracts](docs/renewal/contracts.md).
4. **Provider release gates:** obtain written Last.fm public-page approval and verify applicable territorial, consent, recording, attribution, and hosting requirements. The request remains a draft; confirm the repository URL before sending it. See [Phase 3 references](docs/renewal/phase-3-evidence.md#authoritative-documentation) and the [release checklist](docs/renewal/phase-9-release-checklist.md).
5. **Production operations:** choose hosting and delivery arrangements, validate storage limits against the chosen host, and complete the [Phase 9 release checklist](docs/renewal/phase-9-release-checklist.md). Production deployment and external email delivery have not been performed.
6. **Phase 7 deployment checks:** inspect or convert any existing captured-email files and confirm persistent key-store access and backup protection before relying on encrypted capture. See [Phase 7 evidence](docs/renewal/phase-7-evidence.md).
7. **External CI:** run GitHub Actions and remote scanning; neither has been verified from this checkout.

## Phase implementation status

| Phase | Current status | Evidence |
|---|---|---|
| 1. Baseline and security | Implemented and locally verified; developer confirmation pending | [Phase 1](docs/renewal/phase-1-evidence.md) |
| 2. Harness mechanics | Implemented and locally verified; mechanics only | [Phase 2](docs/renewal/phase-2-evidence.md) |
| 3. Honest ingestion | Implemented and locally verified; provider gates remain | [Phase 3](docs/renewal/phase-3-evidence.md) |
| 4. Pilot tooling | Implemented and locally verified; live pilot remains open | [Phase 4](docs/renewal/phase-4-evidence.md) |
| 5. Real-data evaluation | Not authorized; recommendation quality unverified | [Renewal contracts](docs/renewal/contracts.md) |
| 6. Recommendation generation and feedback | Implemented and locally verified; quality and capacity unverified | [Phase 6](docs/renewal/phase-6-evidence.md) |
| 7. Worker and local email capture | Implemented and locally verified; deployment-specific checks remain open | [Phase 7](docs/renewal/phase-7-evidence.md) |
| 8. Privacy and scheduling | Implemented and locally verified; developer confirmation and external delivery remain open | [Phase 8a](docs/renewal/phase-8a-evidence.md), [Phase 8b](docs/renewal/phase-8b-evidence.md) |
| 9. Reproducibility and release preparation | Implemented and locally verified; external CI and release gates remain open | [Phase 9](docs/renewal/phase-9-evidence.md) |

## Product and evidence constraints

- V1 remains popularity-neutral. Do not claim a new recommendation algorithm or ownership of upstream similarity data. Synthetic fixtures and pilot mechanics do not establish recommendation quality.
- Real recordings and evaluation reports require separate approval, stay local, and must remain gitignored and untracked. Do not tune against held-out data.
- Last.fm and ListenBrainz remain provider-boundary concerns. Live acquisition needs explicit configuration and usage review. Listening-history imports remain deferred. Streaming search links are not Spotify API integration.
- The temporary 1,000,000,000-byte managed-storage stop is not a VPS-capacity decision. Hosting, provisioning, public deployment, and external email delivery are not authorized by this progress record.
- Product decisions and status live here; verified preferences and gotchas live in [MEMORY](MEMORY.md). User-visible changes belong in [CHANGELOG](CHANGELOG.md).
