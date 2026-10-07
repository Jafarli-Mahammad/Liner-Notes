"""Offline Phase 4 operator checks. All inventories, recordings and bridge replies are fabricated."""
import copy
import datetime as dt
import json
from pathlib import Path
import struct
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from acquisition_guard import AcquisitionStopped, CATEGORIES
import phase4_pilot as pilot
import record_lastfm as recorder

NOW = dt.datetime(2026, 10, 7, 12, tzinfo=dt.timezone.utc)


def metadata():
    founders = [("Jakuzi", ["Jakuzi"]), ("Son Feci Bisiklet", ["Son Feci Bisiklet"]),
        ("Hotline Miami", ["M.O.O.N.", "Perturbator", "Jasper Byrne"]), ("Dying Light", ["Paweł Błaszczak"]),
        ("ULTRAKILL", ["Heaven Pierce Her"]), ("Hades", ["Darren Korb"])]
    profiles = [dict(id=f"founder-{i+1}", set="Founder", genre=cluster, seeds=sorted(recorder.canonical(s) for s in seeds))
        for i, (cluster, seeds) in enumerate(founders)]
    profiles += [dict(id="development-extra", set="Development", genre="fake", seeds=sorted(recorder.canonical(s) for s in pilot.EXTRA_SEEDS))]
    chunks = []
    def add(text):
        body = text.encode("utf-8"); chunks.extend([struct.pack(">i", len(body)), body])
    add("profile-lock-v1-nfc-invariant")
    for p in sorted(profiles, key=lambda p: p["id"]):
        for value in (p["id"], p["set"], p["genre"], str(len(p["seeds"]))):
            add(value)
        for seed in p["seeds"]:
            add(seed)
    return dict(profileLock=dict(version="profile-lock-v1-nfc-invariant", hash=recorder.sha(b"".join(chunks)),
        review_reference="fabricated test approval", profiles=profiles), protocol=dict(Version="fake-operator-test"))


class OperatorTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="phase4-offline-test-")
        self.root = Path(self.temporary.name)
        self.repo = self.root / "repo"; self.repo.mkdir()
        subprocess.run(["git", "init", "-q", str(self.repo)], check=True)
        (self.repo / ".gitignore").write_text("recordings/\n")
        subprocess.run(["git", "-C", str(self.repo), "add", ".gitignore"], check=True)
        subprocess.run(["git", "-C", str(self.repo), "-c", "user.name=Offline Test", "-c", "user.email=offline@example.invalid", "commit", "-qm", "fixture"], check=True)
        self.draft = pilot.prepare(self.repo, "fake-pilot", "2026-11-06T00:00:00Z", "2026-10-07", "fake-test-endpoints", metadata(), NOW)

    def tearDown(self):
        self.temporary.cleanup()

    def inventory(self, copies=100_000):
        path = self.root / "inventory.json"
        revision, size = recorder.filesystem_revision([str(self.repo / "recordings")])
        categories = dict.fromkeys(CATEGORIES, 0); categories.update(recordings=size, copies=copies)
        path.write_bytes(recorder.encoded(dict(measured_at_utc=NOW.isoformat(), reconciled=True,
            exclusive_acquisition_reference="fake exclusive recorder", categories=categories,
            filesystem_roots=[str(self.repo / "recordings")], filesystem_revision=revision, filesystem_bytes=size)))
        return path

    def manifest(self):
        output = self.root / "proposal.json"
        digest = pilot.propose(self.repo, self.draft, self.inventory(), "fake test approval only", output, NOW)
        return output, digest, json.loads(output.read_bytes())

    def gap_recording(self, manifest):
        target = Path(manifest["output_directory"]); target.mkdir()
        (target / "manifest.json").write_bytes(recorder.encoded(manifest))
        (target / "status.json").write_bytes(recorder.encoded(dict(label=pilot.LABEL, requests=1, response_bytes=0, status="incomplete")))
        (target / "0001-ledger.json").write_bytes(recorder.encoded(dict(sequence=1, method="artist.getSimilar", artist="Jakuzi",
            label=pilot.LABEL, provider="Last.fm", provenance="recorded", status="gap", reason="response_failed_or_incomplete")))
        return target

    def test_draft_is_nonexecutable_and_never_looks_up_credentials(self):
        draft = json.loads(self.draft.read_bytes())
        self.assertEqual("draft", draft["status"])
        self.assertIsNone(draft["approval"]["developer_reference"])
        self.assertIsNone(draft["storage_inventory"]["path"])
        self.assertEqual(143, draft["request_budget"]["max_attempts"])
        self.assertFalse(Path(draft["output_directory"]).exists())
        with self.assertRaises(AcquisitionStopped):
            recorder.check_manifest(draft, "f" * 64, NOW)
        with patch.object(recorder, "live_transport", side_effect=AssertionError("outbound forbidden")):
            recorder.verify_local_locks(draft, self.repo)
        reply = subprocess.CompletedProcess([], 0, stdout=b'PHASE4_JSON={"offline":true}\n', stderr=b'')
        with patch.object(pilot.subprocess, "run", return_value=reply) as run:
            self.assertEqual({"offline": True}, pilot.bridge(self.repo, "metadata"))
            self.assertIn("--no-cache", run.call_args.args[0])

    def test_proposal_requires_complete_fresh_inventory_and_accounted_temporary_copies(self):
        with self.assertRaises(AcquisitionStopped):
            pilot.propose(self.repo, self.draft, self.inventory(copies=0), "fake approval", self.root / "one.json", NOW)
        inv = self.inventory()
        with self.assertRaises(AcquisitionStopped):
            pilot.propose(self.repo, self.draft, inv, "fake approval", self.root / "two.json", NOW + dt.timedelta(minutes=6))
        output, digest, manifest = self.manifest()
        self.assertTrue(output.exists()); self.assertEqual(digest, recorder.manifest_hash(manifest))
        recorder.check_manifest(manifest, digest, NOW)

    def test_output_and_lock_overwrite_dirty_checkout_and_escape_are_rejected(self):
        with self.assertRaises(AcquisitionStopped):
            pilot.prepare(self.repo, "fake-pilot", "2026-11-06T00:00:00Z", "2026-10-07", "fake", metadata())
        with self.assertRaises(AcquisitionStopped):
            pilot.prepare(self.repo, "../escape", "2026-11-06T00:00:00Z", "2026-10-07", "fake", metadata())
        (self.repo / "unrelated.txt").write_text("uncommitted")
        with self.assertRaises(AcquisitionStopped):
            self.manifest()

    def test_shape_only_analysis_writes_bounded_reports_without_formula_or_blind_files(self):
        path, digest, manifest = self.manifest(); target = self.gap_recording(manifest)
        shape = dict(Seeds=[], Gates=[], CanCompare=False, MatchAssessable=False, ListenersKnown=0, ListenersTotal=0,
            NoiseTagEntries=0, RawTagEntries=0, AmbiguityStatus="not verified")
        reply = dict(Label=pilot.LABEL, Protocol=metadata()["protocol"], Shape=shape, Requests=[], Artists=[], Candidates=[],
            Evaluation=None, CorpusComposition="fake", AcquisitionScope="fake")
        inv = self.inventory()
        def fake_bridge(repo, command, input_path):
            request = json.loads(input_path.read_bytes())
            self.assertEqual([], request["Responses"]); self.assertFalse(request["AcquisitionComplete"])
            self.assertEqual(1, len(request["LedgerGaps"])); return reply
        self.assertFalse(pilot.analyze(self.repo, path, digest, inv, NOW, fake_bridge))
        self.assertTrue((target / "shape-report.md").is_file())
        self.assertFalse((target / "evaluation.json").exists()); self.assertFalse((target / "blind-review.json").exists())
        index = json.loads((target / "analysis-index.json").read_bytes())
        self.assertEqual(sum(p.stat().st_size for p in target.iterdir()), index["run_bytes"])
        with self.assertRaises(AcquisitionStopped):
            pilot.analyze(self.repo, path, digest, self.inventory(), NOW, fake_bridge)

    def test_phase5_rejected_before_any_response_content_or_bridge_read(self):
        path, digest, manifest = self.manifest()
        manifest["phase"] = 5; manifest["lastfm_gate"]["explicit_waiver_reference"] = "fake only"
        manifest["approval"]["approved_manifest_sha256"] = recorder.manifest_hash(manifest)
        path.write_bytes(recorder.encoded(manifest))
        with self.assertRaises(AcquisitionStopped), patch.object(pilot, "bridge", side_effect=AssertionError("held-out access")):
            pilot.approved_run(self.repo, path, manifest["approval"]["approved_manifest_sha256"], NOW)

    def test_shape_is_durable_before_comparison_and_comparison_failure_cannot_trigger_rerun(self):
        path, digest, manifest = self.manifest(); target = self.gap_recording(manifest)
        reply = dict(Label=pilot.LABEL, Protocol=metadata()["protocol"],
            Shape=dict(Seeds=[], Gates=[], CanCompare=True, MatchAssessable=False, ListenersKnown=0, ListenersTotal=0,
                NoiseTagEntries=0, RawTagEntries=0, AmbiguityStatus="not verified"), Requests=[], Artists=[], Candidates=[],
            Evaluation=None, CorpusComposition="fake", AcquisitionScope="fake")
        calls = []
        def fake_bridge(repo, command, input_path):
            calls.append(command)
            if command == "shape":
                self.assertFalse((target / "shape-report.json").exists())
                return reply
            self.assertTrue((target / "shape-report.json").is_file())
            raise AcquisitionStopped("fake oversized comparison")
        self.assertFalse(pilot.analyze(self.repo, path, digest, self.inventory(), NOW, fake_bridge))
        self.assertEqual(["shape", "replay"], calls)
        index = json.loads((target / "analysis-index.json").read_bytes())
        self.assertEqual("blocked_bounded_offline_comparison", index["comparison_status"])
        self.assertFalse((target / "evaluation.json").exists())
        with self.assertRaises(AcquisitionStopped):
            pilot.analyze(self.repo, path, digest, self.inventory(), NOW, fake_bridge)

    def test_successful_analysis_freezes_pool_and_final_ratings_reconcile_complete_run_sizes(self):
        path, digest, manifest = self.manifest(); target = self.gap_recording(manifest)
        shape = dict(Seeds=[], Gates=[], CanCompare=True, MatchAssessable=True, ListenersKnown=0, ListenersTotal=0,
            NoiseTagEntries=0, RawTagEntries=0, AmbiguityStatus="not verified")
        reply = dict(Label=pilot.LABEL, Protocol=metadata()["protocol"], Shape=shape, Requests=[], Artists=[], Candidates=[],
            Evaluation=None, CorpusComposition="fake", AcquisitionScope="fake")
        def fake_bridge(repo, command, input_path):
            if command == "shape":
                return reply
            self.assertTrue((target / "shape-report.json").exists())
            return dict(reply, Evaluation=BlindTests.evaluation())
        self.assertTrue(pilot.analyze(self.repo, path, digest, self.inventory(), NOW, fake_bridge))
        supplied = json.loads((target / "blind-ratings-template.json").read_bytes())
        for row in supplied["ratings"]:
            row["rating"] = 0
        ratings_path = self.root / "ratings.json"; ratings_path.write_bytes(recorder.encoded(supplied))
        result = pilot.ratings(self.repo, path, digest, ratings_path, self.inventory(), NOW)
        self.assertTrue(all(row["mean"] == 0 for row in result["results"]))
        self.assertEqual(sum(p.stat().st_size for p in target.iterdir()), result["run_bytes"])
        with self.assertRaises(AcquisitionStopped):
            pilot.ratings(self.repo, path, digest, ratings_path, self.inventory(), NOW)

    def test_response_hash_retrieval_and_provenance_are_verified(self):
        _, _, manifest = self.manifest(); target = self.gap_recording(manifest)
        body = b'{"similarartists":{"artist":[]}}'
        (target / "0001-response.json").write_bytes(body)
        entry = dict(sequence=1, method="artist.getSimilar", artist="Jakuzi", label=pilot.LABEL,
            provider="Last.fm", provenance="recorded", status="recorded", started_at_utc=NOW.isoformat(),
            retrieved_at_utc=(NOW + dt.timedelta(seconds=1)).isoformat(), bytes=len(body), response_sha256=recorder.sha(body))
        (target / "0001-ledger.json").write_bytes(recorder.encoded(entry))
        (target / "status.json").write_bytes(recorder.encoded(dict(label=pilot.LABEL, requests=1, response_bytes=len(body), status="incomplete")))
        request = pilot.replay_input(manifest, target)
        self.assertEqual(entry["retrieved_at_utc"], request["Responses"][0]["RetrievedAtUtc"])
        for field, value in (("response_sha256", "f" * 64), ("retrieved_at_utc", (NOW - dt.timedelta(seconds=1)).isoformat()), ("provenance", "synthetic")):
            changed = dict(entry); changed[field] = value
            (target / "0001-ledger.json").write_bytes(recorder.encoded(changed))
            with self.assertRaises(AcquisitionStopped):
                pilot.replay_input(manifest, target)

    def test_report_budget_and_unknown_inventory_fail_before_creating_output(self):
        _, _, manifest = self.manifest(); target = self.gap_recording(manifest)
        contract = dict(manifest); contract["storage_inventory"] = pilot.inventory_contract(self.inventory())
        inventory, roots = recorder.load_inventory(contract, NOW)
        with self.assertRaises(AcquisitionStopped):
            pilot.write_outputs(manifest, target, inventory, roots, {"large.json": b"x" * 6_000_000})
        self.assertFalse((target / "large.json").exists())
        with self.assertRaises(AcquisitionStopped):
            pilot.write_outputs(manifest, target, inventory, roots, {"../escape.json": b"x"})


