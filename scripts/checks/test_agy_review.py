import json
import io
import os
from pathlib import Path
import shutil
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

    def test_review_with_tool_activity_is_not_accepted(self):
        events = [{"event": "step_update", "step_update": {"step_type": "tool"}},
                  {"event": "result", "result": {"status": "SUCCESS", "response": "findings"}}]
        with self.assertRaisesRegex(ValueError, "used tools"):
            agy_review.response("\n".join(json.dumps(event) for event in events))

    def test_runner_uses_isolated_directory_and_publishes_response(self):
        output = Path("review.md")
        real_run = subprocess.run

        def fake_run(args, **kwargs):
            if args[0] != "agy":
                return real_run(args, **kwargs)
            work = Path(kwargs["cwd"])
            self.assertNotEqual(work, Path.cwd())
            self.assertNotIn("env", kwargs)
            agent = (work / ".agents/agents/liner-push-review.md").read_text()
            self.assertIn("tools: []", agent)
            self.assertIn("inheritMcp: false", agent)
            self.assertIn("inheritCustomizations: false", agent)
            self.assertIn("liner-push-review", args)
            self.assertIn(agy_review.MODEL, args)
            self.assertIn("high", args)
            request = json.loads(kwargs["input"])
            self.assertEqual(request["event"], "user")
            self.assertIn(self.after, request["message"]["content"])
            return subprocess.CompletedProcess(args, 0, json.dumps({"event": "result", "result": {
                "status": "SUCCESS", "response": "Advisory findings"}}))

        with patch.dict(os.environ, {}, clear=True), \
             patch.object(sys, "argv", ["agy_review.py", self.before, self.after, str(output)]), \
             patch.object(subprocess, "run", side_effect=fake_run):
            self.assertEqual(agy_review.main(), 0)
        self.assertIn("Advisory findings", output.read_text())

    def test_push_reviews_exact_refs_and_skips_deletions(self):
        refs = (f"refs/heads/feature {self.after} refs/heads/other {self.before}\n"
                f"(delete) {'0' * 40} refs/heads/deleted {self.before}\n"
                f"refs/heads/new {self.after} refs/heads/new {'0' * 40}\n")

        def fake_review(before, after, output):
            output.write_text("Local findings")
            return 0

        with patch.object(sys, "argv", ["agy_review.py", "--push"]), \
             patch.object(sys, "stdin", io.StringIO(refs)), \
             patch.object(sys, "stdout", io.StringIO()), \
             patch.object(agy_review, "review", side_effect=fake_review) as reviewer:
            self.assertEqual(agy_review.main(), 0)
        self.assertEqual(reviewer.call_count, 2)
        self.assertEqual(reviewer.call_args_list[0].args[:2], (self.before, self.after))
        self.assertEqual(reviewer.call_args_list[1].args[:2], ("0" * 40, self.after))
        self.assertEqual(len(list(Path(".linernotes/agy-reviews").glob("*.md"))), 2)

    def test_push_propagates_review_failure(self):
        def failure(before, after, output):
            output.write_text("Login required")
            return 1

        with patch.object(sys, "argv", ["agy_review.py", "--push"]), \
             patch.object(sys, "stdin", io.StringIO(
                 f"refs/heads/main {self.after} refs/heads/main {self.before}\n")), \
             patch.object(sys, "stdout", io.StringIO()), \
             patch.object(agy_review, "review", side_effect=failure):
            self.assertEqual(agy_review.main(), 1)

    def test_actual_hook_passes_push_refs_to_local_cli(self):
        checks = Path("scripts/checks")
        checks.mkdir(parents=True)
        shutil.copy(self.old_cwd / "scripts/checks/agy_review.py", checks)
        binaries = Path("fake-bin").resolve()
        binaries.mkdir()
        stubs = {
            "dotnet": "#!/bin/sh\nexit 0\n",
            "python3": (f"#!/bin/sh\ncase \"$1\" in\n"
                        f"*/agy_review.py) exec '{sys.executable}' \"$@\" ;;\n"
                        "*) exit 0 ;;\nesac\n"),
            "agy": (f"#!{sys.executable}\nimport json, sys\n"
                    "request = json.load(sys.stdin)\n"
                    "assert '--model' in sys.argv and 'gemini-3.8-flash-high' in sys.argv\n"
                    "assert '--effort' in sys.argv and 'high' in sys.argv\n"
                    f"assert '{self.after}' in request['message']['content']\n"
                    "print(json.dumps({'event': 'result', 'result': "
                    "{'status': 'SUCCESS', 'response': 'Local hook findings'}}))\n"),
        }
        for name, content in stubs.items():
            executable = binaries / name
            executable.write_text(content)
            executable.chmod(0o755)
        env = {**os.environ, "PATH": f"{binaries}:{os.environ['PATH']}"}
        for name in ("SKIP_PUSH_GATE", "SKIP_GATE", "SKIP_AGY_REVIEW"):
            env.pop(name, None)
        result = subprocess.run(["bash", str(self.old_cwd / ".githooks/pre-push")],
                                input=f"refs/heads/main {self.after} refs/heads/main {self.before}\n",
                                env=env, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("Local hook findings", result.stdout)
        self.assertEqual(len(list(Path(".linernotes/agy-reviews").glob("*.md"))), 1)


if __name__ == "__main__":
    unittest.main()
