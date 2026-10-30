# Architecture visuals

[Project overview](../../README.md) · [Developer guide](../../README-Developer.md)

## Interactive graph

[Open the Nebula](https://jafarli-mahammad.github.io/Liner-Notes/?tab=graph&project=LinerNotes). It is rebuilt from `main` on each successful Pages deployment. The browser view is interactive; its data is a generated snapshot, not a live Codebase Memory session.

## Codebase graph snapshot

![Codebase Memory graph of the Liner Notes source checkout](codebase-nebula.png)

This image is a screenshot of Codebase Memory's local graph viewer, not a
runtime diagram. It captures an index of source, tests, scripts and
documentation. See the table below for when and how it was captured.

| Detail | Snapshot |
| --- | --- |
| Project | `LinerNotes` |
| Checkout | `Prism`, based on commit `62a2698` |
| Captured | 2026-10-10 (Asia/Baku) |
| Viewer | Codebase Memory 0.11.0 |
| Index | Full refresh; 2,804 nodes and 10,789 edges |
| Image | 1600 × 1000 PNG |

The counts describe that index snapshot. They are not test totals or runtime
measurements. Graph relationships are best-effort static analysis; inspect the
source before relying on them. In particular, point placement does not show
architecture layers, and a dead-code label is only a candidate finding.

The index reported partial parsing for:

- [`scripts/git-qa-reviewer.py`](../../scripts/git-qa-reviewer.py)
- [`scripts/phase4_replay.cs`](../../scripts/phase4_replay.cs)
- [`src/Presentation/wwwroot/index.html`](../../src/Presentation/wwwroot/index.html)

Generated and ignored files were excluded. This image is a dated reference and
does not update automatically when the code changes.

## Other images

- The [project README](../../README.md) introduces the product and links to its
  current status.
- The [developer guide](../../README-Developer.md) explains project boundaries
  and request paths.
- [`liner-notes-cover.svg`](../assets/liner-notes-cover.svg) is the static
  cover image used by the project README.
