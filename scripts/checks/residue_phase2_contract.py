"""Compare Phase 2's frozen eight-genre outputs with 5a91411, allowing only receipt metadata changes."""
import hashlib
import json
import sys
from pathlib import Path


def extract(path):
    line = next(line for line in Path(path).read_text().splitlines() if 'CONTRACT_JSON=' in line)
    raw = line.split('CONTRACT_JSON=', 1)[1].strip()
    return json.loads(raw), hashlib.sha256(raw.encode()).hexdigest()


def normalize(value):
    if isinstance(value, list):
        return [normalize(item) for item in value]
    if not isinstance(value, dict):
        return value
    result = {}
    for key, item in value.items():
        if key in ('TasteEvidenceSha256', 'TasteEvidenceCount', 'FavoriteEvidence', 'ConfigurationSha256', 'ReservationBytes'):
            continue
        if key == 'ConfigurationPolicy':
            item = {k: v for k, v in item.items() if k != 'TastePreparation'}
        if key == 'CoverageJson':
            item = json.loads(item)
        result[key] = normalize(item)
    return result


def compare(before, after):
    if len(before) != 16 or len(after) != 16:
        raise ValueError('Expected the fixed eight-genre, two-acquisition-mode contract')
    if normalize(before) != normalize(after):
        raise ValueError('Behavior differs beyond Phase 2 receipt/configuration metadata and payload reservation')


if __name__ == '__main__':
    if len(sys.argv) != 3:
        raise SystemExit('Usage: residue_phase2_contract.py BEFORE_LOG AFTER_LOG (detailed DiscoveryOutputContractTests logs)')
    before, before_hash = extract(sys.argv[1])
    after, after_hash = extract(sys.argv[2])
    compare(before, after)
    print(json.dumps({'equalAfterDocumentedMetadataNormalization': True, 'cases': len(before),
                      'beforeSha256': before_hash, 'afterSha256': after_hash}))
