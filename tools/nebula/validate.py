#!/usr/bin/env python3
"""Validate a generated static Nebula site before publishing it."""

from __future__ import annotations

import argparse
import json
import re
import sys
from html.parser import HTMLParser
from pathlib import Path, PurePosixPath
from urllib.parse import urlsplit


REQUIRED_JSON = (
    "layout.json",
    "layout-missed.json",
    "repo-info.json",
    "ui-config.json",
    "project-health.json",
    "adr.json",
    "metadata.json",
)
REQUIRED_ASSETS = (
    "adapter.js",
    "assets/index-BiHLJL7z.js",
    "assets/index-Bk-h0GSQ.css",
    "LICENSE",
    "NOTICE.md",
)
EXCLUDED_DIRS = {".git", ".codebase-memory", ".idea", ".vs", "bin", "dist", "node_modules", "obj", "recordings"}
EXCLUDED_FILE = re.compile(r"^(?:\.env.*|appsettings(?:\..+)?\.json|.*\.user|.*\.(?:pfx|pem|key))$", re.I)


class SiteParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.local_refs: list[str] = []
        self.meta: dict[str, str] = {}

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        values = dict(attrs)
        for name in ("src", "href"):
            ref = values.get(name)
            if ref:
                self.local_refs.append(ref)
        if tag == "meta" and values.get("name") and values.get("content"):
            self.meta[values["name"]] = values["content"]


def load_json(path: Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"Invalid or missing JSON file: {path.name}") from exc
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object in {path.name}")
    return value


def validate_paths(value: object, runner_paths: tuple[str, ...], current_key: str = "") -> None:
    if isinstance(value, dict):
        for key, item in value.items():
            validate_paths(item, runner_paths, key.lower())
    elif isinstance(value, list):
        for item in value:
            validate_paths(item, runner_paths, current_key)
    elif isinstance(value, str):
        if any(prefix and prefix in value for prefix in runner_paths):
            raise ValueError("Snapshot contains an absolute runner or home path")
        if current_key in {"file_path", "root_path", "path", "source_path"}:
            path = PurePosixPath(value)
            if path.is_absolute() or ".." in path.parts:
                raise ValueError("Snapshot contains an absolute or parent-relative source path")
            if any(part in EXCLUDED_DIRS or EXCLUDED_FILE.fullmatch(part) for part in path.parts):
                raise ValueError(f"Snapshot contains an excluded source path: {value}")
        if current_key in {"url", "remote_url", "web_base", "blob_base", "endpoint"} and re.match(
            r"^https?://(?:127\.0\.0\.1|localhost)(?::|/)", value, re.I
        ):
            raise ValueError("Snapshot contains a local Codebase Memory service URL")


