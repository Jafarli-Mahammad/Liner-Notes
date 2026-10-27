#!/usr/bin/env python3
"""Build a static Codebase Memory graph snapshot for GitHub Pages."""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import socket
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import urlopen


PROJECT = "LinerNotes"
VIEWER_VERSION = "0.11.0"
MAX_LAYOUT_NODES = 10_000_000
LAYOUT_NODE_STEP = 5_000
EXCLUDED_DIRS = {
    ".git",
    ".codebase-memory",
    ".idea",
    ".vs",
    "bin",
    "dist",
    "node_modules",
    "obj",
    "recordings",
}
EXCLUDED_FILE = re.compile(r"^(?:\.env.*|appsettings(?:\..+)?\.json|.*\.user|.*\.(?:pfx|pem|key))$", re.I)


class ExportError(RuntimeError):
    """Raised when indexing or snapshot export fails."""


class McpProcess:
    def __init__(self, binary: Path, repo: Path, cache: Path, port: int) -> None:
        env = os.environ.copy()
        env["CBM_CACHE_DIR"] = str(cache)
        self.binary = binary
        self.process = subprocess.Popen(
            [str(binary), "--ui=true", f"--port={port}"],
            cwd=repo,
            env=env,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            bufsize=0,
        )
        self.buffer = b""
        self.next_id = 0

    def request(self, method: str, params: dict[str, Any] | None = None, timeout: int = 900) -> dict[str, Any]:
        if self.process.stdin is None or self.process.stdout is None:
            raise ExportError("Could not open Codebase Memory MCP pipes")

        self.next_id += 1
        request_id = self.next_id
        payload: dict[str, Any] = {"jsonrpc": "2.0", "id": request_id, "method": method}
        if params is not None:
            payload["params"] = params
        try:
            self.process.stdin.write((json.dumps(payload, separators=(",", ":")) + "\n").encode())
            self.process.stdin.flush()
        except (BrokenPipeError, OSError) as exc:
            raise ExportError("Codebase Memory MCP closed its input") from exc

        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            newline = self.buffer.find(b"\n")
            if newline >= 0:
                line, self.buffer = self.buffer[:newline], self.buffer[newline + 1 :]
                if not line.strip():
                    continue
                try:
                    response = json.loads(line)
                except json.JSONDecodeError as exc:
                    raise ExportError("Codebase Memory returned invalid MCP JSON") from exc
                if response.get("id") == request_id:
                    if "error" in response:
                        raise ExportError(f"MCP {method} failed: {response['error'].get('message', 'unknown error')}")
                    result = response.get("result")
                    if not isinstance(result, dict):
                        raise ExportError(f"MCP {method} returned no result")
                    return result
                continue

            remaining = deadline - time.monotonic()
            if remaining <= 0:
                break
            try:
                chunk = self.process.stdout.read(4096)
            except OSError as exc:
                raise ExportError("Could not read Codebase Memory MCP output") from exc
            if not chunk:
                raise ExportError(f"Codebase Memory exited with status {self.process.poll()}")
            self.buffer += chunk

        raise ExportError(f"Timed out waiting for Codebase Memory {method}")

    def notify(self, method: str) -> None:
        if self.process.stdin is None:
            raise ExportError("Could not open Codebase Memory MCP input")
        payload = {"jsonrpc": "2.0", "method": method}
        self.process.stdin.write((json.dumps(payload, separators=(",", ":")) + "\n").encode())
        self.process.stdin.flush()

    def call_tool(self, name: str, arguments: dict[str, Any], timeout: int = 900) -> dict[str, Any]:
        result = self.request("tools/call", {"name": name, "arguments": arguments}, timeout)
        if result.get("isError"):
            details = [
                block["text"]
                for block in result.get("content", [])
                if isinstance(block, dict) and block.get("type") == "text" and isinstance(block.get("text"), str)
            ]
            detail = "; ".join(details)
            suffix = f": {detail[:1500]}" if detail else ""
            raise ExportError(f"Codebase Memory tool {name} reported an error{suffix}")
        structured = result.get("structuredContent")
        if isinstance(structured, dict):
            return structured
        for block in result.get("content", []):
            if block.get("type") == "text":
                try:
                    value = json.loads(block["text"])
                except (KeyError, json.JSONDecodeError):
                    continue
                if isinstance(value, dict):
                    return value
        raise ExportError(f"Codebase Memory tool {name} returned no JSON result")

    def stop(self) -> None:
        if self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=5)


def get_json(url: str, timeout: int = 30) -> dict[str, Any]:
    try:
        with urlopen(url, timeout=timeout) as response:
            value = json.load(response)
    except (HTTPError, URLError, TimeoutError, json.JSONDecodeError) as exc:
        raise ExportError(f"Could not read Codebase Memory API route {url.split('?', 1)[0]}") from exc
    if not isinstance(value, dict):
        raise ExportError("Codebase Memory API returned a non-object JSON response")
    if "error" in value:
        raise ExportError(f"Codebase Memory API failed: {value['error']}")
    return value


