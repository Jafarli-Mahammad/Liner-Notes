# Release preparation checklist

**Public release is blocked.** Local container/test results do not supply
third-party permission, developer confirmation or production operations.

| Gate | Evidence or remaining decision |
|---|---|
| Reproducible build | SDK/tool/action/image pins, central stable packages and locks; see [version snapshot](phase-9-versions.json) |
| Local verification | [Phase 9 evidence](phase-9-evidence.md); developer confirmation remains pending |
| Actual GitHub Actions run | Not verified: workflow implemented locally; no authorized remote push/run occurred |
| Last.fm written public-page approval | Missing. Supply the real written approval reference, approved pages/button/link placements and any conditions before public access |
| Last.fm other terms questions | Territorial/consent, retained data/publication and text-only email attribution questions remain unresolved; no universal numeric request allowance is asserted |
| Recommendation quality | Not verified. Baseline A remains provisional; real evaluation/held-out results remain deferred |
| Hosting and shared capacity | Not selected. The 1 GB guard remains a temporary placeholder, not an approved capacity plan |
| External email provider/delivery | Not implemented or authorized by the local capture slice; SentAt remains null for local captures |
| Production scheduling | Not implemented: current Worker is Development-only and explicitly bounded |
| Key custody and backups | PostgreSQL keys persist, but wrapping/access/backups and restore/deletion procedures require an operator design; local encryption does not settle those decisions |
| Existing plaintext copies | Actual deployments/copies were not inventoried or converted; only fabricated legacy-conversion tests have evidence |
| Public deployment and remote publication | Separate explicit approval required; no push, provisioning, purchase or deployment performed |

Record real evidence and conditions rather than marking a missing gate satisfied.
The enquiry in the [renewal contracts](contracts.md) remains a draft. No message
was sent to Last.fm. A Phase 5 evidence bypass does not waive §2.7 public-page
permission. [Last.fm API terms](https://www.last.fm/api/tos) were checked on
2026-10-12 for this release-preparation work.

MediatR 12.5.0 is an explicit developer-approved licensing exception; do not
silently adopt RPL/commercial releases or purchase a license. Other package/tool
pins are a verified stable snapshot and need reviewed maintenance updates as
new stable releases become available. The project license remains AGPL-3.0.
