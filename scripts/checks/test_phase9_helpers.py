import contextlib
import importlib.util
import io
from pathlib import Path
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("phase9_results", ROOT / "scripts/checks/phase9_test_results.py")
results = importlib.util.module_from_spec(spec)
spec.loader.exec_module(results)
NAMESPACE = results.NS["t"]


class TestResultsGate(unittest.TestCase):
    def write_results(self, directory, name, *, outcome="Passed", passed=1, total=1):
        tag = lambda value: "{" + NAMESPACE + "}" + value
        root = ET.Element(tag("TestRun"))
        summary = ET.SubElement(root, tag("ResultSummary"))
        ET.SubElement(summary, tag("Counters"), total=str(total), passed=str(passed))
        records = ET.SubElement(root, tag("Results"))
        ET.SubElement(records, tag("UnitTestResult"), outcome=outcome)
        definitions = ET.SubElement(root, tag("TestDefinitions"))
        unit = ET.SubElement(definitions, tag("UnitTest"))
        ET.SubElement(unit, tag("TestMethod"), codeBase=name + ".dll")
        ET.ElementTree(root).write(Path(directory) / (name + ".trx"))

    def test_requires_complete_passing_suite(self):
        with tempfile.TemporaryDirectory() as directory:
            for name in results.PROJECTS:
                self.write_results(directory, name)
            self.assertEqual(5, results.validate(directory))

    def test_rejects_empty_or_missing_project(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                results.validate(directory)
            self.write_results(directory, "LinerNotes.Domain.Tests")
            with self.assertRaises(ValueError):
                results.validate(directory)

    def test_rejects_skipped_failed_and_forged_counts(self):
        for outcome, passed, total in (("NotExecuted", 0, 1), ("Failed", 0, 1), ("Passed", 2, 2)):
            with self.subTest(outcome=outcome, total=total), tempfile.TemporaryDirectory() as directory:
                for name in results.PROJECTS:
                    self.write_results(directory, name)
                self.write_results(directory, "LinerNotes.Domain.Tests", outcome=outcome, passed=passed, total=total)
                with self.assertRaises(ValueError):
                    results.validate(directory)


class PrivateEnvironment(unittest.TestCase):
    def test_creation_is_private_and_overwrite_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / ".env.fixture"
            command = ["python3", str(ROOT / "scripts/phase9_init_local.py"), "--output", str(path)]
            first = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(0, first.returncode)
            content = path.read_bytes()
            self.assertEqual(0o600, path.stat().st_mode & 0o777)
            for value in content.decode().splitlines():
                self.assertNotIn(value.split("=", 1)[1], first.stdout)
            second = subprocess.run(command, capture_output=True, text=True)
            self.assertNotEqual(0, second.returncode)
            self.assertEqual(content, path.read_bytes())


if __name__ == "__main__":
    unittest.main()
