"""Verify Phase 3 changes no-feedback fixtures only through declared policy metadata."""
import importlib.util
import json
import sys
from pathlib import Path

spec = importlib.util.spec_from_file_location('phase2', Path(__file__).with_name('residue_phase2_contract.py'))
phase2 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(phase2)


def normalize(value):
    if isinstance(value, list):
        return [normalize(item) for item in value]
    if not isinstance(value, dict):
        return value
    result = {}
    for key, item in value.items():
        if key == 'FeedbackSupport':
            if item:
                raise ValueError('No-feedback fixture unexpectedly acquired private feedback')
            continue
        if key in ('FeedbackSupportCount', 'FeedbackSupportSha256'):
            if item not in (None, 0):
                raise ValueError('No-feedback fixture unexpectedly acquired support metadata')
            continue
        if key == 'FeedbackPolicy':
            continue
        if key == 'ConfigurationPolicy':
            item = {k: v for k, v in item.items() if k not in ('FeedbackPolicy', 'MinimumNegativeFeedbackRecordings')}
        result[key] = normalize(item)
    return result


if __name__ == '__main__':
    before, before_hash = phase2.extract(sys.argv[1])
    after, after_hash = phase2.extract(sys.argv[2])
    if len(before) != 16 or len(after) != 16 or normalize(phase2.normalize(before)) != normalize(phase2.normalize(after)):
        raise ValueError('No-feedback behavior changed beyond declared policy metadata')
    print(json.dumps({'equivalentNoFeedbackBehavior': True, 'cases': len(after),
                      'beforeSha256': before_hash, 'afterSha256': after_hash}))
