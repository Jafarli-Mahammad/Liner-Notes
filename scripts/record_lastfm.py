"""Manifest-gated evaluation recorder. Phase 2 writes this tool but makes no live run.

Use JSON for the manifest; fields follow docs/renewal/contracts.md. An approved
manifest digest hashes canonical JSON with approval.approved_manifest_sha256=null.
The external --approved-sha256 must match too. Credentials are read only after
all preflight gates pass, and never appear in artifacts or exception messages.
"""
import argparse
import contextlib
import datetime as dt
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import struct
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request

from acquisition_guard import AcquisitionGuard, AcquisitionStopped, CATEGORIES, Inventory, integer

METHODS = frozenset({"artist.getSimilar", "artist.getTopTracks", "artist.getTopTags"})
STOP_ON = frozenset({"rate_limit", "auth_error", "permission_error", "byte_limit", "unknown_inventory", "cancellation"})
OUTPUTS = frozenset({"responses", "provenance", "gaps", "request_ledger", "shape_report", "evaluation", "blind_ratings"})
FOUNDER_SEEDS = frozenset({"jakuzi", "son feci bisiklet", "m.o.o.n.", "perturbator", "jasper byrne",
                          "paweł błaszczak", "heaven pierce her", "darren korb"})
HASH = re.compile(r"[0-9a-f]{64}\Z")
METADATA_BOUND = 16_384  # conservative reservation, verified before every write


def canonical(value):
    return unicodedata.normalize("NFC", value).strip().lower()


