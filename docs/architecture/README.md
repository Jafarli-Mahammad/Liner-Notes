# Liner Notes · Architecture visuals

[Project README](../../README.md#for-developers) ·
[Developer guide](../../README-Developer.md)

## Codebase Memory Nebula snapshot

![Codebase Memory Nebula graph of Liner Notes](codebase-nebula.png)

`codebase-nebula.png` is a browser screenshot of the actual local Codebase Memory
graph viewer, not an illustration or a reconstructed graph. Both READMEs embed
this file, so the documentation keeps one canonical image.

| Snapshot detail | Value |
| --- | --- |
| Project | `LinerNotes` |
| Source checkout | `Prism`, based on commit `62a2698` before this documentation change |
| Capture date | 2026-10-10, Asia/Baku |
| Viewer | Codebase Memory 0.11.0, local HTTP graph UI |
| Index mode | Full, refreshed for this capture |
| Indexed nodes | 2,804 |
| Indexed edges | 10,789 |
| Scope | Indexed source, tests, scripts and documentation; no other project selected |
| Image | PNG, 1600 × 1000 pixels |

### Reading the picture

Each point represents an indexed node, such as a file, class, method or field.
Connections represent indexed relationships, including calls, usage and containment.
The viewer's filter panel provides node-type colors and a folder map. Nearby points
help you explore connected code; their positions are not architectural layers.

The source graph includes tests and tooling. Its node and edge counts are snapshot
metadata, not test totals, runtime traffic or a measure of implementation quality.
Any dead-code labels shown by the viewer are static-analysis candidates, not
confirmed findings.

### Coverage limits

Codebase Memory reports partial parsing for:

- [`scripts/git-qa-reviewer.py`](../../scripts/git-qa-reviewer.py)
- [`scripts/phase4_replay.cs`](../../scripts/phase4_replay.cs)
- [`src/Presentation/wwwroot/index.html`](../../src/Presentation/wwwroot/index.html)

Generated/build folders and configured ignored files are excluded. A clean
coverage check is not proof of a complete graph. Symbol resolution can also infer
connections that need source review. For example, runtime calls to repository
implementations must not be read as compile-time Application → DataAccess
references. The Mermaid dependency diagrams follow the actual `.csproj` files.

### Refreshing the snapshot

1. Select the current source checkout and branch. Refresh its Codebase Memory
   index with `index_repository`, then inspect `index_status` and relevant paths
   with `check_index_coverage`.
2. Open the local graph UI and choose **LinerNotes → View Graph**. Verify the
   selected project before capturing; the viewer can list other checkouts.
3. Keep the project label and filters visible, let the graph settle and frame the
   graph at a readable scale. Capture a 1600 × 1000 PNG.
4. Replace `codebase-nebula.png` and update this snapshot's commit, date, counts and
   coverage notes. Review the screenshot for unrelated project or personal data.

The capture in this change used the local viewer at `http://127.0.0.1:9749/` with
an installed Chromium browser. That localhost address is a contributor tool,
not a public project link.

## Other visuals

- The [main README](../../README.md) contains the intended weekly discovery loop
  and the current project-reference diagram.
- The [developer guide](../../README-Developer.md) adds the stored-digest request
  sequence, central data relationships and transient provider-evidence flow.
- [`liner-notes-cover.svg`](../assets/liner-notes-cover.svg) is the original record
  sleeve artwork used by the README. It is a static, self-contained vector with
  descriptive text and no external fonts, scripts or image dependencies.

Diagrams describe their scope next to the visual. The intended product flow is
separate from implemented requests; it does not establish production readiness.
