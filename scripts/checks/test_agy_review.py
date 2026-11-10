import json
import io
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import agy_review


class PushReviewTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.old_cwd = Path.cwd()
        os.chdir(self.directory.name)
        self.addCleanup(os.chdir, self.old_cwd)
        self.git("init", "-q")
        self.git("config", "user.name", "Review test")
        self.git("config", "user.email", "review@example.invalid")
        Path("old.txt").write_text("old\n")
        self.before = self.commit()
        Path("old.txt").unlink()
        Path("changed file.txt").write_text("new\n")
        Path("binary.dat").write_bytes(b"\x00\xff")
        self.after = self.commit()

    def git(self, *args):
        return subprocess.check_output(["git", *args], stderr=subprocess.DEVNULL).decode().strip()

    def commit(self):
        self.git("add", ".")
        self.git("commit", "-qm", "fixture")
        return self.git("rev-parse", "HEAD")

    def data(self, before, after):
        prompt = agy_review.packet(before, after)
        return json.loads(prompt[len(agy_review.INSTRUCTIONS) + 1:])

    def test_exact_push_range_ignores_dirty_files(self):
        Path("changed file.txt").write_text("uncommitted secret")
        data = self.data(self.before, self.after)
        files = {item["path"]: item for item in data["files"]}
        self.assertEqual(files["changed file.txt"]["content"], "new\n")
        self.assertEqual(files["old.txt"]["status"], "deleted")
        self.assertIn("binary", files["binary.dat"]["status"])
        self.assertNotIn("uncommitted secret", json.dumps(data))

    def test_new_branch_compares_whole_tree(self):
        data = self.data("0" * 40, self.before)
        self.assertEqual(data["files"], [{"path": "old.txt", "content": "old\n"}])

    def test_non_ancestor_force_push(self):
        self.git("checkout", "--orphan", "replacement")
        self.git("rm", "-rf", ".")
        Path("replacement.txt").write_text("replacement")
        after = self.commit()
        data = self.data(self.after, after)
        self.assertIn("replacement.txt", data["diff"])
        self.assertIn("changed file.txt", data["diff"])

    def test_input_limit_fails_without_partial_review(self):
        with patch.object(agy_review, "MAX_PACKET_BYTES", 10):
            with self.assertRaisesRegex(ValueError, "No partial review"):
                agy_review.packet(self.before, self.after)

    def test_revision_injection_rejected(self):
        with self.assertRaisesRegex(ValueError, "full Git SHA"):
            agy_review.packet("--output=unexpected", self.after)

    def test_terminal_status_and_nonempty_response_required(self):
        self.assertEqual(agy_review.response(json.dumps({"event": "result", "result": {
            "status": "SUCCESS", "response": "Review findings"}})), "Review findings")
        for result in ({"status": "ERROR", "response": "partial"},
                       {"status": "WAITING"}, {"status": "SUCCESS", "response": ""}):
            with self.assertRaises(ValueError):
                agy_review.response(json.dumps({"event": "result", "result": result}))

    def test_runner_uses_isolated_directory_and_publishes_response(self):
        output = Path("review.md")
        real_run = subprocess.run

        def fake_run(args, **kwargs):
            if args[0] != "agy":
                return real_run(args, **kwargs)
            work = Path(kwargs["cwd"])
            self.assertNotEqual(work, Path.cwd())
            self.assertNotIn("env", kwargs)
            self.assertIn(agy_review.MODEL, args)
            self.assertIn("high", args)
            request = json.loads(kwargs["input"])
            self.assertEqual(request["event"], "user")
            self.assertIn(self.after, request["message"]["content"])
            return subprocess.CompletedProcess(args, 0, json.dumps({"event": "result", "result": {
                "status": "SUCCESS", "response": "Advisory findings"}}))

        with patch.dict(os.environ, {"GEMINI_API_KEY": "test-placeholder"}), \
             patch.object(sys, "argv", ["agy_review.py", self.before, self.after, str(output)]), \
             patch.object(subprocess, "run", side_effect=fake_run):
            self.assertEqual(agy_review.main(), 0)
        self.assertIn("Advisory findings", output.read_text())

    def test_missing_secret_leaves_failure_report(self):
        with patch.dict(os.environ, {}, clear=True), \
             patch.object(sys, "argv", ["agy_review.py", self.before, self.after, "review.md"]), \
             patch.object(sys, "stderr", io.StringIO()):
            self.assertEqual(agy_review.main(), 1)
        self.assertIn("GEMINI_API_KEY", Path("review.md").read_text())


if __name__ == "__main__":
    unittest.main()