def encoded(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def sha(value):
    return hashlib.sha256(value).hexdigest()


def manifest_hash(manifest):
    copy = json.loads(encoded(manifest))
    copy["approval"]["approved_manifest_sha256"] = None
    return sha(encoded(copy))


def stamp(value):
    result = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    if result.utcoffset() != dt.timedelta(0):
        raise AcquisitionStopped("Timestamp must be UTC")
    return result


def reference(value):
    return isinstance(value, str) and bool(value.strip()) and "://" not in value and "<" not in value and len(value) <= 256


def check_manifest(m, approved_hash, now):
    try:
        def reject_secrets(value):
            if isinstance(value, dict):
                if any(str(key).lower() in {"api_key", "password", "secret", "token", "connection_string"} for key in value):
                    raise AcquisitionStopped("Credentials are forbidden in manifests")
                for item in value.values():
                    reject_secrets(item)
            elif isinstance(value, list):
                for item in value:
                    reject_secrets(item)
            elif isinstance(value, str) and ("api_key=" in value.lower() or "<" in value or len(value) > 4096):
                raise AcquisitionStopped("Placeholder or secret-bearing manifest value")
        reject_secrets(m)
        if len(encoded(m)) > 65_536:
            raise AcquisitionStopped("Manifest is too large")
        if m["manifest_version"] != 1 or m["phase"] not in (4, 5) or m["status"] != "approved" or m["provider"] != "Last.fm":
            raise AcquisitionStopped("Only approved Phase 4/5 manifests are executable")
        if not re.fullmatch(r"[A-Za-z0-9_-]{1,80}", m["run_id"]):
            raise AcquisitionStopped("Invalid run ID")
        approval = m["approval"]
        if not reference(approval["developer_reference"]) or not HASH.fullmatch(approved_hash) or \
                approval["approved_manifest_sha256"] != approved_hash or manifest_hash(m) != approved_hash:
            raise AcquisitionStopped("Missing or changed manifest approval")
        for field in ("profile_lock_sha256", "protocol_sha256"):
            if not HASH.fullmatch(m[field]):
                raise AcquisitionStopped("Missing profile/protocol lock")
        if not re.fullmatch(r"[0-9a-f]{40}", m["code_revision"]):
            raise AcquisitionStopped("Missing pinned code revision")
        verification = m["verification"]
        age = now.date() - dt.date.fromisoformat(verification["terms_date"])
        if age.days < 0 or age.days > 7 or not reference(verification["endpoint_contract_reference"]):
            raise AcquisitionStopped("Fresh endpoint/terms verification required")
        if m["phase"] == 5 and not any(reference(m["lastfm_gate"].get(key))
            for key in ("reply_reference", "explicit_waiver_reference")):
            raise AcquisitionStopped("Phase 5 Last.fm gate missing")
        if m["phase"] == 4 and m["label"] != "pilot, directional":
            raise AcquisitionStopped("Pilot label missing")
        if m["credential_source"] != "LASTFM_API_KEY":
            raise AcquisitionStopped("Credential source must be an environment variable name only")
        seeds = m["seeds"]
        names = [canonical(s["value"]) for s in seeds]
        if not seeds or len(set(names)) != len(names) or any(not name for name in names):
            raise AcquisitionStopped("Missing or duplicate literal seeds")
        if any(s["kind"] != "artist" or s["set"] not in ("founder", "development", "held-out") or
               not reference(s["cluster"]) or len(s["value"]) > 256 for s in seeds):
            raise AcquisitionStopped("Invalid literal seed contract")
        if m["phase"] == 4 and (len(seeds) != 11 or not FOUNDER_SEEDS.issubset(names) or
            any(s["set"] == "held-out" for s in seeds) or
            {canonical(s["value"]) for s in seeds if s["set"] == "founder"} != FOUNDER_SEEDS or
            len({s["cluster"] for s in seeds if s["set"] == "founder"}) != 6):
            raise AcquisitionStopped("Pilot requires eight founder seeds/six clusters and three development seeds")
        endpoints = m["endpoints"]
        if len(endpoints) != 3 or {e["method"] for e in endpoints} != METHODS:
            raise AcquisitionStopped("Endpoint allowlist mismatch")
        for endpoint in endpoints:
            integer(endpoint["max_calls"], "endpoint calls", 1)
            integer(endpoint["per_seed_limit"] if endpoint["method"] == "artist.getSimilar" else endpoint["per_artist_limit"], "endpoint limit", 1)
        request = m["request_budget"]
        if request["max_concurrency"] != 1 or request["automatic_retries"] != 0:
            raise AcquisitionStopped("Sequential execution without retries required")
        attempts = integer(request["max_attempts"], "attempts", 1)
        if sum(e["max_calls"] for e in endpoints) > attempts:
            raise AcquisitionStopped("Endpoint budgets exceed request budget")
        expansion = m["expansion"]
        if expansion["depth"] != 1 or expansion["order"] != "canonical_ordinal":
            raise AcquisitionStopped("Only bounded canonical depth-one expansion supported")
        artists = integer(expansion["max_distinct_artists_including_seeds"], "artist bound", len(seeds))
        pacing = m["pacing"]
        if integer(pacing["minimum_start_interval_ms"], "pacing", 1000) < 1000 or pacing["burst"] is not False or pacing["respect_stricter_server_limits"] is not True:
            raise AcquisitionStopped("Conservative sequential pacing required")
        if set(m["stop_on"]) != STOP_ON or set(m["outputs"]) != OUTPUTS:
            raise AcquisitionStopped("Required stop/output contract missing")
        if any(m["output_rules"].get(key) is not True for key in ("require_gitignored", "reject_tracked", "reject_overwrite", "reject_path_escape")):
            raise AcquisitionStopped("Output safety rules missing")
        response = m["response_budget"]
        single = integer(response["max_single_response_bytes"], "single response", 1)
        total = integer(response["hard_cap_bytes"], "response cap", single)
        integer(response["estimated_uncompressed_bytes"], "estimate", 1)
        if response["estimated_uncompressed_bytes"] > total:
            raise AcquisitionStopped("Estimate exceeds cap")
        storage = m["storage_budget"]
        integer(storage["run_directory_max_bytes"], "run cap", single + METADATA_BOUND)
        if storage["shared_stop_bytes"] != 80_000_000:
            raise AcquisitionStopped("Shared threshold must remain 80,000,000 bytes")
        if stamp(m["retention"]["expires_at_utc"]) <= now or m["retention"]["extension_requires_approval"] is not True:
            raise AcquisitionStopped("Explicit unexpired retention required")
        if m["phase"] == 4 and (attempts > 160 or artists > 66 or total > 5_000_000 or single > 100_000 or storage["run_directory_max_bytes"] > 6_000_000):
            raise AcquisitionStopped("Pilot exceeds approved planning ceilings")
    except (KeyError, TypeError, ValueError, OverflowError) as exc:
        raise AcquisitionStopped("Incomplete or invalid manifest") from None


def git(repo, *args):
    return subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True).stdout


