"""Fake-input Phase 2 verification. Never instantiates the live transport."""
import copy
import datetime as dt
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from acquisition_guard import AcquisitionGuard, AcquisitionStopped, CATEGORIES, Inventory
import record_lastfm as recorder

NOW = dt.datetime(2026, 10, 7, 0, 0, tzinfo=dt.timezone.utc)


def inventory(used=0, revision="revision", reconciled=True):
    return Inventory({key: used if key == "recordings" else 0 for key in CATEGORIES}, revision, reconciled)


def manifest():
    founders = [("Jakuzi", "Jakuzi"), ("Son Feci Bisiklet", "Son Feci Bisiklet"),
        ("M.O.O.N.", "Hotline Miami"), ("Perturbator", "Hotline Miami"), ("Jasper Byrne", "Hotline Miami"),
        ("Paweł Błaszczak", "Dying Light"), ("Heaven Pierce Her", "ULTRAKILL"), ("Darren Korb", "Hades")]
    m = dict(manifest_version=1, run_id="fake-run", phase=4, status="approved", label="pilot, directional",
        approval=dict(developer_reference="fake-test-approval", approved_manifest_sha256=None),
        lastfm_gate=dict(reply_reference=None, explicit_waiver_reference=None),
        verification=dict(terms_date="2026-10-07", endpoint_contract_reference="fake-test-contract"),
        profile_lock_sha256="a" * 64, protocol_sha256="b" * 64, code_revision="c" * 40,
        provider="Last.fm", credential_source="LASTFM_API_KEY",
        seeds=[dict(set="founder", cluster=cluster, kind="artist", value=name) for name, cluster in founders] +
            [dict(set="development", cluster="extra", kind="artist", value=name) for name in ("Agalloch", "Pharoah Sanders", "Altın Gün")],
        endpoints=[dict(method="artist.getSimilar", per_seed_limit=5, max_calls=11),
            dict(method="artist.getTopTracks", per_artist_limit=5, max_calls=66),
            dict(method="artist.getTopTags", per_artist_limit=15, max_calls=66)],
        expansion=dict(depth=1, max_distinct_artists_including_seeds=66, order="canonical_ordinal"),
        request_budget=dict(max_attempts=160, max_concurrency=1, automatic_retries=0),
        pacing=dict(minimum_start_interval_ms=1000, burst=False, respect_stricter_server_limits=True),
        stop_on=sorted(recorder.STOP_ON),
        response_budget=dict(estimated_uncompressed_bytes=4_000_000, hard_cap_bytes=5_000_000, max_single_response_bytes=100_000),
        storage_budget=dict(run_directory_max_bytes=6_000_000, shared_stop_bytes=80_000_000),
        storage_inventory=dict(path="/unused/fake-inventory.json", sha256="d" * 64, measured_at_utc=NOW.isoformat()),
        output_directory="/unused/recordings/fake-run", outputs=sorted(recorder.OUTPUTS),
        output_rules=dict(require_gitignored=True, reject_tracked=True, reject_overwrite=True, reject_path_escape=True),
        retention=dict(expires_at_utc="2026-11-01T00:00:00Z", extension_requires_approval=True))
    approve(m)
    return m


def approve(m):
    digest = recorder.manifest_hash(m)
    m["approval"]["approved_manifest_sha256"] = digest
    return digest