def is_excluded(node: dict[str, Any]) -> bool:
    raw_path = node.get("file_path")
    if not isinstance(raw_path, str) or not raw_path:
        return False
    path = PurePosixPath(raw_path)
    if path.is_absolute() or ".." in path.parts:
        raise ExportError("Graph contains an absolute or parent-relative source path")
    return any(part in EXCLUDED_DIRS or EXCLUDED_FILE.fullmatch(part) for part in path.parts)


def filter_graph(graph: dict[str, Any]) -> dict[str, Any]:
    nodes = graph.get("nodes")
    edges = graph.get("edges")
    if not isinstance(nodes, list) or not isinstance(edges, list):
        raise ExportError("Layout response is missing node or edge arrays")
    if any(not isinstance(node, dict) for node in nodes) or any(not isinstance(edge, dict) for edge in edges):
        raise ExportError("Layout response contains malformed node or edge records")
    visible_nodes = [node for node in nodes if not is_excluded(node)]
    visible_ids = {node.get("id") for node in visible_nodes}
    visible_edges = [
        edge
        for edge in edges
        if isinstance(edge, dict) and edge.get("source") in visible_ids and edge.get("target") in visible_ids
    ]
    result = dict(graph)
    result["nodes"] = visible_nodes
    result["edges"] = visible_edges
    result["total_nodes"] = len(visible_nodes)
    result["total_edges"] = len(visible_edges)
    missed_graph = graph.get("missed_graph")
    if isinstance(missed_graph, dict) and isinstance(missed_graph.get("nodes"), list):
        result["missed_graph"] = filter_graph(missed_graph)
    return result


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binary", required=True, type=Path, help="Verified codebase-memory-mcp binary")
    parser.add_argument("--repo", required=True, type=Path, help="Repository root to index")
    parser.add_argument("--output", required=True, type=Path, help="Empty output directory for the Pages site")
    parser.add_argument("--commit", required=True, help="40-character source commit SHA")
    parser.add_argument("--generated-at", help="UTC timestamp in ISO 8601 format; defaults to current UTC time")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo = args.repo.resolve(strict=True)
    binary = args.binary.resolve(strict=True)
    output = args.output.resolve()
    if not re.fullmatch(r"[0-9a-fA-F]{40}", args.commit):
        raise ExportError("--commit must be a 40-character Git SHA")
    if output.exists() and any(output.iterdir()):
        raise ExportError("Output directory must be empty; refusing to overwrite existing files")

    generated_at = args.generated_at or datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    try:
        parsed_time = datetime.fromisoformat(generated_at.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ExportError("--generated-at must be an ISO 8601 timestamp") from exc
    if parsed_time.tzinfo is None:
        raise ExportError("--generated-at must include a UTC timezone")
    if parsed_time.utcoffset() != timezone.utc.utcoffset(parsed_time):
        raise ExportError("--generated-at must be in UTC")

    output.mkdir(parents=True, exist_ok=True)
    port_socket = socket.socket()
    port_socket.bind(("127.0.0.1", 0))
    port = port_socket.getsockname()[1]
    port_socket.close()

    with tempfile.TemporaryDirectory(prefix="liner-notes-cbm-") as cache_path:
        client: McpProcess | None = None
        try:
            client = McpProcess(binary, repo, Path(cache_path), port)
            client.request(
                "initialize",
                {
                    "protocolVersion": "2025-06-18",
                    "capabilities": {},
                    "clientInfo": {"name": "liner-notes-nebula-export", "version": "1.0.0"},
                },
            )
            client.notify("notifications/initialized")
            tools = client.request("tools/list").get("tools", [])
            tool_names = {item.get("name") for item in tools if isinstance(item, dict)}
            required_tools = {"index_repository", "index_status"}
            if not required_tools.issubset(tool_names):
                raise ExportError("Pinned Codebase Memory release is missing required MCP tools")

            indexed = client.call_tool("index_repository", {"repo_path": str(repo), "name": PROJECT})
            status = client.call_tool("index_status", {"project": PROJECT, "format": "json"})
            if status.get("status") != "ready":
                raise ExportError("Codebase Memory index did not reach ready status")
            if indexed.get("status") not in (None, "indexed", "ready"):
                raise ExportError("Codebase Memory reported an unsuccessful index")

            api = f"http://127.0.0.1:{port}/api"
            ui_config = get_json(f"{api}/ui-config")
            health = get_json(f"{api}/project-health?{urlencode({'name': PROJECT})}")
            repo_info = get_json(f"{api}/repo-info?{urlencode({'project': PROJECT})}")
            adr = get_json(f"{api}/adr?{urlencode({'project': PROJECT})}")

            default_query = urlencode({"project": PROJECT, "max_nodes": LAYOUT_NODE_STEP})
            first_layout = get_json(f"{api}/layout?{default_query}")
            total_nodes = first_layout.get("total_nodes")
            if not isinstance(total_nodes, int) or total_nodes < 1:
                raise ExportError("Codebase Memory reported an empty or invalid graph")
            if total_nodes > MAX_LAYOUT_NODES:
                raise ExportError(f"Graph has more than {MAX_LAYOUT_NODES:,} nodes; refusing a partial export")
            max_nodes = min(MAX_LAYOUT_NODES, max(LAYOUT_NODE_STEP, ((total_nodes + LAYOUT_NODE_STEP - 1) // LAYOUT_NODE_STEP) * LAYOUT_NODE_STEP))
            complete_query = urlencode({"project": PROJECT, "max_nodes": max_nodes})
            layout = get_json(f"{api}/layout?{complete_query}")
            if len(layout.get("nodes", [])) != total_nodes:
                raise ExportError("Codebase Memory returned a truncated graph")
            if layout.get("total_nodes") != total_nodes:
                raise ExportError("Graph node total changed while exporting")
            indexed_nodes = indexed.get("expected_nodes", indexed.get("nodes"))
            indexed_edges = indexed.get("expected_edges", indexed.get("edges"))
            if isinstance(indexed_nodes, int) and indexed_nodes != total_nodes:
                raise ExportError("Indexed node count does not match the layout response")
            if isinstance(indexed_edges, int) and indexed_edges != len(layout.get("edges", [])):
                raise ExportError("Indexed edge count does not match the layout response")
            layout = filter_graph(layout)

            missed_query = urlencode({"project": PROJECT, "max_nodes": max_nodes, "graph": "missed"})
            missed = filter_graph(get_json(f"{api}/layout?{missed_query}"))
            repo_info.update(
                {
                    "root_path": ".",
                    "branch": os.environ.get("GITHUB_REF_NAME", ""),
                    "remote_url": "https://github.com/Jafarli-Mahammad/Liner-Notes",
                    "web_base": "",
                    "blob_base": "https://github.com/Jafarli-Mahammad/Liner-Notes/blob/main",
                }
            )
            health["watch_registered"] = False
            health["watcher_running"] = False
            health["nodes"] = layout["total_nodes"]
            health["edges"] = layout["total_edges"]
            health["indexed_at"] = status.get("indexed_at", health.get("indexed_at"))

            version = ui_config.get("version")
            if not isinstance(version, str) or not version:
                raise ExportError("Codebase Memory UI config did not report a version")
            ui_config["version"] = VIEWER_VERSION
            data_dir = output / "data"
            write_json(data_dir / "layout.json", layout)
            write_json(data_dir / "layout-missed.json", missed)
            write_json(data_dir / "repo-info.json", repo_info)
            write_json(data_dir / "ui-config.json", ui_config)
            write_json(data_dir / "project-health.json", health)
            write_json(data_dir / "adr.json", adr)
            metadata = {
                "source_commit": args.commit.lower(),
                "generated_at": generated_at,
                "codebase_memory_version": version,
                "viewer_version": VIEWER_VERSION,
                "node_count": layout["total_nodes"],
                "edge_count": layout["total_edges"],
                "missed_node_count": len(missed["nodes"]),
                "missed_edge_count": len(missed["edges"]),
            }
            write_json(data_dir / "metadata.json", metadata)
        finally:
            if client is not None:
                client.stop()

    static_dir = Path(__file__).parent / "static"
    vendor_dir = Path(__file__).parent / "vendor"
    assets_dir = output / "assets"
    assets_dir.mkdir(exist_ok=True)
    for name in ("index-BiHLJL7z.js", "index-Bk-h0GSQ.css"):
        shutil.copy2(vendor_dir / name, assets_dir / name)
    shutil.copy2(static_dir / "adapter.js", output / "adapter.js")
    shutil.copy2(vendor_dir / "NOTICE.md", output / "NOTICE.md")
    shutil.copy2(vendor_dir / "LICENSE", output / "LICENSE")
    html = (static_dir / "index.html").read_text(encoding="utf-8")
    html = html.replace("__SOURCE_COMMIT__", args.commit.lower())
    (output / "index.html").write_text(html, encoding="utf-8")
    print(f"Exported {metadata['node_count']:,} nodes and {metadata['edge_count']:,} edges for {args.commit[:7]}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ExportError, OSError, ValueError) as error:
        print(f"Nebula export failed: {error}", file=sys.stderr)
        raise SystemExit(1)
