import re
import unittest
from pathlib import Path

from phase9_compose import EXPECTED_EXPORT_VERSION


class ComposeContractTests(unittest.TestCase):
    def test_export_version_matches_application_contract(self):
        root = Path(__file__).resolve().parents[2]
        handler = root / 'src/Application/Features/Export/Queries/GetUserDataExport/GetUserDataExportQueryHandler.cs'
        version = re.search(r'ExportVersion: "([^"\n]+)"', handler.read_text())
        self.assertIsNotNone(version, 'Export version declaration not found; review the Compose contract check')
        self.assertEqual(version.group(1), EXPECTED_EXPORT_VERSION,
                         'Update the Compose lifecycle expectation when the export contract changes')