class GuardTests(unittest.TestCase):
    def test_all_categories_and_reservation_accounting(self):
        inv = Inventory({key: 100 for key in CATEGORIES}, "r", True)
        guard = AcquisitionGuard(inv, 1000, 100)
        with guard.reserve(100, 20, "r") as reservation:
            reservation.commit(50, 10, "new")
        self.assertEqual(guard.used, 760)
        self.assertEqual(guard.run_used, 60)
        self.assertEqual(guard.inventory.categories["recordings"], 160)
        with self.assertRaises(AcquisitionStopped):
            guard.reserve(1, 0, "r")

    def test_exact_shared_boundary_stops_before_request(self):
        guard = AcquisitionGuard(inventory(79_999_800), 1000, 100)
        with self.assertRaises(AcquisitionStopped):
            guard.reserve(100, 100, "revision")
        with guard.reserve(100, 99, "revision"):
            pass

    def test_run_cap_additional_to_shared_cap(self):
        guard = AcquisitionGuard(inventory(), 100, 100)
        with guard.reserve(90, 10, "revision") as r:
            r.commit(90, 10, "next")
        with self.assertRaises(AcquisitionStopped):
            guard.reserve(1, 0, "next")

    def test_unknown_stale_overflow_negative_boolean(self):
        bad = [Inventory({}, "r", True), inventory(reconciled=False), inventory(revision=""),
               inventory(2**63), inventory(-1), inventory(True)]
        for inv in bad:
            with self.subTest(inv=inv), self.assertRaises(AcquisitionStopped):
                AcquisitionGuard(inv, 1000, 100)

    def test_concurrent_cancelled_and_oversized_attempts(self):
        guard = AcquisitionGuard(inventory(), 1000, 100)
        with guard.reserve(100, 10, "revision") as reservation:
            with self.assertRaises(AcquisitionStopped):
                guard.reserve(1, 0, "revision")
            with self.assertRaises(AcquisitionStopped):
                reservation.commit(101, 0, "next")
        self.assertEqual(guard.used, 0)
        with self.assertRaises(AcquisitionStopped):
            guard.reserve(1, 0, "revision", cancelled=True)
        with guard.reserve(1, 1, "revision") as reservation:
            with self.assertRaises(AcquisitionStopped):
                reservation.commit(0, 2, "next")
        with guard.reserve(1, 1, "revision") as reservation:
            with self.assertRaises(AcquisitionStopped):
                reservation.commit(1, 1, "next", cancelled=True)


