# PR review fixes, 2026-10-19

## Recommendation findings

All four Greptile findings were valid.

- **Recording identity:** onsite batch completion reused a track using names
  alone. It now also matches the candidate MBID, case-insensitively, and keeps
  name-only tracks separate from identified recordings, matching weekly storage.
  PostgreSQL regression coverage uses separate listeners, identical display
  names, different MBIDs, a missing MBID, and uppercase/lowercase MBID reuse.
- **Recording tag coverage:** ListenBrainz hydration rejected candidates with
  valid recording tags whenever artist tags were empty. It now accepts the
  recording evidence independently. A synthetic client with empty artist-tag
  responses verifies the retained candidates and recording-scoped vectors.
- **Weekly answer changes:** the save-time input hash omitted profile revision
  and onboarding state. Both are now included. A PostgreSQL regression changes
  only answers and then only revision, with no signals or feedback, and verifies
  that stale generation cannot persist recommendations.
- **Long favorites:** shared seed and candidate-pool preparation imposed a
  200-character display-name limit despite the catalog's 256-character contract.
  Both now accept 256; tests cover 201 and 256 characters for canonical seed/pool
  requests and retain rejection at 257. Canonical upstream requests still use IDs.

These changes protect subsequent generation. Existing saved history is not
rewritten or reconstructed.

## CI findings

Public GitHub job annotations confirmed failures in restore, the Compose build,
and the older Qodana job's linter pull. Full job-log download returned HTTP 403
without authenticated GitHub credentials. The excerpts' `NU1510` messages are
warnings and do not identify the restore failure.

Locked restore passed on the original Prism head. Restoring a disposable tree
made from the actual `origin/Prism` + `origin/main` merge failed with `NU1004`:
the DataAccess lock declared `System.Security.Cryptography.Pkcs` as
`CentralTransitive`, but the central manifest supplied no matching pin.
The inherited lock also contained duplicate package keys and a stale Application
project dependency. This explains why a head-only local check missed the CI
failure, while the default PR merge checkout and Docker build encountered it.

Main's existing crypto updates are retained. The regenerated DataAccess lock
resolves Xml and its transitive Pkcs dependency to `10.0.13`, without an undeclared
central pin. All eleven lock files were checked for duplicate JSON keys.
The versions are stable and support the target framework:
[Xml package](https://www.nuget.org/packages/System.Security.Cryptography.Xml/10.0.13),
[Pkcs package](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/10.0.13).
No dependency was removed to suppress the `NU1510` warnings.

The old `qodana_code_quality.yml` is replaced by the single pinned native-mode
workflow in `qodana.yml`, so analysis no longer needs to pull a linter container.
The configuration retains the dependency-license inspection and accepts either
`QODANA_TOKEN` or the existing `QODANA_TOKEN_1205615720` secret. See the
[Qodana runbook](../qodana.md). A successful remote Qodana run and Cloud license
validation remain unverified locally.

## GitGuardian findings

The reported commits were inspected without publishing credential values.

| Incident | Finding | Assessment and action |
| --- | --- | --- |
| 38143097 | `a5c815e`, `compose.yaml` | The historical connection strings interpolate an externally required password. They do not contain a concrete password. Current Compose uses private connection-string variables. No active credential exposure was established from this match. |
| 38369067 | `2648afe`, `scripts/catalog/test_catalog_probe.py` | The fixed value is synthetic input to a unit test of environment validation, not a connection to a service. Replaced the current literal with `secrets.token_urlsafe(24)`. |
| 37766727 | `a5c815e`, `scripts/checks/secret_defaults.py` | The match is a negative assertion rejecting a known default password, not a credential assignment. That literal is already absent from HEAD; the current check rejects any literal password assignment. |

No deployed secret requiring revocation was identified in these three findings.
Changing HEAD does not erase historical matches or change GitGuardian incident
status. These incidents need classification in GitGuardian using the evidence
above. No history rewrite, force-push, service rotation, or external incident
status change was performed.

## Local verification

- Full Release suite with PostgreSQL: **680 passed, zero failures/skips** across
  all five projects. The CI TRX gate confirmed the complete result set.
- Focused infrastructure regression group: **22 passed**.
- Catalog probe tests: **15 passed**; existing offline phase checks: **32 passed**;
  CI helper checks: **4 passed**.
- Secret-default and renewal-document checks passed.
- Workflow YAML, shell syntax, existing-token fallback and single-workflow
  checks passed; the final workflow passed `actionlint`.
- All **nine Compose lifecycle groups passed**, including a fresh Docker build,
  migration failure isolation, repeated migrations, generation/capture/export,
  shared operator ownership, recreation, scheduling, unsubscribe and deletion.
- Forced locked restore from a clean archive of the final staged merge passed;
  the original uncorrected merge failed the same check with `NU1004`.

Developer confirmation and GitHub reruns remain separate from these local checks.
