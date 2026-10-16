#!/usr/bin/env python3
"""Bounded, synthetic-only catalog probes. No HTTP, credentials in arguments, or live data."""
import argparse
from contextlib import closing, contextmanager
from dataclasses import dataclass
import hashlib
import ipaddress
import json
import math
import os
from pathlib import Path
import re
import resource
import sqlite3
import subprocess
import tempfile
import time
import unicodedata
import uuid

HEADER = {'format': 'liner-catalog-probe/v1', 'origin': 'synthetic', 'source': 'generated-fixture'}
NAMESPACE = uuid.UUID('2b148282-d5cb-413b-a016-feb106cadf90')


class ProbeError(ValueError):
    pass


@dataclass(frozen=True)
class Limits:
    max_entities: int = 1_000_000
    max_input_bytes: int = 512 * 1024 * 1024
    max_output_bytes: int = 512 * 1024 * 1024
    max_line_bytes: int = 32 * 1024

    def check(self):
        if any(type(x) is not int or x <= 0 for x in self.__dict__.values()):
            raise ProbeError('limits must be positive integers')


def encoded(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'), allow_nan=False)


def search_key(value):
    folded = unicodedata.normalize('NFKD', value.casefold()).translate(str.maketrans({'ı': 'i', 'ə': 'e'}))
    return ' '.join(''.join(c for c in folded if not unicodedata.combining(c)).split())


def text(value, maximum=256):
    if not isinstance(value, str) or not value.strip() or len(value) > maximum:
        raise ProbeError('invalid or oversized text')
    if any(unicodedata.category(c) in ('Cc', 'Cs') for c in value):
        raise ProbeError('control or surrogate character in text')
    return value.strip()


def identifier(value):
    try:
        if not isinstance(value, str) or len(value) != 36:
            raise ValueError()
        parsed = uuid.UUID(value)
        if not parsed.int or str(parsed) != value:
            raise ValueError()
        return value
    except ValueError:
        raise ProbeError('invalid canonical entity identifier') from None


def canonical_record(record):
    required = {'id', 'kind', 'name', 'aliases', 'artist_ids', 'tags'}
    if not isinstance(record, dict) or set(record) != required:
        raise ProbeError('unexpected record fields')
    kind = record['kind']
    if kind not in ('artist', 'album'):
        raise ProbeError('unsupported entity kind')
    aliases, artists, tags = (record[k] for k in ('aliases', 'artist_ids', 'tags'))
    if not isinstance(aliases, list) or len(aliases) > 8:
        raise ProbeError('alias count limit')
    if not isinstance(artists, list) or len(artists) > 8:
        raise ProbeError('credited artist count limit')
    if (kind == 'artist' and artists) or (kind == 'album' and not artists):
        raise ProbeError('invalid credited artists for entity kind')
    if not isinstance(tags, list) or len(tags) > 32:
        raise ProbeError('tag count limit')
    cleaned_tags = {}
    for tag in tags:
        if not isinstance(tag, dict) or set(tag) != {'name', 'weight'}:
            raise ProbeError('invalid tag fields')
        name, weight = search_key(text(tag['name'], 100)), tag['weight']
        if type(weight) not in (float, int) or not 0 <= weight <= 1 or not math.isfinite(weight):
            raise ProbeError('invalid finite tag weight')
        cleaned_tags[name] = max(cleaned_tags.get(name, 0.0), float(weight))
    return dict(id=identifier(record['id']), kind=kind, name=text(record['name']),
                aliases=sorted(set(text(a) for a in aliases), key=lambda a: (search_key(a), a)),
                artist_ids=sorted(set(identifier(a) for a in artists)),
                tags=[dict(name=n, weight=w) for n, w in sorted(cleaned_tags.items())])


class Output:
    def __init__(self, handle, maximum):
        self.handle, self.maximum = handle, maximum
        self.size = 0
        self.digest = hashlib.sha256()

    def row(self, value):
        data = (encoded(value) + '\n').encode('utf-8')
        if self.size + len(data) > self.maximum:
            raise ProbeError('output byte limit')
        self.handle.write(data)
        self.size += len(data)
        self.digest.update(data)


@contextmanager
def atomic_output(destination, maximum):
    destination = Path(destination)
    if destination.exists():
        raise ProbeError('output already exists')
    fd, temporary = tempfile.mkstemp(prefix='.catalog-part-', dir=destination.parent)
    try:
        with os.fdopen(fd, 'wb') as handle:
            output = Output(handle, maximum)
            yield output
            handle.flush()
            os.fsync(handle.fileno())
        # Atomic publication without overwriting a file created by another process.
        try:
            os.link(temporary, destination)
        except FileExistsError:
            raise ProbeError('output already exists') from None
    finally:
        Path(temporary).unlink(missing_ok=True)


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ProbeError('duplicate JSON key')
        result[key] = value
    return result


def _invalid_constant(_):
    raise ProbeError('nonfinite JSON number')


def rows(path, limits=Limits()):
    limits.check()
    size = 0
    with Path(path).open('rb') as source:
        row = source.readline(limits.max_line_bytes + 1)
        line = 0
        while row:
            line += 1
            size += len(row)
            if len(row) > limits.max_line_bytes:
                raise ProbeError('line byte limit')
            if size > limits.max_input_bytes:
                raise ProbeError('input byte limit')
            try:
                value = json.loads(row.decode('utf-8'), object_pairs_hook=_unique_object,
                                   parse_constant=_invalid_constant)
            except (UnicodeError, ValueError, RecursionError):
                raise ProbeError(f'invalid UTF-8 or JSON at line {line}') from None
            if line == 1:
                if value != HEADER:
                    raise ProbeError('only the versioned synthetic probe format is accepted')
            else:
                yield value
            row = source.readline(limits.max_line_bytes + 1)
        if line == 0:
            raise ProbeError('missing synthetic header')


def generate(destination, artists=10_000, albums_per_artist=2, limits=Limits()):
    limits.check()
    if (type(artists) is not int or not 1 <= artists <= 1_000_000 or
            type(albums_per_artist) is not int or not 0 <= albums_per_artist <= 20 or
            artists * (1 + albums_per_artist) > limits.max_entities):
        raise ProbeError('generator count limit')
    names = ['Synthetic Artist', 'Süni İşıq', 'Оркестр', '夜の音', 'Électronique',
             'Synthetic Artist', 'Əks Səda', 'Indie Ensemble']
    genres = ['electronic', 'jazz', 'indie', 'ambient', 'classical', 'folk', 'rock', 'soundtrack']
    with atomic_output(destination, limits.max_output_bytes) as output:
        output.row(HEADER)
        for index in range(artists):
            artist_id = str(uuid.uuid5(NAMESPACE, f'artist/{index}'))
            tags = [dict(name=genres[index % len(genres)], weight=1.0),
                    dict(name=genres[(index + 1) % len(genres)], weight=0.4)]
            output.row(dict(id=artist_id, kind='artist', name=f'{names[index % len(names)]} {index:08d}',
                            aliases=[f'Artist {index:08d}', f'Probe Artist {index:08d}'], artist_ids=[], tags=tags))
            for album in range(albums_per_artist):
                output.row(dict(id=str(uuid.uuid5(NAMESPACE, f'album/{index}/{album}')), kind='album',
                                name=f'Synthetic Album {index:08d} Volume {album + 1}',
                                aliases=[f'Album {index:08d}-{album + 1}'], artist_ids=[artist_id], tags=tags))
    return dict(origin='synthetic', artists=artists, albums=artists * albums_per_artist,
                bytes=output.size, sha256=output.digest.hexdigest())


def validate(source, destination, limits=Limits()):
    limits.check()
    counts = dict(artists=0, albums=0, entities=0, duplicate_records=0, terms=0, artist_links=0, tag_assignments=0)
    started = time.perf_counter()
    # Disk-backed identity/reference validation keeps memory independent of corpus size.
    with tempfile.TemporaryDirectory(prefix='.catalog-check-', dir=Path(destination).parent) as scratch:
        database = Path(scratch) / 'identity.sqlite'
        with closing(sqlite3.connect(database)) as db, db, atomic_output(destination, limits.max_output_bytes) as output:
            db.executescript('PRAGMA journal_mode=OFF; PRAGMA cache_size=-2048; PRAGMA mmap_size=0; '
                             'PRAGMA temp_store=FILE; CREATE TABLE entity(id TEXT PRIMARY KEY, kind TEXT, hash TEXT); '
                             'CREATE TABLE credit(album TEXT, artist TEXT);')
            output.row(HEADER)
            for raw in rows(source, limits):
                record = canonical_record(raw)
                digest = hashlib.sha256(encoded(record).encode('utf-8')).hexdigest()
                existing = db.execute('SELECT hash FROM entity WHERE id=?', (record['id'],)).fetchone()
                if existing:
                    if existing[0] != digest:
                        raise ProbeError('conflicting canonical entity identifier')
                    counts['duplicate_records'] += 1
                    continue
                if counts['entities'] >= limits.max_entities:
                    raise ProbeError('entity count limit')
                db.execute('INSERT INTO entity VALUES (?,?,?)', (record['id'], record['kind'], digest))
                db.executemany('INSERT INTO credit VALUES (?,?)', [(record['id'], a) for a in record['artist_ids']])
                counts[record['kind'] + 's'] += 1
                counts['entities'] += 1
                counts['artist_links'] += len(record['artist_ids'])
                counts['terms'] += len({search_key(record['name']), *(search_key(a) for a in record['aliases'])})
                counts['tag_assignments'] += len(record['tags'])
                output.row(record)
            db.commit()
            invalid = db.execute("SELECT c.artist FROM credit c LEFT JOIN entity e ON e.id=c.artist "
                                 "WHERE e.kind IS NULL OR e.kind!='artist' LIMIT 1").fetchone()
            if invalid:
                raise ProbeError('missing or nonartist reference')
            scratch_bytes = database.stat().st_size
    return dict(counts, origin='synthetic', normalized_bytes=output.size,
                normalized_sha256=output.digest.hexdigest(), scratch_bytes=scratch_bytes,
                elapsed_seconds=round(time.perf_counter() - started, 3))


def _schema(value):
    if not re.fullmatch(r'liner_probe_[a-f0-9]{6,32}', value):
        raise ProbeError('invalid isolated schema identifier')
    return value


def _copy(value):
    return str(value).replace('\\', '\\\\').replace('\t', '\\t').replace('\n', '\\n').replace('\r', '\\r')


def write_import_sql(source, output, schema):
    _schema(schema)
    output.write(f'''\\set ON_ERROR_STOP on
CREATE EXTENSION IF NOT EXISTS pg_trgm;
BEGIN;
CREATE SCHEMA {schema};
SET search_path TO {schema},public;
SET work_mem='4MB';
SET maintenance_work_mem='32MB';
SET temp_file_limit='128MB';
CREATE TABLE entities(id uuid PRIMARY KEY, kind text NOT NULL, name text NOT NULL);
CREATE TABLE terms(entity_id uuid REFERENCES entities(id), kind text NOT NULL, term text COLLATE "C" NOT NULL);
CREATE TABLE credits(album_id uuid REFERENCES entities(id), artist_id uuid REFERENCES entities(id));
CREATE TABLE tags(entity_id uuid REFERENCES entities(id), tag text NOT NULL, weight double precision NOT NULL);
COPY entities(id,kind,name) FROM STDIN;
''')
    for record in rows(source):
        output.write('\t'.join(_copy(record[k]) for k in ('id', 'kind', 'name')) + '\n')
    output.write('\\.\nCOPY terms(entity_id,kind,term) FROM STDIN;\n')
    for record in rows(source):
        for term in sorted({search_key(record['name']), *(search_key(a) for a in record['aliases'])}):
            output.write('\t'.join(_copy(v) for v in (record['id'], record['kind'], term)) + '\n')
    output.write('\\.\nCOPY credits(album_id,artist_id) FROM STDIN;\n')
    for record in rows(source):
        for artist in record['artist_ids']:
            output.write(f"{record['id']}\t{artist}\n")
    output.write('\\.\nCOPY tags(entity_id,tag,weight) FROM STDIN;\n')
    for record in rows(source):
        for tag in record['tags']:
            output.write('\t'.join(_copy(v) for v in (record['id'], tag['name'], tag['weight'])) + '\n')
    output.write('\\.\nCREATE INDEX terms_prefix ON terms(kind,term,entity_id);\n'
                 'CREATE INDEX terms_typo ON terms USING gist(term gist_trgm_ops(siglen=32));\n'
                 'CREATE INDEX credits_artist ON credits(artist_id);\n'
                 'ANALYZE entities; ANALYZE terms; ANALYZE credits; ANALYZE tags; COMMIT;\n')


def database_environment(environment=None):
    env = dict(os.environ if environment is None else environment)
    try:
        if not ipaddress.ip_address(env.get('PGHOST', '')).is_loopback:
            raise ValueError()
        if env.get('PGHOSTADDR') and not ipaddress.ip_address(env['PGHOSTADDR']).is_loopback:
            raise ValueError()
        if not 1 <= int(env.get('PGPORT', '5432')) <= 65535:
            raise ValueError()
        if not re.fullmatch(r'liner_catalog_probe_[a-z0-9_]{1,40}', env.get('PGDATABASE', '')):
            raise ValueError()
    except ValueError:
        raise ProbeError('probe requires explicit loopback host, valid port and dedicated database prefix') from None
    env.update(PGCONNECT_TIMEOUT='5', PGAPPNAME='liner_catalog_probe')
    for key in ('PGSERVICE', 'PGSERVICEFILE', 'PGOPTIONS'):
        env.pop(key, None)
    return env


def _psql(env, sql=None, handle=None, timeout=120):
    try:
        result = subprocess.run(['psql', '-X', '-v', 'ON_ERROR_STOP=1', '-A', '-t', '-q'],
                                input=sql, stdin=handle, text=True, capture_output=True, env=env, timeout=timeout)
    except subprocess.TimeoutExpired:
        raise ProbeError('database probe deadline exceeded') from None
    if result.returncode:
        # Never report a libpq environment or a credential, including on failure.
        message = result.stderr.replace(env.get('PGPASSWORD') or '\x00', '[redacted]')
        raise ProbeError('database probe failed: ' + message[-500:])
    return result.stdout.strip()


def _literal(value):
    return "'" + value.replace("'", "''") + "'"


def _query(schema, term, kind, fuzzy=False):
    key = search_key(term)
    if fuzzy:
        match = f"SELECT entity_id,term <-> {_literal(key)} AS distance FROM {schema}.terms "
        match += f"WHERE kind={_literal(kind)} ORDER BY term <-> {_literal(key)},entity_id LIMIT 80"
        return f'SELECT DISTINCT entity_id FROM ({match}) matches WHERE distance <= 0.65 LIMIT 10'
    pattern = key.replace('\\', '\\\\').replace('%', '\\%').replace('_', '\\_') + '%'
    match = f'SELECT entity_id FROM {schema}.terms WHERE kind={_literal(kind)} '
    match += f"AND term LIKE {_literal(pattern)} ESCAPE E'\\\\' ORDER BY term,entity_id LIMIT 80"
    return f'SELECT DISTINCT entity_id FROM ({match}) matches LIMIT 10'


def _nodes(plan):
    yield plan['Node Type']
    for child in plan.get('Plans', []):
        yield from _nodes(child)


def percentile(values, fraction):
    return sorted(values)[max(0, math.ceil(len(values) * fraction) - 1)]


def process_memory():
    values = dict(ru_maxrss_kib=resource.getrusage(resource.RUSAGE_SELF).ru_maxrss)
    status = Path('/proc/self/status')
    if status.exists():
        for line in status.read_text().splitlines():
            if line.startswith(('VmHWM:', 'VmRSS:')):
                key, number, _ = line.split()
                values['process_' + key[:-1].lower() + '_kib'] = int(number)
    return values


def evidence_sample(source, seeds=16, candidates=10):
    if not 1 <= seeds <= 1000 or not 1 <= candidates <= 100:
        raise ProbeError('evidence sample count limit')
    sizes, tag_counts = [], set()
    for raw in rows(source):
        record = canonical_record(raw)
        if record['kind'] != 'artist':
            continue
        reference = dict(provider='CatalogProbe', method='synthetic.fixture', origin='Synthetic',
                         request_identity=record['id'], response_sha256=hashlib.sha256(encoded(record).encode()).hexdigest())
        tracks = [dict(id=str(uuid.uuid5(NAMESPACE, f"track/{record['id']}/{i}")),
                       title=f'Synthetic Recording {i}', artist_id=record['id'], tags=record['tags'],
                       discovery_path=dict(seed=record['id'], reference=reference)) for i in range(candidates)]
        bundle = dict(seed_id=record['id'], seed_tags=record['tags'], source=reference, candidates=tracks)
        sizes.append(len(encoded(bundle).encode('utf-8')))
        tag_counts.add(len(record['tags']))
        if len(sizes) == seeds:
            break
    if not sizes:
        raise ProbeError('no artist seeds in evidence sample')
    return dict(origin='synthetic', sample_seeds=len(sizes), candidates_per_seed=candidates,
                observed_tag_counts=sorted(tag_counts), minimum_bundle_bytes=min(sizes),
                median_bundle_bytes=percentile(sizes, 0.5), maximum_bundle_bytes=max(sizes),
                scope='illustrative prepared bundle; excludes database/index and personalized snapshot overhead')


def postgres_probe(source, repeats=7, max_database_bytes=512 * 1024 * 1024, environment=None):
    if not 1 <= repeats <= 100 or max_database_bytes <= 0:
        raise ProbeError('invalid database probe limit')
    env = database_environment(environment)
    schema = 'liner_probe_' + uuid.uuid4().hex[:12]
    with tempfile.TemporaryDirectory(prefix='liner-probe-') as directory:
        normalized = Path(directory) / 'normalized.jsonl'
        validation = validate(source, normalized)
        # Conservative probe admission only; production imports need real shared reservations.
        if validation['normalized_bytes'] * 4 > max_database_bytes:
            raise ProbeError('conservative database admission limit')
        sql = Path(directory) / 'import.sql'
        with sql.open('w', encoding='utf-8') as output:
            write_import_sql(normalized, output, schema)
        started = time.perf_counter()
        try:
            with sql.open(encoding='utf-8') as handle:
                _psql(env, handle=handle, timeout=600)
            import_seconds = time.perf_counter() - started
            size = json.loads(_psql(env, f"""SELECT json_build_object('server_version',version(),
                'table_bytes',sum(pg_table_size(c.oid)), 'index_bytes',sum(pg_indexes_size(c.oid)),
                'total_bytes',sum(pg_total_relation_size(c.oid)),
                'database_bytes',pg_database_size(current_database()),
                'shared_buffers',current_setting('shared_buffers'), 'work_mem',current_setting('work_mem'))
                FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='{schema}' AND c.relkind='r';"""))
            if size['total_bytes'] > max_database_bytes:
                raise ProbeError('measured database byte limit')
            cases = [('short', 'ar', 'artist', False), ('common', 'synthetic', 'artist', False),
                     ('alias', 'probe artist 0000', 'artist', False), ('accent', 'suni isi', 'artist', False),
                     ('cyrillic', 'оркестр', 'artist', False), ('cjk', '夜の', 'artist', False),
                     ('album', 'synthetic album 0000', 'album', False),
                     ('typo', 'synthetci artist 00001234', 'artist', True)]
            queries = []
            for name, term, kind, fuzzy in cases:
                query, times, nodes, result_count = _query(schema, term, kind, fuzzy), [], set(), 0
                for _ in range(repeats):
                    raw = _psql(env, "SET statement_timeout='2s'; EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) " + query)
                    plan = json.loads(raw)[0]
                    times.append(plan['Execution Time'])
                    nodes.update(_nodes(plan['Plan']))
                    result_count = plan['Plan']['Actual Rows']
                queries.append(dict(case=name, result_count=result_count, p50_ms=percentile(times, 0.5),
                                    p95_ms=percentile(times, 0.95), maximum_ms=max(times), plan_nodes=sorted(nodes)))
            return dict(origin='synthetic', scenario='single-client SQL probe; not API or whole-host capacity',
                        validation=validation, database=size, import_seconds=round(import_seconds, 3),
                        sql_spool_bytes=sql.stat().st_size, repeats=repeats, queries=queries,
                        probe_memory=process_memory())
        finally:
            _psql(env, f'DROP SCHEMA IF EXISTS {schema} CASCADE;', timeout=30)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    generator = sub.add_parser('generate')
    generator.add_argument('--output', type=Path, required=True)
    generator.add_argument('--artists', type=int, default=10_000)
    generator.add_argument('--albums-per-artist', type=int, default=2)
    validator = sub.add_parser('validate')
    validator.add_argument('--input', type=Path, required=True)
    validator.add_argument('--output', type=Path, required=True)
    postgres = sub.add_parser('postgres')
    postgres.add_argument('--input', type=Path, required=True)
    postgres.add_argument('--repeats', type=int, default=7)
    postgres.add_argument('--max-database-bytes', type=int, default=512 * 1024 * 1024)
    evidence = sub.add_parser('evidence')
    evidence.add_argument('--input', type=Path, required=True)
    evidence.add_argument('--seeds', type=int, default=16)
    evidence.add_argument('--candidates', type=int, default=10)
    args = parser.parse_args()
    try:
        if args.action == 'generate':
            result = generate(args.output, args.artists, args.albums_per_artist)
        elif args.action == 'validate':
            result = validate(args.input, args.output)
        elif args.action == 'evidence':
            result = evidence_sample(args.input, args.seeds, args.candidates)
        else:
            result = postgres_probe(args.input, args.repeats, args.max_database_bytes)
        print(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False))
    except (ProbeError, OSError) as error:
        parser.exit(1, f'FAIL catalog probe: {error}\n')


if __name__ == '__main__':
    main()
