"""Boundary checks for the synthetic, bounded catalog resource probe."""
import io
import json
import secrets
from pathlib import Path
import tempfile
import unittest
import uuid

import catalog_probe as probe


class CatalogProbeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.artist = str(uuid.uuid5(uuid.NAMESPACE_URL, 'synthetic-artist'))
        self.album = str(uuid.uuid5(uuid.NAMESPACE_URL, 'synthetic-album'))

    def record(self, **changes):
        record = dict(id=self.artist, kind='artist', name='İşıq Ətrafı',
                      aliases=['ISIQ ETRAFI', 'İşıq Ətrafı'], artist_ids=[],
                      tags=[dict(name='electronic', weight=1.0)])
        record.update(changes)
        return record

    def artifact(self, records, header=None):
        path = self.root / 'source.jsonl'
        lines = [probe.HEADER if header is None else header, *records]
        path.write_text(''.join(json.dumps(x, ensure_ascii=False) + '\n' for x in lines))
        return path

    def test_generated_artifacts_are_byte_reproducible_and_relationships_valid(self):
        a, b = self.root / 'a.jsonl', self.root / 'b.jsonl'
        probe.generate(a, artists=8, albums_per_artist=2)
        probe.generate(b, artists=8, albums_per_artist=2)
        self.assertEqual(a.read_bytes(), b.read_bytes())
        report = probe.validate(a, self.root / 'normalized.jsonl')
        self.assertEqual((8, 16, 24), (report['artists'], report['albums'], report['entities']))
        self.assertEqual(64, len(report['normalized_sha256']))
        self.assertEqual('synthetic', report['origin'])

    def test_forward_references_and_exact_duplicates_are_supported(self):
        artist = self.record()
        album = self.record(id=self.album, kind='album', name='A synthetic album',
                            aliases=[], artist_ids=[self.artist])
        report = probe.validate(self.artifact([album, artist, artist]), self.root / 'out.jsonl')
        self.assertEqual(2, report['entities'])
        self.assertEqual(1, report['duplicate_records'])
        self.assertEqual(3, len((self.root / 'out.jsonl').read_text().splitlines()))

    def test_conflicting_ids_fail_without_publishing_partial_output(self):
        source = self.artifact([self.record(), self.record(name='Different artist')])
        destination = self.root / 'out.jsonl'
        with self.assertRaisesRegex(probe.ProbeError, 'conflicting'):
            probe.validate(source, destination)
        self.assertFalse(destination.exists())
        self.assertEqual(['source.jsonl'], sorted(x.name for x in self.root.iterdir()))

    def test_missing_or_nonartist_credit_is_rejected(self):
        for credit in [str(uuid.uuid4()), self.album]:
            with self.subTest(credit=credit):
                album = self.record(id=self.album, kind='album', artist_ids=[credit])
                with self.assertRaisesRegex(probe.ProbeError, 'artist reference'):
                    probe.validate(self.artifact([album]), self.root / 'out.jsonl')

    def test_search_folding_preserves_display_names_and_disambiguated_ids(self):
        display = 'İŞIQ ƏTRAFI – Оркестр 夜の'
        self.assertEqual('isiq etrafi – оркестр 夜の', probe.search_key(display))
        a = self.record(name=display)
        b = self.record(id=str(uuid.uuid4()), name=display)
        report = probe.validate(self.artifact([a, b]), self.root / 'out.jsonl')
        self.assertEqual(2, report['entities'])
        self.assertIn(display, (self.root / 'out.jsonl').read_text())

    def test_invalid_values_are_rejected_without_type_coercion(self):
        cases = [
            dict(id=str(uuid.UUID(int=0))), dict(id='invalid'), dict(kind='recording'),
            dict(name=''), dict(name='bad\x00name'), dict(name='x' * 257),
            dict(aliases=['bad\nname']), dict(aliases=['x'] * 9),
            dict(tags=[dict(name='jazz', weight=float('nan'))]),
            dict(tags=[dict(name='jazz', weight=float('inf'))]),
            dict(tags=[dict(name='jazz', weight=True)]),
            dict(tags=[dict(name='jazz', weight=-0.1)]),
            dict(tags=[dict(name='jazz', weight=1.1)]),
            dict(tags=[dict(name='jazz', weight=10 ** 1000)]),
            dict(kind='artist', artist_ids=[self.artist]), dict(unknown='data'),
        ]
        for changes in cases:
            with self.subTest(changes=changes):
                with self.assertRaises(probe.ProbeError):
                    probe.validate(self.artifact([self.record(**changes)]), self.root / 'out.jsonl')

    def test_duplicate_json_keys_and_invalid_utf8_are_rejected(self):
        source = self.root / 'source.jsonl'
        for body in [b'{"origin":"synthetic","origin":"public"}\n', b'\xff\n']:
            source.write_bytes(body)
            with self.subTest(body=body), self.assertRaises(probe.ProbeError):
                probe.validate(source, self.root / 'out.jsonl')

    def test_nonsynthetic_artifact_is_not_implicitly_approved(self):
        header = dict(probe.HEADER, origin='live')
        with self.assertRaisesRegex(probe.ProbeError, 'synthetic'):
            probe.validate(self.artifact([self.record()], header), self.root / 'out.jsonl')

    def test_deep_json_fails_cleanly_without_publication(self):
        source = self.root / 'source.jsonl'
        source.write_text(json.dumps(probe.HEADER) + '\n' + '[' * 2000 + '0' + ']' * 2000)
        with self.assertRaises(probe.ProbeError):
            probe.validate(source, self.root / 'out.jsonl')
        self.assertFalse((self.root / 'out.jsonl').exists())

    def test_input_row_entity_and_output_budgets_are_enforced(self):
        source = self.artifact([self.record(), self.record(id=self.album)])
        limits = [probe.Limits(max_input_bytes=50), probe.Limits(max_line_bytes=50),
                  probe.Limits(max_entities=1), probe.Limits(max_output_bytes=50)]
        for limit in limits:
            with self.subTest(limit=limit), self.assertRaisesRegex(probe.ProbeError, 'limit'):
                probe.validate(source, self.root / 'out.jsonl', limits=limit)
            self.assertFalse((self.root / 'out.jsonl').exists())

    def test_existing_output_is_never_overwritten(self):
        source = self.artifact([self.record()])
        destination = self.root / 'out.jsonl'
        destination.write_text('keep this')
        with self.assertRaises(probe.ProbeError):
            probe.validate(source, destination)
        self.assertEqual('keep this', destination.read_text())
        with self.assertRaises(probe.ProbeError):
            probe.generate(destination, artists=1, albums_per_artist=1)
        self.assertEqual('keep this', destination.read_text())

    def test_sql_copy_escapes_input_and_schema_cannot_be_injected(self):
        name = "Synthetic '); DROP SCHEMA public; -- \\ name"
        source = self.artifact([self.record(name=name, aliases=[])])
        normalized = self.root / 'out.jsonl'
        probe.validate(source, normalized)
        sql = io.StringIO()
        probe.write_import_sql(normalized, sql, 'liner_probe_abc123')
        self.assertIn('COPY entities', sql.getvalue())
        self.assertIn('\\\\ name', sql.getvalue())
        self.assertEqual(4, sql.getvalue().count('\n\\.\n'))
        with self.assertRaises(probe.ProbeError):
            probe.write_import_sql(normalized, io.StringIO(), 'public; DROP DATABASE x')

    def test_probe_requires_explicit_loopback_and_dedicated_database(self):
        base = dict(PGHOST='127.0.0.1', PGPORT='5432', PGDATABASE='liner_catalog_probe_test',
                    PGUSER='postgres', PGPASSWORD=secrets.token_urlsafe(24))
        self.assertEqual('127.0.0.1', probe.database_environment(base)['PGHOST'])
        for changes in [dict(PGHOST='example.com'), dict(PGHOST='0.0.0.0'),
                        dict(PGDATABASE='postgres'), dict(PGHOSTADDR='203.0.113.1'),
                        dict(PGPORT='0'), dict(PGDATABASE='liner_catalog_probe_test;drop')]:
            with self.subTest(changes=changes), self.assertRaises(probe.ProbeError):
                probe.database_environment(dict(base, **changes))

    def test_generator_rejects_unbounded_and_invalid_counts(self):
        for artists, albums in [(0, 1), (-1, 1), (1, -1), (1, 21), (1_000_001, 1)]:
            with self.subTest(artists=artists, albums=albums), self.assertRaises(probe.ProbeError):
                probe.generate(self.root / 'out.jsonl', artists=artists, albums_per_artist=albums)

    def test_prepared_sample_is_bounded_and_distinguished_from_real_evidence(self):
        source = self.root / 'sample.jsonl'
        probe.generate(source, artists=20, albums_per_artist=1)
        sample = probe.evidence_sample(source, seeds=2, candidates=5)
        self.assertEqual(('synthetic', 2, 5),
                         (sample['origin'], sample['sample_seeds'], sample['candidates_per_seed']))
        self.assertGreater(sample['minimum_bundle_bytes'], 0)
        self.assertIn('excludes', sample['scope'])
        with self.assertRaises(probe.ProbeError):
            probe.evidence_sample(source, seeds=1001)


if __name__ == '__main__':
    unittest.main()