def validate_local_assets(site: Path, html: str, base_path: str, expected_commit: str) -> None:
    parser = SiteParser()
    parser.feed(html)
    if parser.meta.get("pages-base-path") != base_path:
        raise ValueError(f"Page base path does not match {base_path}")
    if parser.meta.get("graph-source-commit") != expected_commit:
        raise ValueError("Page source commit does not match the triggering revision")

    def validate_ref(ref: str) -> None:
        parsed = urlsplit(ref)
        if parsed.scheme or ref.startswith("#"):
            return
        if ref.startswith("/"):
            raise ValueError(f"Root-absolute asset reference does not work under Pages subpaths: {ref}")
        target = (site / parsed.path).resolve()
        try:
            target.relative_to(site.resolve())
        except ValueError as exc:
            raise ValueError(f"Asset escapes the site directory: {ref}") from exc
        if not target.is_file():
            raise ValueError(f"Referenced local asset is missing: {ref}")

    for ref in parser.local_refs:
        validate_ref(ref)
    for css_path in site.rglob("*.css"):
        for ref in re.findall(r"url\((?:['\"]?)([^)'\"]+)", css_path.read_text(encoding="utf-8")):
            validate_ref(ref.strip())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--site", required=True, type=Path)
    parser.add_argument("--expected-commit", required=True)
    parser.add_argument("--base-path", required=True)
    args = parser.parse_args()

    site = args.site.resolve(strict=True)
    if not args.base_path.startswith("/") or not args.base_path.endswith("/"):
        raise ValueError("--base-path must start and end with a slash")

    index_path = site / "index.html"
    if not index_path.is_file():
        raise ValueError("index.html is missing")
    for relative in REQUIRED_ASSETS:
        if not (site / relative).is_file():
            raise ValueError(f"Required asset is missing: {relative}")
    data_dir = site / "data"
    for name in REQUIRED_JSON:
        if not (data_dir / name).is_file():
            raise ValueError(f"Required graph data is missing: data/{name}")

    metadata = load_json(data_dir / "metadata.json")
    if metadata.get("source_commit") != args.expected_commit.lower():
        raise ValueError("Graph source commit does not match the triggering revision")
    generated_at = metadata.get("generated_at")
    if not isinstance(generated_at, str) or not re.fullmatch(r"\d{4}-\d\d-\d\dT.*Z", generated_at):
        raise ValueError("Graph generation time must be a UTC ISO 8601 timestamp")
    for field in ("node_count", "edge_count"):
        if not isinstance(metadata.get(field), int) or metadata[field] < 1:
            raise ValueError(f"Graph metadata has an invalid {field}")
    if not re.fullmatch(r"\d+\.\d+\.\d+", str(metadata.get("codebase_memory_version", ""))):
        raise ValueError("Graph metadata has an invalid Codebase Memory version")
    if metadata.get("viewer_version") != "0.11.0":
        raise ValueError("Graph viewer version does not match the vendored bundle")

    layout = load_json(data_dir / "layout.json")
    nodes = layout.get("nodes")
    edges = layout.get("edges")
    if not isinstance(nodes, list) or not nodes or not isinstance(edges, list) or not edges:
        raise ValueError("Graph layout must contain nodes and edges")
    if layout.get("total_nodes") != len(nodes) or layout.get("total_edges") != len(edges):
        raise ValueError("Graph layout counts do not match its node and edge arrays")
    if metadata["node_count"] != len(nodes) or metadata["edge_count"] != len(edges):
        raise ValueError("Graph metadata counts do not match the exported layout")
    node_ids = {node.get("id") for node in nodes if isinstance(node, dict)}
    if len(node_ids) != len(nodes):
        raise ValueError("Graph node records are malformed or have duplicate identifiers")
    if any(not isinstance(edge, dict) for edge in edges):
        raise ValueError("Graph edge records are malformed")
    if any(edge.get("source") not in node_ids or edge.get("target") not in node_ids for edge in edges):
        raise ValueError("Graph contains an edge without a corresponding node")

    missed = load_json(data_dir / "layout-missed.json")
    if not isinstance(missed.get("nodes"), list) or not isinstance(missed.get("edges"), list):
        raise ValueError("Missed-graph data is malformed")
    health = load_json(data_dir / "project-health.json")
    if health.get("nodes") != len(nodes) or health.get("edges") != len(edges):
        raise ValueError("Static project health counts do not match the published graph")
    repo_info = load_json(data_dir / "repo-info.json")
    if repo_info.get("root_path") != ".":
        raise ValueError("Repository metadata must not expose a runner path")

    runner_paths = tuple(filter(None, {str(Path.cwd().resolve()), str(Path.home().resolve())}))
    for path in data_dir.glob("*.json"):
        validate_paths(load_json(path), runner_paths)
    html = index_path.read_text(encoding="utf-8")
    validate_local_assets(site, html, args.base_path, args.expected_commit.lower())
    print(f"Validated Nebula snapshot: {len(nodes):,} nodes, {len(edges):,} edges, commit {args.expected_commit[:7]}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"Nebula validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
