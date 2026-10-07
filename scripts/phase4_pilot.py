"""Phase 4 preparation and offline analysis. This script never acquires upstream data.

The separate record_lastfm.py remains the only live entry point. Concrete manifests
need developer approval; generated controls and reports stay local and untracked.
"""
import argparse
import base64
import copy
import datetime as dt
import json
import os
import re
from pathlib import Path
import secrets
import subprocess
import tempfile

from acquisition_guard import AcquisitionGuard, AcquisitionStopped
import record_lastfm as recorder

LABEL = "pilot, directional"
TRANSIENT_COPY_BOUND = 16_000_000  # bounded replay input plus captured output, also reserved against shared cap
EXTRA_SEEDS = ("Agalloch", "Pharoah Sanders", "Altın Gün")


def read_json(path, bound=1_000_000):
    path = Path(path)
    if path.is_symlink() or not path.is_file() or path.stat().st_size > bound:
        raise AcquisitionStopped("Missing, linked or oversized explicit artifact")
    return json.loads(path.read_bytes())


def write_new(path, body):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "wb") as output:
        output.write(body)


def bridge(repo, command, path=None):
    args = ["dotnet", "run", "--file", "scripts/phase4_replay.cs", "--no-restore", "--no-cache", "--", command]
    if path is not None:
        args.append(str(path))
    completed = subprocess.run(args, cwd=repo, capture_output=True, check=False)
    if completed.returncode or len(completed.stdout) > 8_000_000:
        raise AcquisitionStopped("Offline C# bridge failed or exceeded output bound")
    lines = [line[len(b"PHASE4_JSON="):] for line in completed.stdout.splitlines() if line.startswith(b"PHASE4_JSON=")]
    if len(lines) != 1:
        raise AcquisitionStopped("Missing bounded bridge result")
    return json.loads(lines[0])


def clean_revision(repo, revision=None):
    current = recorder.git(repo, "rev-parse", "HEAD").decode().strip()
    if recorder.git(repo, "status", "--porcelain", "--untracked-files=normal").strip() or (revision and current != revision):
        raise AcquisitionStopped("Pinned clean checkout required")
    return current


def prepare(repo, run_id, expires, terms_date, endpoint_reference, metadata, now=None):
    now = now or dt.datetime.now(dt.timezone.utc)
    if not re.fullmatch(r"[A-Za-z0-9_-]{1,71}", run_id) or recorder.stamp(expires) <= now or not recorder.reference(endpoint_reference):
        raise AcquisitionStopped("Explicit future UTC expiry and safe run/reference required")
    revision = clean_revision(repo)
    controls = recorder.contained_output(repo, str(repo / "recordings" / (run_id + "-controls")), run_id + "-controls")
    recorder.contained_output(repo, str(repo / "recordings" / run_id), run_id)
    lock, protocol = metadata["profileLock"], metadata["protocol"]
    seeds = [dict(set="founder", cluster=p["genre"], kind="artist", value=s)
             for p in lock["profiles"] if p["set"] == "Founder" for s in p["seeds"]]
    seeds += [dict(set="development", cluster="extra", kind="artist", value=s) for s in EXTRA_SEEDS]
    # Acquisition guard also counts these local controls. No response content is read here.
    with recorder.writer_lease(repo / "recordings"):
        _, used = recorder.filesystem_revision([str(repo / "recordings")])
        if used + 100_000 >= 80_000_000:
            raise AcquisitionStopped("Insufficient space for pilot controls")
        controls.mkdir(mode=0o700)
        lock_path, protocol_path = controls / "profile-lock.json", controls / "protocol.json"
        write_new(lock_path, recorder.encoded(lock))
        protocol_bytes = recorder.encoded(protocol)
        write_new(protocol_path, protocol_bytes)
        manifest = dict(manifest_version=1, run_id=run_id, phase=4, status="draft", label=LABEL,
            approval=dict(developer_reference=None, approved_manifest_sha256=None),
            lastfm_gate=dict(reply_reference=None, explicit_waiver_reference=None),
            verification=dict(terms_date=terms_date, endpoint_contract_reference=endpoint_reference),
            profile_lock_path=str(lock_path), profile_lock_sha256=lock["hash"],
            protocol_path=str(protocol_path), protocol_sha256=recorder.sha(protocol_bytes), code_revision=revision,
            provider="Last.fm", credential_source="LASTFM_API_KEY", seeds=seeds,
            endpoints=[dict(method="artist.getSimilar", per_seed_limit=5, max_calls=11),
                dict(method="artist.getTopTracks", per_artist_limit=5, max_calls=66),
                dict(method="artist.getTopTags", per_artist_limit=15, max_calls=66)],
            expansion=dict(depth=1, max_distinct_artists_including_seeds=66, order="canonical_ordinal"),
            request_budget=dict(max_attempts=143, max_concurrency=1, automatic_retries=0),
            pacing=dict(minimum_start_interval_ms=1000, burst=False, respect_stricter_server_limits=True),
            stop_on=sorted(recorder.STOP_ON), outputs=sorted(recorder.OUTPUTS),
            response_budget=dict(estimated_uncompressed_bytes=4_000_000, hard_cap_bytes=5_000_000, max_single_response_bytes=100_000),
            storage_budget=dict(run_directory_max_bytes=6_000_000, shared_stop_bytes=80_000_000),
            storage_inventory=dict(path=None, sha256=None, measured_at_utc=None),
            output_directory=str(repo / "recordings" / run_id),
            output_rules=dict(require_gitignored=True, reject_tracked=True, reject_overwrite=True, reject_path_escape=True),
            retention=dict(expires_at_utc=expires, extension_requires_approval=True))
        recorder.verify_local_locks(manifest, repo)
        write_new(controls / "draft-manifest.json", recorder.encoded(manifest))
    return controls / "draft-manifest.json"