class ManifestTests(unittest.TestCase):
    def test_approved_manifest_and_mutation_gate(self):
        m = manifest()
        recorder.check_manifest(m, m["approval"]["approved_manifest_sha256"], NOW)
        m["seeds"][0]["value"] = "changed"
        with self.assertRaises(AcquisitionStopped):
            recorder.check_manifest(m, m["approval"]["approved_manifest_sha256"], NOW)

    def test_invalid_contracts_even_when_rehashed(self):
        mutations = [lambda m: m.update(status="draft"), lambda m: m.update(phase=2),
            lambda m: m["approval"].update(developer_reference=None),
            lambda m: m["request_budget"].update(automatic_retries=1),
            lambda m: m["request_budget"].update(max_concurrency=2),
            lambda m: m["pacing"].update(minimum_start_interval_ms=999),
            lambda m: m["response_budget"].update(hard_cap_bytes=5_000_001),
            lambda m: m["storage_budget"].update(shared_stop_bytes=100_000_000),
            lambda m: m["retention"].update(expires_at_utc="2026-10-01T00:00:00Z"),
            lambda m: m["verification"].update(terms_date="2026-09-01"),
            lambda m: m["seeds"].pop(), lambda m: m["endpoints"].pop(),
            lambda m: m["output_rules"].update(reject_tracked=False),
            lambda m: m.update(profile_lock_sha256="placeholder"),
            lambda m: m.update(credential_source="api_key=secret"),
            lambda m: m["stop_on"].pop(), lambda m: m["expansion"].update(depth=2)]
        for mutate in mutations:
            m = manifest()
            mutate(m)
            digest = approve(m)
            with self.subTest(mutate=mutate), self.assertRaises(AcquisitionStopped):
                recorder.check_manifest(m, digest, NOW)

    def test_phase_five_requires_separate_gate(self):
        m = manifest()
        m.update(phase=5, label="real evaluation")
        digest = approve(m)
        with self.assertRaises(AcquisitionStopped):
            recorder.check_manifest(m, digest, NOW)
        m["lastfm_gate"]["explicit_waiver_reference"] = "fake-test-waiver"
        recorder.check_manifest(m, approve(m), NOW)

    def test_secret_fields_are_rejected_before_retaining_manifest(self):
        for value in ({"api_key": "fake-secret"}, {"note": "https://fake.invalid/?api_key=fake-secret"}):
            m = manifest()
            m["unexpected"] = value
            with self.assertRaises(AcquisitionStopped):
                recorder.check_manifest(m, approve(m), NOW)

    def test_inventory_hash_freshness_and_external_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "data"
            root.mkdir()
            (root / "cache").write_bytes(b"fake")
            revision, size = recorder.filesystem_revision([str(root)])
            path = Path(temp) / "inventory.json"
            inv = dict(measured_at_utc=NOW.isoformat(), reconciled=True,
                exclusive_acquisition_reference="fake-exclusive-test", filesystem_roots=[str(root)],
                filesystem_revision=revision, filesystem_bytes=size,
                categories={key: size if key == "http_cache" else 0 for key in CATEGORIES})
            def save():
                path.write_bytes(recorder.encoded(inv))
                m = manifest()
                m["storage_inventory"] = dict(path=str(path), sha256=recorder.sha(path.read_bytes()), measured_at_utc=inv["measured_at_utc"])
                return m
            m = save()
            snapshot, paths = recorder.load_inventory(m, NOW)
            self.assertEqual(size, snapshot.total())
            with self.assertRaises(AcquisitionStopped):
                recorder.load_inventory(m, NOW + dt.timedelta(minutes=6))
            with self.assertRaises(AcquisitionStopped):
                recorder.load_inventory(m, NOW - dt.timedelta(minutes=1))
            (root / "external").write_bytes(b"change")
            with self.assertRaises(AcquisitionStopped):
                recorder.load_inventory(m, NOW)
            inv["categories"] = {}
            with self.assertRaises(AcquisitionStopped):
                recorder.load_inventory(save(), NOW)

    def test_actual_local_profile_and_protocol_hashes_and_seed_partition(self):
        import struct
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp)
            subprocess.run(["git", "init", "--quiet", temp], check=True)
            (repo / ".gitignore").write_text("recordings/\n")
            root = repo / "recordings"
            root.mkdir()
            m = manifest()
            profiles = [dict(id="fake-" + str(i), set="Founder" if s["set"] == "founder" else "Development",
                genre=s["cluster"], seeds=[recorder.canonical(s["value"])]) for i, s in enumerate(m["seeds"])]
            profiles.sort(key=lambda p: p["id"])
            version = "profile-lock-v1-nfc-invariant"
            values = [version]
            for p in profiles:
                values.extend([p["id"], p["set"], p["genre"], str(len(p["seeds"])), *p["seeds"]])
            digest = recorder.sha(b"".join(struct.pack(">i", len(v.encode())) + v.encode() for v in values))
            path = root / "profile-lock.json"
            path.write_bytes(recorder.encoded(dict(version=version, hash=digest, review_reference="fake-test-only", profiles=profiles)))
            protocol = root / "protocol.json"
            protocol.write_bytes(b'{"label":"fake-test-only"}')
            m.update(profile_lock_path=str(path), profile_lock_sha256=digest,
                     protocol_path=str(protocol), protocol_sha256=recorder.sha(protocol.read_bytes()))
            recorder.verify_local_locks(m, repo)
            m["seeds"][0]["set"] = "held-out"
            with self.assertRaises(AcquisitionStopped):
                recorder.verify_local_locks(m, repo)
            m["seeds"][0]["set"] = "founder"
            protocol.write_bytes(b"changed")
            with self.assertRaises(AcquisitionStopped):
                recorder.verify_local_locks(m, repo)

    def test_output_path_ignore_tracked_overwrite_and_symlink(self):
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp)
            subprocess.run(["git", "init", "--quiet", temp], check=True)
            (repo / ".gitignore").write_text("recordings/\n")
            target = repo / "recordings" / "run"
            self.assertEqual(target, recorder.contained_output(repo, str(target), "run"))
            for invalid in ("relative", str(repo / "outside"), str(target / ".." / "escape")):
                with self.assertRaises(AcquisitionStopped):
                    recorder.contained_output(repo, invalid, "run")
            target.mkdir(parents=True)
            with self.assertRaises(AcquisitionStopped):
                recorder.contained_output(repo, str(target), "run")
            with self.assertRaises(AcquisitionStopped):
                recorder.contained_output(repo, str(repo / "unignored" / "run"), "run")
            tracked = repo / "recordings" / "tracked"
            tracked.mkdir()
            (tracked / "data").write_text("fake")
            subprocess.run(["git", "-C", temp, "add", "-f", "recordings/tracked/data"], check=True)
            with self.assertRaises(AcquisitionStopped):
                recorder.contained_output(repo, str(tracked), "tracked")
            with tempfile.TemporaryDirectory() as elsewhere:
                (repo / "recordings" / "link").symlink_to(elsewhere, target_is_directory=True)
                with self.assertRaises(AcquisitionStopped):
                    recorder.contained_output(repo, str(repo / "recordings" / "link" / "new"), "link/new")


