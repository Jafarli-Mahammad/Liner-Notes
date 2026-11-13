#!/usr/bin/env python3
"""Explicit read-only beta capture and concealed listening sheets. Never tunes Residue."""
import argparse
import hashlib
import html
import json
import os
from pathlib import Path
import secrets
import subprocess
import urllib.parse

MAX_BYTES = 10_000_000
SAFE_SETTINGS = ('Generation:', 'DiscoveryRuntime:', 'DiscoveryTaste:')
SQL = '''BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '15s';
SELECT json_build_object(
 'version','residue-beta-baseline-v1', 'capturedAtUtc',CURRENT_TIMESTAMP,
 'profiles',(SELECT coalesce(json_agg(x),'[]') FROM
   (SELECT "Id", "UserId" IS NOT NULL AS "IsAccount", "Revision", "OnboardingJson", "CreatedAtUtc", "LastActiveAtUtc", "ExpiresAtUtc"
    FROM "ListenerProfiles" ORDER BY "Id" LIMIT 101) x),
 'signals',(SELECT coalesce(json_agg(x),'[]') FROM
   (SELECT "Id", "ProfileId", "TargetType", "TargetValue", "NormalizedTargetValue", "Weight", "Source",
      "CanonicalEntityId", "CatalogRecordId", "SelectionJson" FROM "TasteSignals"
    WHERE NOT "IsDeleted" ORDER BY "ProfileId", "Id" LIMIT 10001) x),
 'batches',(SELECT coalesce(json_agg(x),'[]') FROM
   (SELECT "Id", "ProfileId", "Kind", "InputRevision", "CreatedAtUtc", "CompletedAtUtc", "CoverageJson"
    FROM "DiscoveryBatches" ORDER BY "CreatedAtUtc", "Id" LIMIT 501) x),
 'picks',(SELECT coalesce(json_agg(x),'[]') FROM
   (SELECT r."Id", r."ProfileId", r."BatchId", r."WeeklyDigestId", r."Rank", r."ScoreBreakdown",
      r."Feedback", r."Rating", r."FeedbackGivenAt", r."CreatedAt", r."FeedbackComment" IS NOT NULL AS "HasNote",
      t."Title", t."ArtistName", t."Mbid", t."NormalizedTitle", t."NormalizedArtistName" FROM "WeeklyRecommendations" r JOIN "Tracks" t ON t."Id"=r."TrackId" ORDER BY r."ProfileId", r."BatchId", r."Rank", r."Id" LIMIT 501) x));
ROLLBACK;'''


def write_private(path, value):
    data = json.dumps(value, ensure_ascii=False, indent=2).encode()
    if len(data) > MAX_BYTES:
        raise ValueError('Artifact exceeds 10 MB cap')
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, 'wb') as f:
        f.write(data)
    return hashlib.sha256(data).hexdigest()


def make_review(snapshot, profile_id):
    profiles = {p['Id'] for p in snapshot['profiles']}
    if profile_id not in profiles:
        raise ValueError('Profile not present in baseline')
    picks = [p for p in snapshot['picks'] if p['ProfileId'] == profile_id]
    if not picks:
        raise ValueError('Profile has no preserved recommendations')
    # One review per recording identity; preserve all occurrences privately.
    groups = {}
    for pick in picks:
        key = pick.get('Mbid') or (pick['NormalizedArtistName'], pick['NormalizedTitle'])
        groups.setdefault(key, []).append(pick)
    groups = list(groups.values())
    secrets.SystemRandom().shuffle(groups)
    review, key = [], {}
    for i, occurrences in enumerate(groups, 1):
        opaque = f'item-{i:03}'
        p = occurrences[0]
        review.append({'id': opaque, 'artist': p['ArtistName'], 'title': p['Title'],
                       'enjoyment1to5': None, 'relevance1to5': None,
                       'alreadyKnown': None, 'potentialFavorite': None})
        key[opaque] = [p['Id'] for p in occurrences]
    return {'version': 'residue-listening-v1', 'items': review}, key


def private_directory(path):
    root = Path(subprocess.check_output(['git', 'rev-parse', '--show-toplevel'], text=True).strip())
    relative = path.resolve().relative_to(root)
    ignored = subprocess.run(['git', 'check-ignore', '--no-index', '--quiet', '--', str(relative)]).returncode == 0
    tracked = subprocess.check_output(['git', 'ls-files', '--', str(relative)], text=True).strip()
    if not ignored or tracked:
        raise ValueError('Private artifacts must use an ignored, untracked repository destination')
    path.mkdir(mode=0o700, parents=True, exist_ok=False)