def inventory_contract(path):
    path = Path(path).absolute()
    value = read_json(path)
    return dict(path=str(path), sha256=recorder.sha(path.read_bytes()), measured_at_utc=value["measured_at_utc"])


def propose(repo, draft_path, inventory_path, reference, output, now):
    manifest = read_json(draft_path)
    if manifest["status"] != "draft" or manifest["phase"] != 4:
        raise AcquisitionStopped("Expected a Phase 4 draft")
    clean_revision(repo, manifest["code_revision"])
    recorder.verify_local_locks(manifest, repo)
    recorder.contained_output(repo, manifest["output_directory"], manifest["run_id"])
    manifest["storage_inventory"] = inventory_contract(inventory_path)
    manifest["status"] = "approved"  # candidate bytes only: the displayed digest still needs human approval
    manifest["approval"]["developer_reference"] = reference
    digest = recorder.manifest_hash(manifest)
    manifest["approval"]["approved_manifest_sha256"] = digest
    recorder.check_manifest(manifest, digest, now)
    output = Path(output)
    if not output.is_absolute() or output.resolve() != output or output.is_relative_to(repo):
        raise AcquisitionStopped("Proposed manifest must be a new explicit temporary file outside measured repository roots")
    # Manifest/inventory files cannot be inside the measured roots: a pinned inventory
    # containing its own hash is self-referential. Their bytes are separately accounted as copies.
    inventory, roots = recorder.load_inventory(manifest, now)
    body = recorder.encoded(manifest)
    if any(output.is_relative_to(Path(root)) for root in roots) or inventory.categories["copies"] < len(body) + Path(inventory_path).stat().st_size:
        raise AcquisitionStopped("Temporary manifest/inventory copies must be explicitly included in inventory")
    write_new(output, body)
    return digest


def approved_run(repo, manifest_path, approved_hash, now):
    manifest = read_json(manifest_path, 65_536)
    recorder.check_manifest(manifest, approved_hash, now)
    if manifest["phase"] != 4:
        raise AcquisitionStopped("Phase 5 content remains sealed")
    clean_revision(repo, manifest["code_revision"])
    recorder.verify_local_locks(manifest, repo)
    target = Path(manifest["output_directory"])
    if target != repo / "recordings" / manifest["run_id"] or target.resolve() != target or not target.is_dir():
        raise AcquisitionStopped("Explicit approved recording directory required")
    if recorder.git(repo, "ls-files", "--", str(target.relative_to(repo))).strip() or subprocess.run(
            ["git", "-C", str(repo), "check-ignore", "--no-index", "--quiet", "--", str(target)], capture_output=True).returncode:
        raise AcquisitionStopped("Recording directory must be ignored and untracked")
    if recorder.manifest_hash(read_json(target / "manifest.json", 65_536)) != approved_hash:
        raise AcquisitionStopped("Retained manifest differs from approved run")
    return manifest, target