def contained_output(repo, output, run_id):
    root = repo / "recordings"
    target = Path(output)
    if not target.is_absolute() or target != root / run_id or target.exists():
        raise AcquisitionStopped("Output must be a new absolute recordings/run-id path")
    for part in (root, target):
        if part.is_symlink() or part.resolve() != part:
            raise AcquisitionStopped("Output symlink/path escape")
    relative = str(target.relative_to(repo))
    if git(repo, "ls-files", "--", relative).strip():
        raise AcquisitionStopped("Output is tracked")
    result = subprocess.run(["git", "-C", str(repo), "check-ignore", "--no-index", "--quiet", "--", relative], capture_output=True)
    if result.returncode != 0:
        raise AcquisitionStopped("Output must be ignored by git")
    return target


def filesystem_snapshot(paths):
    """Detect external file changes without opening held-out response contents."""
    rows = []
    seen = set()
    for name in sorted(paths):
        path = Path(name)
        if not path.is_absolute() or not path.exists() or path.is_symlink():
            raise AcquisitionStopped("Unknown inventory path")
        files = [path] if path.is_file() else sorted(path.rglob("*"))
        for item in files:
            if item.is_symlink():
                raise AcquisitionStopped("Inventory symlink is not accountable")
            if item.is_file():
                if str(item) in seen:
                    raise AcquisitionStopped("Overlapping inventory roots")
                seen.add(str(item))
                stat = item.stat()
                rows.append((str(item), stat.st_size, stat.st_mtime_ns, stat.st_ino))
    return sorted(rows)


def filesystem_revision(paths):
    rows = filesystem_snapshot(paths)
    return sha(encoded(rows)), sum(row[1] for row in rows)


def reconcile_local_write(paths, before, created):
    after = filesystem_snapshot(paths)
    previous = {row[0]: row for row in before}
    current = {row[0]: row for row in after}
    if any(current.get(name) != row for name, row in previous.items()) or \
            set(current) - set(previous) != {str(path) for path in created}:
        raise AcquisitionStopped("External inventory change during output write")
    return sha(encoded(after))


def load_inventory(m, now):
    contract = m["storage_inventory"]
    path = Path(contract["path"])
    if not path.is_absolute() or path.is_symlink():
        raise AcquisitionStopped("Inventory path must be absolute and regular")
    raw = path.read_bytes()
    if sha(raw) != contract["sha256"]:
        raise AcquisitionStopped("Inventory hash mismatch")
    inv = json.loads(raw)
    measured = stamp(inv["measured_at_utc"])
    if contract["measured_at_utc"] != inv["measured_at_utc"] or not dt.timedelta(0) <= now - measured <= dt.timedelta(minutes=5):
        raise AcquisitionStopped("Inventory must be reconciled at run start")
    if set(inv["categories"]) != CATEGORIES or inv["reconciled"] is not True or inv["exclusive_acquisition_reference"] is None:
        raise AcquisitionStopped("Complete reconciled inventory and exclusive-acquisition reference required")
    paths = inv["filesystem_roots"]
    revision, disk_bytes = filesystem_revision(paths)
    if revision != inv["filesystem_revision"] or disk_bytes != inv["filesystem_bytes"]:
        raise AcquisitionStopped("External inventory change")
    snapshot = Inventory(inv["categories"], revision, True)
    if snapshot.total() < disk_bytes:
        raise AcquisitionStopped("Inventory undercounts disk data")
    return snapshot, paths


