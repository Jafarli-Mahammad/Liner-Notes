"""Characterize Phase 5 against the preserved fixed pool; never treat fewer picks as listening-quality proof."""
import json
import sys
from residue_phase2_contract import extract


def compare(before, after):
    if len(before) != 16 or len(after) != 16:
        raise ValueError('Expected eight fixed genres in both acquisition modes')
    paired = 0
    cases = []
    for old, new in zip(before, after):
        if (old['genre'], old['acquisition']) != (new['genre'], new['acquisition']):
            raise ValueError('Unmatched pool/profile cases')
        old_picks = {pick['Track']['TrackKey']: pick['Breakdown'] for pick in old['result']['Picks']}
        coverage = json.loads(new['result']['CoverageJson'])
        selection = coverage['Selection']
        decisions = {row['TrackKey']: row for row in selection['Decisions']}
        inputs = {row['TrackKey']: row for row in selection['Input']['Candidates']}
        picks = new['result']['Picks']
        if len(inputs) != 16 or len(decisions) != 16 or selection['Shortfall'] != 5 - len(picks):
            raise ValueError('Incomplete considered-pool or shortfall receipt')
        if selection['SelectedKeys'] != [pick['Track']['TrackKey'] for pick in picks]:
            raise ValueError('Receipt differs from displayed order')
        for pick in picks:
            key = pick['Track']['TrackKey']
            if inputs[key]['RecordingSeed'] is None and decisions[key]['Affinity'] < selection['Input']['Configuration']['MinimumAffinity']:
                raise ValueError('Unsupported novelty-only pick')
            if key not in old_picks:
                raise ValueError('No preserved paired score for the selected fixed-pool fixture')
            for field in ('FinalScore', 'TagSimilarityRaw', 'TagSimilarityScore', 'NoveltyRaw', 'NoveltyBoost'):
                if pick['Breakdown'][field] != old_picks[key][field]:
                    raise ValueError('Paired numerical score changed: ' + field)
            paired += 1
        cases.append({'genre': old['genre'], 'acquisition': old['acquisition'], 'beforePicks': len(old_picks), 'afterPicks': len(picks)})
    return {'cases': cases, 'pairedUnchangedNumericalScores': paired,
            'beforePicks': sum(case['beforePicks'] for case in cases), 'afterPicks': sum(case['afterPicks'] for case in cases),
            'limit': 'Synthetic selection characterization, not musical-quality evidence.'}


if __name__ == '__main__':
    if len(sys.argv) != 3:
        raise SystemExit('Usage: residue_phase5_contract.py BEFORE_LOG AFTER_LOG')
    before, before_hash = extract(sys.argv[1])
    after, after_hash = extract(sys.argv[2])
    print(json.dumps(compare(before, after) | {'beforeSha256': before_hash, 'afterSha256': after_hash}))