class FakeResponse(io.BytesIO):
    def __init__(self, body, headers=None):
        super().__init__(body)
        self.headers = headers or {}


class RecorderTests(unittest.TestCase):
    def test_bounded_response_exact_oversize_compression_cancel(self):
        self.assertEqual(b"123", recorder.read_bounded(FakeResponse(b"123"), 3, lambda: False))
        for response, cancelled in [(FakeResponse(b"1234"), lambda: False),
            (FakeResponse(b"123", {"Content-Encoding": "gzip"}), lambda: False),
            (FakeResponse(b"123"), lambda: True)]:
            with self.assertRaises(AcquisitionStopped):
                recorder.read_bounded(response, 3, cancelled)

    def test_single_os_writer(self):
        with tempfile.TemporaryDirectory() as temp:
            with recorder.writer_lease(Path(temp)):
                with self.assertRaises(AcquisitionStopped):
                    with recorder.writer_lease(Path(temp)):
                        pass

    def run_fake(self, response_factory, cancelled=lambda: False):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        repo = Path(self.temp.name)
        root = repo / "recordings"
        root.mkdir()
        m = manifest()
        m["output_directory"] = str(root / "fake-run")
        calls, sleeps = [], []
        def transport(method, artist, limit, credential):
            calls.append((method, artist, limit))
            self.assertEqual("fake-secret", credential)
            return response_factory(method, artist)
        revision, size = recorder.filesystem_revision([str(root)])
        recorder.record(m, repo, inventory(size, revision), [str(root)], transport,
                        "fake-secret", cancelled, sleeps.append, lambda: 0)
        return Path(m["output_directory"]), calls, sleeps

    def test_fake_acquisition_depth_one_pacing_provenance_no_scoring(self):
        def response(method, artist):
            return FakeResponse(recorder.encoded({"similarartists": {"artist": [{"name": "fake neighbour", "match": "0.5"}]}})
                if method == "artist.getSimilar" else b"{}", {"Cache-Control": "max-age=60"})
        output, calls, sleeps = self.run_fake(response)
        self.assertEqual(35, len(calls))  # 11 similarity plus 2*12 distinct artists
        self.assertEqual(11, sum(c[0] == "artist.getSimilar" for c in calls))
        self.assertTrue(all(value == 1 for value in sleeps))
        state = json.loads((output / "status.json").read_bytes())
        self.assertEqual("acquisition_complete", state["status"])
        self.assertTrue(state["no_scoring_performed"])
        for path in output.glob("*-ledger.json"):
            ledger = json.loads(path.read_bytes())
            self.assertEqual("recorded", ledger["provenance"])
            self.assertEqual("pilot, directional", ledger["label"])
            self.assertGreaterEqual(recorder.stamp(ledger["retrieved_at_utc"]), recorder.stamp(ledger["started_at_utc"]))
            raw = path.with_name(path.name.replace("ledger", "response")).read_bytes()
            self.assertEqual(recorder.sha(raw), ledger["response_sha256"])
        self.assertFalse(any(b"fake-secret" in p.read_bytes() for p in output.iterdir()))

    def test_provider_error_and_secret_bearing_transport_fail_once(self):
        count = []
        def response(method, artist):
            count.append(method)
            return FakeResponse(b'{"error":29,"message":"fake-secret"}')
        with self.assertRaises(AcquisitionStopped):
            self.run_fake(response)
        self.assertEqual(1, len(count))
        output = Path(self.temp.name) / "recordings" / "fake-run"
        self.assertEqual("gap", json.loads((output / "0001-ledger.json").read_bytes())["status"])
        self.assertFalse((output / "0001-response.json").exists())
        self.assertFalse(any(b"fake-secret" in p.read_bytes() for p in output.iterdir()))
        self.assertEqual("incomplete", json.loads((output / "status.json").read_bytes())["status"])

    def test_credential_echo_and_stricter_server_pacing_fail_without_response_file(self):
        for body, headers in [(b'{"echo":"fake-secret"}', {}), (b"{}", {"Retry-After": "120"})]:
            count = []
            def response(method, artist):
                count.append(method)
                return FakeResponse(body, headers)
            with self.subTest(body=body), self.assertRaises(AcquisitionStopped):
                self.run_fake(response)
            self.assertEqual(1, len(count))
            output = Path(self.temp.name) / "recordings" / "fake-run"
            self.assertFalse((output / "0001-response.json").exists())
            self.assertFalse(any(b"fake-secret" in p.read_bytes() for p in output.iterdir()))

    def test_cancellation_makes_zero_requests(self):
        count = []
        def response(method, artist):
            count.append(method)
            return FakeResponse(b"{}")
        with self.assertRaises(AcquisitionStopped):
            self.run_fake(response, cancelled=lambda: True)
        self.assertEqual([], count)

    def test_inventory_external_mutation_stops(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            file = root / "fake"
            file.write_text("a")
            first = recorder.filesystem_revision([temp])[0]
            file.write_text("changed")
            self.assertNotEqual(first, recorder.filesystem_revision([temp])[0])
            with self.assertRaises(AcquisitionStopped):
                recorder.filesystem_revision([temp, str(file)])

    def test_external_change_during_own_write_is_not_accepted_as_reconciliation(self):
        with tempfile.TemporaryDirectory() as temp:
            old = Path(temp) / "old"
            new = Path(temp) / "own-write"
            old.write_bytes(b"old")
            before = recorder.filesystem_snapshot([temp])
            new.write_bytes(b"new")
            recorder.reconcile_local_write([temp], before, [new])
            old.write_bytes(b"external edit")
            with self.assertRaises(AcquisitionStopped):
                recorder.reconcile_local_write([temp], before, [new])

    def test_draft_cli_never_looks_up_credentials_or_transport(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "draft.json"
            m = manifest()
            m["status"] = "draft"
            path.write_bytes(recorder.encoded(m))
            with patch("sys.argv", ["record_lastfm.py", str(path), "--approved-sha256", "a" * 64]), \
                 patch.object(recorder, "live_transport", side_effect=AssertionError("must not call")), \
                 patch.object(recorder.os, "environ", {}), self.assertRaises(SystemExit) as exit:
                recorder.main()
            self.assertEqual(2, exit.exception.code)


if __name__ == "__main__":
    unittest.main()
