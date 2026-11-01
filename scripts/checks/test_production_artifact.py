import tempfile
import unittest
from pathlib import Path

from production_artifact import validate


class ProductionArtifactTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        (self.root / 'home-assets/licenses').mkdir(parents=True)
        (self.root / 'index.html').write_text('<script type="module" src="/home-assets/app.js"></script><link rel="stylesheet" href="/home-assets/app.css">')
        for name in ['app.js', 'app.css', 'licenses/Inter-OFL.txt', 'licenses/DM-Sans-OFL.txt', 'licenses/ThirdParty.txt']:
            (self.root / 'home-assets' / name).write_text('artifact fixture')

    def test_complete_output(self):
        self.assertEqual(2, len(validate(self.root)))

    def test_missing_or_empty_assets_and_licenses_fail(self):
        for name in ['app.js', 'app.css', 'licenses/Inter-OFL.txt', 'licenses/DM-Sans-OFL.txt', 'licenses/ThirdParty.txt']:
            with self.subTest(name=name):
                path = self.root / 'home-assets' / name
                path.write_text('')
                with self.assertRaises(ValueError):
                    validate(self.root)
                path.write_text('artifact fixture')

    def test_external_or_traversing_references_fail(self):
        for reference in ['https://example.test/app.js', '/../outside.js', '//example.test/app.js', '/home-assets/%2e%2e/outside.js']:
            with self.subTest(reference=reference):
                (self.root / 'index.html').write_text(f'<script src="{reference}"></script>')
                with self.assertRaises(ValueError):
                    validate(self.root)

    def test_html_instead_of_javascript_fails(self):
        (self.root / 'home-assets/app.js').write_text('<!doctype html><html>fallback</html>')
        with self.assertRaises(ValueError):
            validate(self.root)


if __name__ == '__main__':
    unittest.main()