def replay_input(manifest, target):
    status = read_json(target / "status.json", recorder.METADATA_BOUND)
    attempts = status["requests"]
    if type(attempts) is not int or not 0 <= attempts <= manifest["request_budget"]["max_attempts"] or status["label"] != LABEL:
        raise AcquisitionStopped("Invalid bounded acquisition status")
    endpoints = {e["method"]: e for e in manifest["endpoints"]}
    responses, gaps, response_bytes, calls = [], [], 0, dict.fromkeys(endpoints, 0)
    for sequence in range(1, attempts + 1):
        entry = read_json(target / f"{sequence:04d}-ledger.json", recorder.METADATA_BOUND)
        method = entry["method"]
        if entry["sequence"] != sequence or method not in endpoints or entry["label"] != LABEL or \
                entry["provider"] != "Last.fm" or entry["provenance"] != "recorded" or not recorder.reference(entry["artist"]):
            raise AcquisitionStopped("Ledger provenance/sequence mismatch")
        calls[method] += 1
        if calls[method] > endpoints[method]["max_calls"]:
            raise AcquisitionStopped("Endpoint request budget exceeded")
        if entry["status"] == "gap":
            gaps.append(dict(Seed=entry["artist"], Method=method.lower(), Reason=entry["reason"], Response=None))
            continue
        if entry["status"] != "recorded":
            raise AcquisitionStopped("Unknown ledger status")
        path = target / f"{sequence:04d}-response.json"
        if path.is_symlink() or path.stat().st_size > manifest["response_budget"]["max_single_response_bytes"]:
            raise AcquisitionStopped("Linked/oversized response")
        body = path.read_bytes()
        if len(body) != entry["bytes"] or recorder.sha(body) != entry["response_sha256"]:
            raise AcquisitionStopped("Response bytes/hash mismatch")
        retrieved = recorder.stamp(entry["retrieved_at_utc"])
        if retrieved < recorder.stamp(entry["started_at_utc"]):
            raise AcquisitionStopped("Response retrieval precedes request")
        limit = endpoints[method].get("per_seed_limit", endpoints[method].get("per_artist_limit"))
        responses.append(dict(Method=method, Artist=entry["artist"], Body=base64.b64encode(body).decode("ascii"),
            Sha256=entry["response_sha256"], RetrievedAtUtc=entry["retrieved_at_utc"], Limit=limit))
        response_bytes += len(body)
    if response_bytes != status["response_bytes"] or response_bytes > manifest["response_budget"]["hard_cap_bytes"]:
        raise AcquisitionStopped("Response budget/accounting mismatch")
    if status["status"] not in ("acquisition_complete", "incomplete"):
        raise AcquisitionStopped("Unknown acquisition status")
    return dict(Protocol=read_json(manifest["protocol_path"]), Seeds=[dict(Set=s["set"], Cluster=s["cluster"], Value=s["value"]) for s in manifest["seeds"]],
        Responses=responses, Attempts=attempts, AcquisitionComplete=status["status"] == "acquisition_complete", LedgerGaps=gaps, Label=LABEL)


def blind_artifacts(evaluation, profiles, concealed_seed=None):
    concealed_seed = concealed_seed or secrets.token_hex(32)
    items, private = [], []
    for profile_id in sorted(evaluation["Rankings"]["A"]):
        pool = {}
        for rankings in evaluation["Rankings"].values():
            for pick in rankings[profile_id][:5]:
                pool[pick["Key"]] = pick
        ordered = sorted(pool, key=lambda key: recorder.sha(recorder.encoded(["pilot-blind-v1", concealed_seed, profile_id, key])))
        for key in ordered:
            slot = recorder.sha(recorder.encoded(["pilot-slot-v1", concealed_seed, profile_id, key]))
            pick = pool[key]
            items.append(dict(profile=profile_id, cluster=profiles[profile_id], id=slot, artist=pick["Artist"], title=pick["Title"]))
            private.append(dict(profile=profile_id, id=slot, key=key))
    if len(items) > 120:
        raise AcquisitionStopped("Blind workload exceeds predeclared maximum")
    return {
        "blind-review.json": dict(label=LABEL, instructions="Rate every track like=1, neutral=0 or dislike=-1. Formula/rank/score/explanation concealed. Pilot only.", items=items),
        "blind-ratings-template.json": dict(label=LABEL, ratings=[dict(profile=i["profile"], id=i["id"], rating=None) for i in items]),
        ".blind-key.json": dict(label=LABEL, concealed_seed=concealed_seed, mapping=private)
    }


