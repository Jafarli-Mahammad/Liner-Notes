# Phase 1 evidence

## Baseline

2026-10-06, `Prism` at `10d97f9`, initially clean. SDK 10.0.112 / runtime 10.0.12.
`dotnet test 'Liner Notes.sln' --no-restore --filter 'FullyQualifiedName!~FounderSeedCoverageSpikeTests'`
passed; counts are recorded in [baseline summary](evidence/baseline.txt).
Sandbox IPC initially failed; rerun outside sandbox was approved. No live API calls made.
The excluded test can read local credentials and writes hard-coded coverage conclusions;
its removal/replacement requires approval. Filtering this run does not certify that test.

## 1A

Commit-set `renewal-1a`. `python3 scripts/checks/renewal_docs.py` failed on personal notes
before the rewrite ([red](evidence/1a-red.txt)), then passed ([green](evidence/1a-green.txt)).
Reconciled README, progress, agent guidance and specification precedence. Removed personal taste
notes and obsolete pillar; replaced the unsupported coverage report with a provenance qualification.
No scorer/provider changes. Developer confirmation pending.

## 1B

Commit-set `renewal-1b`. `python3 scripts/checks/secret_defaults.py` reproduces the tracked
credential fallback ([red](evidence/1b-red.txt)), then passes ([green](evidence/1b-green.txt)).
Removed JSON and DataAccess fallback credentials; missing connection configuration fails startup.
Added placeholder/environment guidance and ignore rules. [Rotation](local-configuration.md) remains
an operator action, not claimed performed. No credential values were copied into evidence.
