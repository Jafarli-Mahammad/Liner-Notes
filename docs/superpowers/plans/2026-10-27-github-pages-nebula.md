# GitHub Pages Nebula Implementation Plan

> **For agentic workers:** Execute this plan inline in small steps. Keep each commit focused and do not run the application test suite.

**Goal:** Publish an interactive Liner Notes code graph on GitHub Pages, refreshed from the `main` revision that triggered each successful deployment.

**Architecture:** A dedicated GitHub Actions workflow downloads a pinned Codebase Memory release, verifies its checksum, indexes the checked-out source in an isolated runner cache, and exports the viewer plus graph data to a Pages artifact. A small Python adapter captures the release's local viewer, snapshots its API responses into static JSON, and maps browser requests to those files. A validator refuses incomplete, stale, or path-leaking output before deployment.

**Tech Stack:** GitHub Actions Pages artifact/deploy actions, Codebase Memory native Linux portable release, Python 3 standard library, static HTML/JavaScript/JSON.

---

## Files and responsibilities

- Create `.github/workflows/nebula-pages.yml`: `main` push, `workflow_dispatch`, and pull-request validation; only `main` deploys; least-privilege permissions and a single Pages concurrency group.
- Create `tools/nebula/export.py`: run the pinned MCP server in an isolated cache, request a fresh index for the checkout, and export the local viewer responses into `dist/nebula`.
- Create `tools/nebula/validate.py`: check required static files, commit and timestamp metadata, non-empty graph counts, base-path references, excluded data, and runner-path leakage.
- Create `tools/nebula/static/index.html` and `tools/nebula/static/adapter.js`: a static-hosting adapter for the same Codebase Memory v0.11.0 graph viewer used by DevJourney.
- Create `tools/nebula/vendor/` with the committed viewer bundle and its license/attribution. Source only the two tracked assets from DevJourney commit `8130a404955b636f0be21fb6f8f806e26694db7f`; do not copy or modify that checkout.
- Modify `README.md`, `README-Developer.md`, and `docs/architecture/README.md` to link to the deployed page and distinguish the refreshed snapshot from a live server.
- Modify `CHANGELOG.md` for the public Nebula addition.

## Task 1: Verify the pinned Codebase Memory interface

**Files:** No repository changes.

- [ ] Fetch the official Linux portable v0.12.1 archive and `checksums.txt`. Verify the archive SHA-256 `8c3d0341064301e78ecc84cf7348d4e2c6562a0f57448243ea560aaa34de36c1` before extracting it, then verify the executable SHA-256 `77745491f0a9c346d6da23eed4d92b5feaa31a27a883c9faa4fa9f47c8183705` from the official manifest.
- [ ] Run the extracted binary's `--version` command and confirm it reports v0.12.1.
- [ ] Use the official asset `codebase-memory-mcp-linux-amd64-portable.tar.gz`; the archive hash is `8c3d0341064301e78ecc84cf7348d4e2c6562a0f57448243ea560aaa34de36c1`, and the extracted executable hash is `77745491f0a9c346d6da23eed4d92b5feaa31a27a883c9faa4fa9f47c8183705`.
- [ ] Download the archive and verify the pinned hash before extraction:

  ```bash
  set -eu
  cbm_tmp="$(mktemp -d)"
  mkdir -p "$cbm_tmp/cbm"
  curl -fsSL -o "$cbm_tmp/cbm.tar.gz" \
    https://github.com/DeusData/codebase-memory-mcp/releases/download/v0.12.1/codebase-memory-mcp-linux-amd64-portable.tar.gz
  echo '8c3d0341064301e78ecc84cf7348d4e2c6562a0f57448243ea560aaa34de36c1  '"$cbm_tmp/cbm.tar.gz" | sha256sum --check
  tar -xzf "$cbm_tmp/cbm.tar.gz" -C "$cbm_tmp/cbm"
  cbm_bin="$(find "$cbm_tmp/cbm" -type f -name codebase-memory-mcp -print -quit)"
  echo '77745491f0a9c346d6da23eed4d92b5feaa31a27a883c9faa4fa9f47c8183705  '"$cbm_bin" | sha256sum --check
  cbm_bin="$(find "$cbm_tmp/cbm" -type f -name codebase-memory-mcp -print -quit)"
  "$cbm_bin" --version
  ```