def verify_local_locks(m, repo):
    """Verify actual membership/protocol, not just hash-shaped strings in a manifest.

    profile_lock_path and protocol_path are concrete-run additions to the planning
    template. They must point at ignored/untracked local files. Held-out seed
    metadata is allowed here; held-out responses and statistics are never read.
    """
    def local_file(field):
        path = Path(m[field])
        root = repo / "recordings"
        if not path.is_absolute() or not path.is_relative_to(root) or path.resolve() != path or not path.is_file():
            raise AcquisitionStopped("Lock path escapes recordings or is missing")
        relative = str(path.relative_to(repo))
        if git(repo, "ls-files", "--", relative).strip():
            raise AcquisitionStopped("Generated lock/protocol must not be tracked")
        if subprocess.run(["git", "-C", str(repo), "check-ignore", "--no-index", "--quiet", "--", relative], capture_output=True).returncode != 0:
            raise AcquisitionStopped("Generated lock/protocol must be ignored")
        if path.stat().st_size > 1_000_000:
            raise AcquisitionStopped("Oversized lock/protocol")
        return path.read_bytes()
    lock = json.loads(local_file("profile_lock_path"))
    if lock["version"] != "profile-lock-v1-nfc-invariant" or not reference(lock["review_reference"]):
        raise AcquisitionStopped("Reviewed profile lock required")
    profiles = sorted(lock["profiles"], key=lambda p: p["id"])
    chunks = []
    def add(value):
        data = value.encode("utf-8")
        chunks.extend([struct.pack(">i", len(data)), data])
    add(lock["version"])
    for p in profiles:
        add(p["id"]); add(p["set"]); add(p["genre"]); add(str(len(p["seeds"])))
        for seed in sorted(p["seeds"]):
            add(seed)
    digest = sha(b"".join(chunks))
    if digest != lock["hash"] or digest != m["profile_lock_sha256"]:
        raise AcquisitionStopped("Profile lock hash mismatch")
    seen = set()
    for profile in profiles:
        if profile["id"] in seen:
            raise AcquisitionStopped("Duplicate locked profile ID")
        seen.add(profile["id"])
    lookup = {"founder": "Founder", "development": "Development", "held-out": "HeldOut"}
    for seed in m["seeds"]:
        if not any(p["set"] == lookup[seed["set"]] and canonical(seed["value"]) in p["seeds"] for p in profiles):
            raise AcquisitionStopped("Manifest seed is outside its reviewed partition")
    if sha(local_file("protocol_path")) != m["protocol_sha256"]:
        raise AcquisitionStopped("Protocol hash mismatch")


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *unused):
        raise AcquisitionStopped("Redirect rejected")


def live_transport(method, artist, limit, credential):
    params = {"method": method, "artist": artist, "autocorrect": "0", "api_key": credential, "format": "json"}
    if method != "artist.getTopTags":  # topTags has no documented limit parameter
        params["limit"] = str(limit)
    request = urllib.request.Request("https://ws.audioscrobbler.com/2.0/?" + urllib.parse.urlencode(params),
        headers={"User-Agent": "LinerNotes-EvaluationRecorder/1", "Accept-Encoding": "identity"})
    try:
        return urllib.request.build_opener(NoRedirect()).open(request, timeout=30)
    except (urllib.error.URLError, OSError):
        raise AcquisitionStopped("HTTP failure; acquisition stopped without retry") from None


def read_bounded(response, maximum, cancelled):
    if response.headers.get("Content-Encoding", "identity").lower() != "identity":
        raise AcquisitionStopped("Compressed response rejected; uncompressed bounds required")
    chunks, size = [], 0
    while True:
        if cancelled():
            raise AcquisitionStopped("Cancelled")
        chunk = response.read(min(8192, maximum + 1 - size))
        if not chunk:
            return b"".join(chunks)
        size += len(chunk)
        if size > maximum:
            raise AcquisitionStopped("Oversized partial response discarded")
        chunks.append(chunk)


def write_new(path, data):
    with path.open("xb") as output:
        output.write(data)