def capture(args):
    result = subprocess.run(['docker', 'exec', args.container, 'psql', '-X', '-q', '-v', 'ON_ERROR_STOP=1',
                             '-U', args.db_user, '-d', args.database, '-Atc', SQL],
                            capture_output=True, timeout=30, check=True)
    if len(result.stdout) > MAX_BYTES:
        raise ValueError('Snapshot exceeds 10 MB cap')
    snapshot = json.loads(result.stdout)
    for name, cap in [('profiles', 100), ('signals', 10000), ('batches', 500), ('picks', 500)]:
        if len(snapshot[name]) > cap:
            raise ValueError(f'{name} exceeds capture bound; use a separately reviewed narrower capture')
    manifest = {'version': 'residue-baseline-manifest-v1', 'database': args.database,
                'sourceHead': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
                'counts': {k: len(snapshot[k]) for k in ('profiles', 'signals', 'batches', 'picks')},
                'scope': 'Read-only current beta; no credentials, account names, emails or feedback-note text.'}
    if args.settings:
        settings = json.loads(Path(args.settings).read_text())
        manifest['configuredSettings'] = {k: v for k, v in settings.items() if k.startswith(SAFE_SETTINGS)}
        manifest['configuredProviderMode'] = settings.get('ListenBrainz:Mode')
        manifest['limitation'] = 'Settings file is not proof of the running process effective configuration.'
    if args.runtime_checkout:
        runtime = Path(args.runtime_checkout).resolve()
        manifest['runtimeCheckoutHead'] = subprocess.check_output(['git', '-C', str(runtime), 'rev-parse', 'HEAD'], text=True).strip()
        manifest['runtimeTrackedDiffSha256'] = hashlib.sha256(subprocess.check_output(['git', '-C', str(runtime), 'diff', 'HEAD'])).hexdigest()
        binary = runtime / 'src/Presentation/bin/Debug/net10.0/LinerNotes.Presentation.dll'
        manifest['runtimeBinarySha256'] = hashlib.sha256(binary.read_bytes()).hexdigest()
    private_directory(args.output)
    manifest['snapshotSha256'] = write_private(args.output / 'snapshot.json', snapshot)
    write_private(args.output / 'manifest.json', manifest)
    print(json.dumps({'counts': manifest['counts'], 'snapshotSha256': manifest['snapshotSha256']}))


def review(args):
    snapshot_path = args.baseline / 'snapshot.json'
    raw = snapshot_path.read_bytes()
    manifest = json.loads((args.baseline / 'manifest.json').read_text())
    if hashlib.sha256(raw).hexdigest() != manifest['snapshotSha256']:
        raise ValueError('Baseline integrity mismatch')
    sheet, key = make_review(json.loads(raw), args.profile)
    private_directory(args.output)
    sheet_hash = write_private(args.output / 'ratings.json', sheet)
    write_private(args.output / '.blind-key.json', {'snapshotSha256': manifest['snapshotSha256'],
                  'sheetSha256': sheet_hash, 'profile': args.profile, 'mapping': key})
    lines = ['# Concealed baseline listening review', '',
             'Rate enjoyment and relevance from 1 (low) to 5 (high) in ratings.json. Mark alreadyKnown and potentialFavorite true/false. Leave unanswered values null. This is one listener baseline, not a global quality judgment. Review before reading explanations or the private key.', '']
    for item in sheet['items']:
        query = urllib.parse.quote(item['artist'] + ' ' + item['title'])
        lines.append(f"- {item['id']}: {html.escape(item['artist'])} — {html.escape(item['title'])} · https://www.youtube.com/results?search_query={query}")
    path = args.output / 'listen.md'
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, 'w') as f:
        f.write('\n'.join(lines) + '\n')
    print(f'Prepared {len(sheet["items"])} concealed baseline items; no alternative system or ratings invented.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    c = commands.add_parser('capture')
    for arg in ('container', 'database', 'db-user'):
        c.add_argument('--' + arg, required=True)
    c.add_argument('--settings', type=Path)
    c.add_argument('--runtime-checkout', type=Path)
    c.add_argument('--output', type=Path, required=True)
    r = commands.add_parser('review')
    r.add_argument('--baseline', type=Path, required=True)
    r.add_argument('--profile', required=True)
    r.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    (capture if args.command == 'capture' else review)(args)


if __name__ == '__main__':
    main()