- [ ] Start the release with an isolated `CBM_CACHE_DIR` and `--ui=true --port=9749`. Use the MCP `initialize`, `notifications/initialized`, `tools/list`, and `tools/call` JSON-RPC methods to run `index_repository` on this checkout and poll `index_status` until ready.
- [ ] Request the local layout response from `http://127.0.0.1:9749/api/layout?project=LinerNotes`. Inspect the returned JSON and the release's served HTML to record every endpoint and asset required by the interactive viewer.
- [ ] Confirm compatibility against the tracked DevJourney snapshot: both layout responses use `nodes`, `edges`, `total_nodes`, and node fields `id`, `x`, `y`, `z`, `label`, `name`, `file_path`, `qualified_name`, `start_line`, `end_line`, `size`, `color`, and `in_calls`. The v0.12.1 response may add fields.
- [ ] Use these static response files and routes: `/api/layout?project=LinerNotes&max_nodes=...` → `data/layout.json`; `/api/layout?project=LinerNotes&max_nodes=10000&graph=missed` → `data/layout-missed.json`; `/api/repo-info?project=LinerNotes` → `data/repo-info.json`; `/api/ui-config` → `data/ui-config.json`; `/api/project-health?name=LinerNotes` → `data/project-health.json`; `/api/adr?project=LinerNotes` → `data/adr.json`.
- [ ] Account for the viewer's 5,000-node default and 10,000,000-node cap. Read `total_nodes` from the first layout response, request `ceil(total_nodes / 5000) * 5000`, then fail if the returned node count differs from `total_nodes`.
- [ ] Stop the local server and remove only the temporary cache created for this probe.

**Expected result:** A reproducible, version-pinned export contract for graph data and UI assets; no source files or user data are modified.

## Task 2: Implement the graph exporter and static adapter

**Files:** Create `tools/nebula/export.py`, `tools/nebula/static/index.html`, `tools/nebula/static/adapter.js`, `tools/nebula/vendor/index-BiHLJL7z.js`, `tools/nebula/vendor/index-Bk-h0GSQ.css`, `tools/nebula/vendor/LICENSE`, and `tools/nebula/vendor/NOTICE.md`.

- [ ] Implement `export.py` with standard-library `subprocess`, `json`, and `urllib` only. It must accept `--binary`, `--repo`, `--output`, `--commit`, and `--generated-at`; start the local MCP/UI process; initialize MCP; request indexing; wait for ready status with a finite timeout; export the responses established in Task 1; and stop the child process in `finally`.
- [ ] Write `dist/nebula/data/metadata.json` with the source commit, UTC generation time, Codebase Memory version, complete node count, and complete edge count. Write the six viewer JSON responses beneath `dist/nebula/data/` without including local database files.
- [ ] Copy only the two tracked viewer assets listed in the Files section from DevJourney commit `8130a404955b636f0be21fb6f8f806e26694db7f`; add the upstream Codebase Memory license and credit CodeAtlasWeb in `NOTICE.md`.
- [ ] Add the copied assets under `tools/nebula/vendor/`. The checked-in `static/index.html` loads them with relative URLs and loads `adapter.js` before the viewer bundle. Keep the document under the `/Liner-Notes/` path and preserve `tab=graph&project=LinerNotes`.
- [ ] Map the viewer routes recorded in Task 1 to their JSON files. Route `/api/layout` with `graph=missed` to `data/layout-missed.json`; route the default graph request to `data/layout.json`. Scrub `repo-info.root_path` to `.` and omit runner paths. Preserve the `tab=graph&project=LinerNotes` entry URL and return an empty JSON object for other `/api/` requests, matching the DevJourney static-hosting adapter.
- [ ] Set `repo-info.blob_base` to `https://github.com/Jafarli-Mahammad/Liner-Notes/blob/main` so selecting a graph node can open its source file on the repository's default branch.
- [ ] Write `NOTICE.md` with the Codebase Memory version, upstream repository, license link, and a note that only the static viewer assets and graph snapshot are published.
- [ ] Ensure graph data contains repository-relative paths only. Do not include `.git`, build output, caches, local settings, secrets, or any application data.