def shape_markdown(report):
    shape = report["Shape"]
    lines = [f"# {LABEL} — shape report", "", "All values are pilot, directional. No tuning or promotion.", "",
        "| pilot, directional: seed | raw paths | distinct tracks | eligible tracks | artists |", "|---|---:|---:|---:|---:|"]
    lines += [f"| {s['Seed'].replace('|', '&#124;')} | {s['RawPaths']} | {s['DistinctTracks']} | {s['EligibleTracks']} | {s['Artists']} |" for s in shape["Seeds"]]
    lines += ["", "| pilot, directional: gate | numerator | denominator | required | pass |", "|---|---:|---:|---:|---|"]
    lines += [f"| {g['Name']} | {g['Numerator']} | {g['Denominator']} | {g['Required']} | {g['Pass']} |" for g in shape["Gates"]]
    lines += ["", f"Comparisons permitted: {shape['CanCompare']}. C assessable: {shape['MatchAssessable']}.",
        f"Authentic track listeners: {shape['ListenersKnown']}/{shape['ListenersTotal']} eligible candidates. Artist-wide listeners are unknown.",
        f"Raw noise: {shape['NoiseTagEntries']}/{shape['RawTagEntries']} artist/tag entries. Ambiguous-tag classification: {shape['AmbiguityStatus']}.",
        "", report["AcquisitionScope"], "", report["CorpusComposition"], "", "See JSON for all gaps, request denominators and normalized evidence."]
    return ("\n".join(lines) + "\n").encode("utf-8")


def run_categories(target, outputs):
    categories = dict(responses=0, provenance=0, reports=0, derived=0)
    for path in target.iterdir():
        if path.is_symlink() or not path.is_file():
            raise AcquisitionStopped("Unexpected recording directory entry")
        name = path.name
        category = "responses" if name.endswith("-response.json") else "provenance" if name.endswith("-ledger.json") or name in ("manifest.json", "status.json") else "derived" if name in ("evidence.json", ".blind-key.json") else "reports"
        categories[category] += path.stat().st_size
    for name, body in outputs.items():
        categories["derived" if name in ("evidence.json", ".blind-key.json") else "reports"] += len(body)
    return categories


def write_outputs(manifest, target, inventory, roots, outputs):
    if any((target / name).exists() or Path(name).name != name for name in outputs):
        raise AcquisitionStopped("Refuse output overwrite or path escape")
    used = sum(p.stat().st_size for p in target.iterdir())
    guard = AcquisitionGuard(inventory, manifest["storage_budget"]["run_directory_max_bytes"] - used,
        manifest["response_budget"]["max_single_response_bytes"])
    before = recorder.filesystem_snapshot(roots)
    with guard.reserve(0, sum(len(b) for b in outputs.values()), recorder.sha(recorder.encoded(before))) as reservation:
        created = []
        for name, body in outputs.items():
            path = target / name
            write_new(path, body)
            created.append(path)
        reservation.commit(0, sum(len(b) for b in outputs.values()), recorder.reconcile_local_write(roots, before, created))
    return guard.inventory