class BlindTests(unittest.TestCase):
    @staticmethod
    def evaluation():
        rankings = {f: {f"founder-{i}": [dict(Key=f"key-{f}-{i}", Title=f"song-{f}", Artist="artist")]
            for i in range(1, 7)} for f in "ABCD"}
        return dict(Label=pilot.LABEL, Rankings=rankings)

    def test_blind_union_is_deduplicated_reproducible_and_conceals_formula_information(self):
        evaluation = self.evaluation()
        evaluation["Rankings"]["B"] = copy.deepcopy(evaluation["Rankings"]["A"])
        profiles = {f"founder-{i}": "cluster" for i in range(1, 7)}
        a = pilot.blind_artifacts(evaluation, profiles, "concealed fixed test seed")
        b = pilot.blind_artifacts(evaluation, profiles, "concealed fixed test seed")
        self.assertEqual(a, b); self.assertEqual(18, len(a["blind-review.json"]["items"]))
        for item in a["blind-review.json"]["items"]:
            self.assertEqual({"profile", "cluster", "id", "artist", "title"}, set(item))
        self.assertNotIn("concealed_seed", a["blind-review.json"])

    def test_ratings_require_complete_slots_and_include_unfilled_slots_in_denominator(self):
        evaluation = self.evaluation()
        artifacts = pilot.blind_artifacts(evaluation, {f"founder-{i}": "cluster" for i in range(1, 7)}, "seed")
        key = artifacts[".blind-key.json"]
        rows = [dict(profile=i["profile"], id=i["id"], rating=1 if i["key"].startswith("key-B") else 0) for i in key["mapping"]]
        ratings = dict(label=pilot.LABEL, ratings=rows)
        result = pilot.rate_result(evaluation, key, ratings)
        b = next(r for r in result["results"] if r["formula"] == "B")
        self.assertAlmostEqual(.2, b["mean_difference"]); self.assertEqual(6, b["positive_profiles"])
        self.assertTrue(b["directional_margin_met"])
        for changed in (rows[:-1], rows + [rows[0]], [dict(rows[0], rating=True)] + rows[1:], [dict(rows[0], rating=None)] + rows[1:]):
            with self.assertRaises(AcquisitionStopped):
                pilot.rate_result(evaluation, key, dict(label=pilot.LABEL, ratings=changed))


if __name__ == "__main__":
    unittest.main()
