# Production beta engineering evidence

This record distinguishes local implementation checks from production acceptance.
The approved scope and execution order are in the [design](../superpowers/specs/2026-11-01-production-beta-design.md) and [plan](../superpowers/plans/2026-11-01-production-beta.md).

## Release packaging

The original tracked tree at `972d536` failed the new artifact check with `missing_release_asset`: tracked HTML referenced ignored React outputs. The new validator's four tests cover complete output, missing/empty assets/licenses, unsafe references and HTML returned instead of JavaScript.

The Docker frontend stage uses Node 26.13.0, verified against the [official release index](https://nodejs.org/dist/index.json), and the official `node:26.13.0-bookworm-slim` manifest digest `sha256:bc0a2afd3b74ce12222fc8bab99271e66257f021078f5f4a5d687a77d280c1d9`. It runs locked npm installation, TypeScript checking, Vite build and the existing runtime-license collector. Generated host assets are excluded from build input.

Local checks on 2026-11-01:

- `python3 -m unittest discover -s scripts/checks -p test_production_artifact.py`: four passing tests.
- `python3 scripts/checks/production_artifact.py --build --tree 4e098def02fa9318db545d21418941df1fc0a950 --tag linernotes-production-beta:packaging`: clean Git-tree image build and complete asset/license verification passed. The staged tree contains the reviewed packaging changes; it has no ignored developer outputs.
- The same image ran in a disposable loopback container with provider jobs disabled and no database use. `production_artifact.py --image ... --url ...` fetched HTML and both assets and compared their SHA-256 hashes to the image contents; all passed. This was packaging verification under Development, not Production acceptance.

Production topology, recovery and live-provider acceptance remain separate plan tasks; packaging alone does not establish readiness.
