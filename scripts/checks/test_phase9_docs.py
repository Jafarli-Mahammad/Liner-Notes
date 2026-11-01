import unittest
from pathlib import Path

from renewal_docs import validate_documents


class DocumentationGateTests(unittest.TestCase):
    def setUp(self):
        root = Path(__file__).resolve().parents[2]
        self.progress = (root / 'PROGRESS.md').read_text()
        self.readme = (root / 'README.md').read_text()

    def test_current_documents(self):
        validate_documents(self.progress, self.readme)

    def test_equivalent_qualifications(self):
        validate_documents(self.progress.replace('not authorized', 'not approved'),
                           self.readme.replace('unverified', 'not verified'))

    def test_missing_approval_gate_is_rejected(self):
        with self.assertRaisesRegex(AssertionError, 'approval gates'):
            validate_documents(self.progress.replace('not authorized', 'authorized').replace('not approved', 'approved'), self.readme)

    def test_unqualified_readme_is_rejected(self):
        with self.assertRaisesRegex(AssertionError, 'verified implementation'):
            validate_documents(self.progress, self.readme.replace('unverified', 'verified').replace('not verified', 'verified'))

    def test_popularity_bias_is_rejected(self):
        with self.assertRaisesRegex(AssertionError, 'popularity-neutral'):
            validate_documents(self.progress, self.readme + '\nAnti-Popularity Bias')