@contextlib.contextmanager
def writer_lease(root):
    root.mkdir(mode=0o700, exist_ok=True)
    path = root / ".acquisition.lock"
    fd = os.open(path, os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    try:
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise AcquisitionStopped("Another recorder owns acquisition") from None
        yield
    finally:
        os.close(fd)


def record(m, repo, inventory, roots, transport, credential, cancelled=lambda: False, sleep=time.sleep, clock=time.monotonic):
    """Call only after preflight and under writer_lease. Tests inject every transport."""
    target = Path(m["output_directory"])
    if not any((repo / "recordings").is_relative_to(Path(root)) for root in roots):
        raise AcquisitionStopped("Inventory roots must include recordings")
    guard = AcquisitionGuard(inventory, m["storage_budget"]["run_directory_max_bytes"], m["response_budget"]["max_single_response_bytes"])
    # Bootstrap bytes include the retained manifest and a bounded final status record.
    bootstrap = encoded(m)
    before = filesystem_snapshot(roots)
    with guard.reserve(0, len(bootstrap) + METADATA_BOUND, sha(encoded(before)), cancelled()) as reservation:
        target.mkdir(mode=0o700, exist_ok=False)
        write_new(target / "manifest.json", bootstrap)
        revision = reconcile_local_write(roots, before, [target / "manifest.json"])
        reservation.commit(0, len(bootstrap) + METADATA_BOUND, revision)
    endpoints = {e["method"]: e for e in m["endpoints"]}
    endpoint_calls = dict.fromkeys(METHODS, 0)
    calls, response_bytes, last_start = 0, 0, None
    artists = {canonical(s["value"]): s["value"] for s in m["seeds"]}
    status = "incomplete"

    def acquire(method, artist):
        nonlocal calls, response_bytes, last_start
        e = endpoints[method]
        if calls >= m["request_budget"]["max_attempts"] or endpoint_calls[method] >= e["max_calls"]:
            raise AcquisitionStopped("Request budget exhausted")
        maximum = m["response_budget"]["max_single_response_bytes"]
        if response_bytes + maximum > m["response_budget"]["hard_cap_bytes"]:
            raise AcquisitionStopped("Projected response budget exhausted")
        with guard.reserve(maximum, METADATA_BOUND, filesystem_revision(roots)[0], cancelled()) as reservation:
            interval = m["pacing"]["minimum_start_interval_ms"] / 1000
            if last_start is not None:
                sleep(max(0, interval - (clock() - last_start)))
            if cancelled():
                raise AcquisitionStopped("Cancelled")
            last_start = clock()
            calls += 1
            endpoint_calls[method] += 1
            started = dt.datetime.now(dt.timezone.utc).isoformat()
            limit = e.get("per_seed_limit", e.get("per_artist_limit"))
            entry = {"sequence": calls, "method": method, "artist": artist, "started_at_utc": started,
                     "provenance": "recorded", "provider": "Last.fm", "label": m["label"], "status": "gap"}
            raw, parsed, failure = b"", None, None
            try:
                with transport(method, artist, limit, credential) as response:
                    raw = read_bounded(response, maximum, cancelled)
                    if filesystem_revision(roots)[0] != guard.inventory.revision:
                        raise AcquisitionStopped("External inventory change during request")
                    parsed = json.loads(raw)
                    if not isinstance(parsed, dict):
                        raise AcquisitionStopped("Invalid response shape")
                    if parsed.get("error"):
                        raise AcquisitionStopped("Provider error; no retry")
                    if response.headers.get("Retry-After"):
                        raise AcquisitionStopped("Server requires stricter pacing; stop for revised approval")
                    if credential.encode("utf-8") in raw or b"api_key=" in raw.lower():
                        raise AcquisitionStopped("Credential-bearing response rejected")
                    entry.update({"status": "recorded", "response_sha256": sha(raw), "bytes": len(raw),
                        "headers": {key: response.headers[key] for key in ("Cache-Control", "Expires", "Date", "Retry-After") if key in response.headers}})
                    if any(len(value) > 512 for value in entry["headers"].values()):
                        raise AcquisitionStopped("Oversized response metadata")
                    if credential.encode("utf-8") in encoded(entry):
                        raise AcquisitionStopped("Credential-bearing metadata rejected")
            except (AcquisitionStopped, ValueError, OSError):
                # Do not log exception text: transports can include credential-bearing URLs.
                failure = AcquisitionStopped("Request failed or was incomplete; see sanitized gap ledger")
                raw, parsed = b"", None
                entry = {"sequence": calls, "method": method, "artist": artist, "started_at_utc": started,
                    "provenance": "recorded", "provider": "Last.fm", "label": m["label"],
                    "status": "gap", "reason": "response_failed_or_incomplete"}
            metadata = encoded(entry)
            if len(metadata) > METADATA_BOUND or filesystem_revision(roots)[0] != guard.inventory.revision:
                raise AcquisitionStopped("Metadata bound or external inventory change")
            before = filesystem_snapshot(roots)
            if sha(encoded(before)) != guard.inventory.revision:
                raise AcquisitionStopped("External inventory change before output write")
            created = []
            if raw:
                response_path = target / f"{calls:04d}-response.json"
                write_new(response_path, raw)
                created.append(response_path)
            ledger_path = target / f"{calls:04d}-ledger.json"
            write_new(ledger_path, metadata)
            created.append(ledger_path)
            reservation.commit(len(raw), len(metadata), reconcile_local_write(roots, before, created), False)
            response_bytes += len(raw)
            if failure:
                raise failure
            return parsed

    try:
        for seed in sorted(m["seeds"], key=lambda s: canonical(s["value"])):
            data = acquire("artist.getSimilar", seed["value"])
            candidates = data.get("similarartists", {}).get("artist", [])
            if isinstance(candidates, dict):
                candidates = [candidates]
            if not isinstance(candidates, list):
                raise AcquisitionStopped("Invalid similar artist shape")
            limit = endpoints["artist.getSimilar"]["per_seed_limit"]
            # Preserve raw response in full; only acquisition expansion is bounded.
            for artist in candidates[:limit]:
                name = artist.get("name") if isinstance(artist, dict) else None
                if isinstance(name, str) and name.strip() and len(name) <= 256:
                    artists.setdefault(canonical(name), name)
        if len(artists) > m["expansion"]["max_distinct_artists_including_seeds"]:
            raise AcquisitionStopped("Artist expansion budget exceeded")
        for key in sorted(artists):
            acquire("artist.getTopTags", artists[key])
            acquire("artist.getTopTracks", artists[key])
        status = "acquisition_complete"
    finally:
        # Reserved at bootstrap; no response statistics or held-out summaries emitted.
        final = encoded({"status": status, "requests": calls, "response_bytes": response_bytes,
                         "label": m["label"], "held_out_sealed": m["phase"] == 5,
                         "no_scoring_performed": True})
        before = filesystem_snapshot(roots)
        if len(final) > METADATA_BOUND or sha(encoded(before)) != guard.inventory.revision:
            raise AcquisitionStopped("Cannot safely write final state")
        write_new(target / "status.json", final)
        reconcile_local_write(roots, before, [target / "status.json"])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--approved-sha256", required=True)
    parser.add_argument("--execute-approved-run", action="store_true")
    args = parser.parse_args()
    try:
        now = dt.datetime.now(dt.timezone.utc)
        m = json.loads(args.manifest.read_bytes())
        check_manifest(m, args.approved_sha256, now)
        repo = Path(__file__).resolve().parent.parent
        if git(repo, "rev-parse", "HEAD").decode().strip() != m["code_revision"] or git(repo, "status", "--porcelain", "--untracked-files=normal").strip():
            raise AcquisitionStopped("Pinned clean checkout required")
        contained_output(repo, m["output_directory"], m["run_id"])
        verify_local_locks(m, repo)
        if not args.execute_approved_run:
            raise AcquisitionStopped("Live execution requires the explicit execution flag")
        with writer_lease(repo / "recordings"):
            inventory, roots = load_inventory(m, now)
            credential = os.environ.get(m["credential_source"])
            if not credential:
                raise AcquisitionStopped("Missing API credential")
            record(m, repo, inventory, roots, live_transport, credential)
    except (AcquisitionStopped, KeyError, TypeError, ValueError, OSError, KeyboardInterrupt, subprocess.SubprocessError):
        # All diagnostics are constant and secret-free, including library failures.
        parser.exit(2, "Recorder stopped; preflight/manifest, inventory or bounded acquisition failed. No automatic retry.\n")


if __name__ == "__main__":
    main()