def analyze(repo, manifest_path, approved_hash, inventory_path, now, invoke=bridge):
    manifest, target = approved_run(repo, manifest_path, approved_hash, now)
    if any((target / name).exists() for name in ("analysis-index.json", "shape-report.json", "evidence.json")):
        raise AcquisitionStopped("Analysis already exists; no automatic rerun")
    with recorder.writer_lease(repo / "recordings"):
        accounting = copy.deepcopy(manifest)
        accounting["storage_inventory"] = inventory_contract(inventory_path)
        inventory, roots = recorder.load_inventory(accounting, now)
        if inventory.total() + TRANSIENT_COPY_BOUND + manifest["storage_budget"]["run_directory_max_bytes"] >= 80_000_000:
            raise AcquisitionStopped("Insufficient shared capacity for transient replay copies and reports")
        payload = recorder.encoded(replay_input(manifest, target))
        if len(payload) > 8_000_000:
            raise AcquisitionStopped("Replay input exceeds transient bound")
        with tempfile.TemporaryDirectory(prefix="liner-phase4-") as temporary:
            path = Path(temporary) / "explicit-input.json"
            write_new(path, payload)
            report = invoke(repo, "shape", path)
            if report["Label"] != LABEL or report["Protocol"] != read_json(manifest["protocol_path"]):
                raise AcquisitionStopped("Replay protocol/label mismatch")
            # Raw responses already retain every observation. Derived evidence references those hashes
            # once, avoiding per-formula duplication of artist tags and random Domain entity IDs.
            evidence = dict(label=LABEL, protocol_sha256=manifest["protocol_sha256"],
                artists=[dict(identity=a["Identity"], weights=a["Vector"]["Weights"], response=a["Response"]) for a in report["Artists"]],
                candidates=[{k: v for k, v in c.items() if k != "Tags"} for c in report["Candidates"]])
            outputs = {"shape-report.json": recorder.encoded(dict(label=LABEL, shape=report["Shape"], requests=report["Requests"],
                    acquisition_scope=report["AcquisitionScope"], corpus_composition=report["CorpusComposition"])),
                "shape-report.md": shape_markdown(report), "evidence.json": recorder.encoded(evidence)}
            # Leave a small index reservation so an aborted comparison can still be reported.
            if sum(len(body) for body in outputs.values()) + sum(p.stat().st_size for p in target.iterdir()) + 4096 > manifest["storage_budget"]["run_directory_max_bytes"]:
                raise AcquisitionStopped("Insufficient run budget for shape evidence and completion index")
            inventory = write_outputs(manifest, target, inventory, roots, outputs)
            comparison_status = "blocked_adequacy"
            if report["Shape"]["CanCompare"]:
                try:
                    comparison = invoke(repo, "replay", path)
                    if comparison["Label"] != LABEL or comparison["Protocol"] != report["Protocol"] or comparison["Shape"] != report["Shape"] or comparison["Evaluation"] is None:
                        raise AcquisitionStopped("Frozen shape/protocol differs at comparison")
                    profiles = {p["id"]: p["genre"] for p in read_json(manifest["profile_lock_path"])["profiles"] if p["set"] == "Founder"}
                    additional = {"evaluation.json": recorder.encoded(comparison["Evaluation"])}
                    additional.update({name: recorder.encoded(value) for name, value in blind_artifacts(comparison["Evaluation"], profiles).items()})
                    if sum(len(body) for body in additional.values()) + sum(p.stat().st_size for p in target.iterdir()) + 4096 > manifest["storage_budget"]["run_directory_max_bytes"]:
                        raise AcquisitionStopped("Insufficient run budget for comparison and completion index")
                    inventory = write_outputs(manifest, target, inventory, roots, additional)
                    outputs.update(additional)
                    comparison_status = "ready_for_blind_review"
                except AcquisitionStopped:
                    # No rerun/expansion. Shape is already durable; malformed/oversized comparison is unassessable.
                    comparison_status = "blocked_bounded_offline_comparison"
        index = dict(label=LABEL, approved_manifest_sha256=approved_hash, code_revision=manifest["code_revision"],
            measurement="after analysis, before blind ratings",
            comparison_status=comparison_status,
            protocol_sha256=manifest["protocol_sha256"], inventory_sha256=accounting["storage_inventory"]["sha256"],
            artifacts={name: recorder.sha(body) for name, body in outputs.items()}, bytes_by_category={}, run_bytes=0)
        index_outputs = {}
        for _ in range(10):
            index_outputs["analysis-index.json"] = recorder.encoded(index)
            counts = run_categories(target, index_outputs)
            if counts == index["bytes_by_category"]:
                break
            index["bytes_by_category"] = counts
            index["run_bytes"] = sum(counts.values())
        else:
            raise AcquisitionStopped("Cannot reconcile report sizes")
        write_outputs(manifest, target, inventory, roots, index_outputs)
    return comparison_status == "ready_for_blind_review"


def rate_result(evaluation, key, ratings):
    if set(ratings) != {"label", "ratings"} or any(set(row) != {"profile", "id", "rating"} for row in ratings["ratings"]):
        raise AcquisitionStopped("Ratings contain undeclared fields")
    expected = {(i["profile"], i["id"]): i["key"] for i in key["mapping"]}
    supplied = {}
    for row in ratings["ratings"]:
        identity = (row["profile"], row["id"])
        if identity in supplied or identity not in expected or type(row["rating"]) is not int or row["rating"] not in (-1, 0, 1):
            raise AcquisitionStopped("Every blind slot requires exactly one valid rating")
        supplied[identity] = row["rating"]
    if ratings["label"] != LABEL or set(supplied) != set(expected):
        raise AcquisitionStopped("Blind ratings are incomplete or from another pool")
    by_track = {(profile, expected[(profile, slot)]): rating for (profile, slot), rating in supplied.items()}
    scores = {formula: {profile: sum(by_track[(profile, p["Key"])] for p in picks[:5]) / 5
        for profile, picks in profiles.items()} for formula, profiles in evaluation["Rankings"].items()}
    if len(scores["A"]) != 6:
        raise AcquisitionStopped("Expected six founder profiles")
    results = []
    for formula in sorted(scores):
        differences = {profile: scores[formula][profile] - scores["A"][profile] for profile in scores["A"]}
        mean = sum(differences.values()) / 6
        positive = sum(v > 0 for v in differences.values())
        results.append(dict(formula=formula, per_profile=scores[formula], mean=sum(scores[formula].values()) / 6,
            differences_against_a=differences, mean_difference=mean, positive_profiles=positive,
            directional_margin_met=mean >= .20 - 1e-12 and positive >= 4))
    return dict(label=LABEL, results=results, conclusion="Pilot, directional founder preference evidence only. No tuning or formula promotion.")


