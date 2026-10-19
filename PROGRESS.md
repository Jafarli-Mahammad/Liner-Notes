# Liner Notes renewal progress

## Current status

Phases 1–4 and 6–9 have locally verified implementations. Phase 5 real-data recommendation evaluation is not authorized. Local implementation evidence does not establish recommendation quality, external provider approval, production readiness, or developer confirmation.

Phase 9 includes pinned build inputs, non-root web and Worker images, local Compose topology, and PostgreSQL-backed CI. Recorded local verification passed 478 .NET checks, 32 offline Python tests, four helper test methods, and all eight container lifecycle groups. GitHub Actions execution and remote scanning have not been verified. See [Phase 9 evidence](docs/renewal/phase-9-evidence.md) and the [local runbook](docs/renewal/phase-9-local-run.md).

## Guest discovery

The guest-first discovery design and sequential implementation were approved. The first onsite flow now supports artist, album, and style-tag selection; immediate synthetic recommendations with persisted explanations; explicit catalog lookup; feedback; export; registration with guest data preservation; explicit import into an existing account; and guest data deletion.

The implementation passed 648 .NET checks with zero failures/skips and 12 synthetic browser checks. Seven catalog integration checks also passed after strengthening private-evidence retention assertions. The browser covered search, artist/album selection, explicit lookup, generation, return visits, feedback, export, registration/import, cooldown, sparse results and deletion.

Provider selection uses registered polymorphic bundles. Application workflows do not depend on provider implementations or EF Core types. Persistence contracts live in Application; database-specific SQL and mappings remain in DataAccess. A database change still needs schema/data migration, but should not rewrite product workflows.

Bounded public cleanup now preserves the active catalog, fresh lookups, staged imports, canonical identities and private favorite snapshots. Weekly saves, scheduler activity and unsubscribe updates use discovery admission when its runtime is enabled; PostgreSQL verification confirmed a subsequent guest write still succeeds.

The constrained Release runtime passed the 50-profile load scenario with five generating visitors and 5/20/50 concurrent search clients. The final warm search p95 values were 28/123/323 ms; five synthetic batches completed in 2.11–2.27 seconds. A separate run recovered five unfinished requests across a forced website restart. Earlier burst runs were slower; the provisional 100 ms burst target is not met. This is local synthetic evidence, not a production capacity guarantee. See [resource measurements and limits](docs/renewal/guest-discovery-capacity.md).

Remaining confirmation and deployment gates:

- Developer review of the concrete onsite flow remains pending.
- A real catalog acquisition manifest is not approved or imported. Coverage, recommendation quality and live-provider latency remain unverified.
- Optional local email capture is excluded from concurrent guest-runtime operation in both hosts. It needs separate file/key write coordination before that combination is enabled. No external email transport is configured.
- Production hosting, full-disk operating limits, backups, real-catalog load and broader saturation behavior need deployment-specific verification. There is no configured total-user ceiling.

See the [evidence](docs/renewal/guest-discovery-evidence.md), [storage notes](docs/renewal/guest-discovery-storage.md), [design](docs/superpowers/specs/2026-10-16-guest-discovery-design.md), [execution plan](docs/superpowers/plans/2026-10-16-guest-discovery.md), and [synthetic runbook](docs/renewal/guest-discovery-local-run.md).

## Optional taste onboarding

Implementation of the approved 2026-10-19 design passed local verification: 669
.NET tests with zero failures/skips, 21 browser checks and the EF model/migration
consistency check. The 271-tag synthetic generation/export journey passed within
the existing snapshot budget.
Guests and accounts share resumable optional questions, searchable tags without a
selection count cap, broad taste, artist/album/recording favorites and explicit
review. Skipping taste opens a separate recipient-free Recent recommendations
feed. Saved selections are separate from bounded generation windows and replay
snapshots. Export, attachment, explicit import, ownership and deletion include the
new state. Existing name seeds, custom tags and queued request retries remain compatible.

See [implementation evidence](docs/renewal/optional-taste-onboarding-evidence.md)
and the [execution plan](docs/superpowers/plans/2026-10-19-optional-taste-onboarding.md).
Developer confirmation, live recording/vocal coverage and recommendation quality
remain open. Last.fm stays available; unsupported direct recording capability is
reported as a coverage gap. Deployment and real catalog acquisition remain separate gates.

## Provider status

ListenBrainz is the configured default; Last.fm remains available behind the provider abstraction. Live use is explicitly gated. Per-user provider choice and listening-history imports are deferred. Developer confirmation, deployment-specific usage and license review, and recommendation-quality evaluation remain open. See the [provider evidence](docs/renewal/listenbrainz-provider-evidence.md) and [operations guide](docs/renewal/listenbrainz-provider-runbook.md).

## Remaining renewal work

1. **Developer confirmation:** review locally verified phase evidence and confirm the implementation.
2. **Phase 4 live pilot:** approve a concrete run manifest, reconcile shared-storage inventory, freeze inputs, and complete data-shape checks and blind ratings. No live pilot has been run; fixtures establish mechanics only. See [Phase 4 evidence and run procedure](docs/renewal/phase-4-evidence.md).
3. **Phase 5 decision:** real-data evaluation needs separate authorization, an approved development and held-out manifest, frozen scoring configuration, and documented comparison steps. Recommendation quality remains unverified. See [renewal contracts](docs/renewal/contracts.md).
4. **Provider release gates:** obtain written Last.fm public-page approval and verify applicable territorial, consent, recording, attribution, and hosting requirements. The request remains a draft; confirm the repository URL before sending it. See [Phase 3 references](docs/renewal/phase-3-evidence.md#authoritative-documentation) and the [release checklist](docs/renewal/phase-9-release-checklist.md).
5. **Production operations:** choose hosting and delivery arrangements, validate storage limits against the chosen host, and complete the [Phase 9 release checklist](docs/renewal/phase-9-release-checklist.md). Production deployment and external email delivery have not been performed.
6. **Phase 7 deployment checks:** inspect or convert any existing captured-email files and confirm persistent key-store access and backup protection before relying on encrypted capture. See [Phase 7 evidence](docs/renewal/phase-7-evidence.md).
7. **External CI:** GitHub Actions and Qodana have run and reported failures. The PR merge's malformed dependency lock and older Qodana workflow have local fixes; a successful GitHub rerun and GitGuardian incident classification remain pending. See [PR review evidence](docs/renewal/2026-10-19-pr-review-fixes.md).

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