## Task 3: Add fail-closed artifact validation

**Files:** Create `tools/nebula/validate.py`.

- [ ] Accept `--site`, `--expected-commit`, and `--base-path`.
- [ ] Require `index.html`, all referenced local assets, all six JSON responses used by the adapter, and `data/metadata.json`.
- [ ] Reject malformed JSON, empty node or edge collections, zero counts, metadata for a different commit, an invalid UTC timestamp, root-absolute asset links, and any path that resolves outside the repository or temporary site root.
- [ ] Scan generated text files for the runner workspace prefix, user home path, absolute source paths, and forbidden file names (`appsettings.*`, `.env*`, and `.git/`). Fail with the offending relative artifact path and never print file contents.
- [ ] Confirm the emitted `index.html` and assets use the `/Liner-Notes/` base path and include no remote MCP URLs.

## Task 4: Add the GitHub Pages workflow

**Files:** Create `.github/workflows/nebula-pages.yml`.

- [ ] Trigger on `push` to `main`, `pull_request`, and `workflow_dispatch`. Run build and validation for all events; run deployment only when `github.ref == 'refs/heads/main'` and the event is not a pull request.
- [ ] Pin `actions/checkout` v6 to `d23441a48e516b6c34aea4fa41551a30e30af803`, `actions/upload-pages-artifact` v4 to `7b1f4a764d45c48632c6b24a0339c27f5614fb0b`, and `actions/deploy-pages` v4 to `d6db90164ac5ed86f2b6aed7e0febac5b3c0c03e`. Give the build job `contents: read`; give the deploy job only `pages: write` and `id-token: write`.
- [ ] Download Codebase Memory v0.12.1's Linux portable archive from the official release URL, verify SHA-256 `77745491f0a9c346d6da23eed4d92b5feaa31a27a883c9faa4fa9f47c8183705` before extraction, then run `--version` and fail unless it matches v0.12.1.
- [ ] Set an isolated `CBM_CACHE_DIR`, pass `${{ github.sha }}` as source commit and a UTC timestamp to `export.py`, then run `validate.py` before calling `upload-pages-artifact`.
- [ ] Upload only `dist/nebula`; do not write generated graph data back to the repository.
- [ ] Set workflow-level concurrency to `group: nebula-${{ github.ref }}` and `cancel-in-progress: true`, so a newer run for the same ref cancels an older run without a feature-branch check cancelling a `main` deployment.
- [ ] On pull requests, stop after validation. On manual runs from any branch other than `main`, stop before deployment.

## Task 5: Link and describe the viewer

**Files:** Modify `README.md`, `README-Developer.md`, `docs/architecture/README.md`, and `CHANGELOG.md`.

- [ ] Add the URL `https://jafarli-mahammad.github.io/Liner-Notes/?tab=graph&project=LinerNotes` beside the existing source-graph references.
- [ ] Explain that the browser graph is interactive but its data is a static snapshot rebuilt on each successful `main` deployment; include its generated revision/time in the viewer.
- [ ] Keep the existing screenshot and its October 2026 capture metadata as an archive, not as the current graph.
- [ ] Add one concise user-visible changelog entry; do not claim production deployment until the live Pages run is verified.

## Task 6: Verify and commit

- [ ] Run `python3 tools/nebula/export.py --help` and `python3 tools/nebula/validate.py --help` to confirm command interfaces.
- [ ] Build the site from this checkout with the pinned release, then run `python3 tools/nebula/validate.py --site dist/nebula --expected-commit "$(git rev-parse HEAD)" --base-path /Liner-Notes/`.
- [ ] Serve `dist/nebula` with `python3 -m http.server` and open the Pages-style URL in the available browser. Confirm graph navigation, query preservation, asset loading, and freshness metadata.
- [ ] Run `git diff --check`; inspect the artifact inventory and workflow permissions. Do not run `dotnet test` or another application test suite.

- [ ] Commit only the exporter, adapter/assets, workflow, README links, and changelog for this feature.