def ratings(repo, manifest_path, approved_hash, ratings_path, inventory_path, now):
    manifest, target = approved_run(repo, manifest_path, approved_hash, now)
    index = read_json(target / "analysis-index.json")
    if index["approved_manifest_sha256"] != approved_hash:
        raise AcquisitionStopped("Analysis index belongs to another approved run")
    for name, digest in index["artifacts"].items():
        if Path(name).name != name or recorder.sha((target / name).read_bytes()) != digest:
            raise AcquisitionStopped("Frozen analysis/blind pool changed")
    supplied = read_json(ratings_path)
    result = rate_result(read_json(target / "evaluation.json", 6_000_000), read_json(target / ".blind-key.json"), supplied)
    with recorder.writer_lease(repo / "recordings"):
        accounting = copy.deepcopy(manifest)
        accounting["storage_inventory"] = inventory_contract(inventory_path)
        inventory, roots = recorder.load_inventory(accounting, now)
        outputs = {"blind-ratings.json": recorder.encoded(supplied)}
        result.update(measurement="after blind ratings", bytes_by_category={}, run_bytes=0)
        for _ in range(10):
            outputs["blind-result.json"] = recorder.encoded(result)
            counts = run_categories(target, outputs)
            if counts == result["bytes_by_category"]:
                break
            result["bytes_by_category"] = counts
            result["run_bytes"] = sum(counts.values())
        else:
            raise AcquisitionStopped("Cannot reconcile final blind result sizes")
        write_outputs(manifest, target, inventory, roots, outputs)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    prep = commands.add_parser("prepare")
    prep.add_argument("--run-id", required=True)
    prep.add_argument("--expires-at", required=True)
    prep.add_argument("--terms-date", required=True)
    prep.add_argument("--endpoint-reference", required=True)
    prep.add_argument("--metadata", type=Path)
    proposal = commands.add_parser("propose")
    proposal.add_argument("draft", type=Path)
    proposal.add_argument("--inventory", type=Path, required=True)
    proposal.add_argument("--developer-reference", required=True)
    proposal.add_argument("--output", type=Path, required=True)
    for name in ("analyze", "ratings"):
        command = commands.add_parser(name)
        command.add_argument("manifest", type=Path)
        command.add_argument("--approved-sha256", required=True)
        command.add_argument("--inventory", type=Path, required=True)
        if name == "ratings":
            command.add_argument("--ratings", type=Path, required=True)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    now = dt.datetime.now(dt.timezone.utc)
    try:
        if args.command == "prepare":
            metadata = read_json(args.metadata) if args.metadata else bridge(repo, "metadata")
            path = prepare(repo, args.run_id, args.expires_at, args.terms_date, args.endpoint_reference, metadata, now)
            print(f"Draft prepared at {path}. No live authorization; inventory and concrete digest approval required.")
        elif args.command == "propose":
            digest = propose(repo, args.draft, args.inventory, args.developer_reference, args.output, now)
            print(f"Proposed manifest digest: {digest}. Await explicit developer approval before execution. Inventory freshness expires after five minutes.")
        elif args.command == "analyze":
            compare = analyze(repo, args.manifest, args.approved_sha256, args.inventory, now)
            print(f"{LABEL}: shape report written; comparison {'ready for blind review' if compare else 'unavailable; see analysis-index.json'}. No promotion.")
        else:
            ratings(repo, args.manifest, args.approved_sha256, args.ratings, args.inventory, now)
            print(f"{LABEL}: complete blind ratings recorded. No formula promotion.")
    except (AcquisitionStopped, OSError, ValueError, TypeError, KeyError, subprocess.SubprocessError):
        parser.exit(2, "Pilot stopped: approval, explicit inventory, immutable artifact or bounded offline analysis check failed. No automatic rerun.\n")


if __name__ == "__main__":
    main()
