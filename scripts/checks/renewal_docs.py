from pathlib import Path

root = Path(__file__).resolve().parents[2]
progress = (root / 'PROGRESS.md').read_text()
readme = (root / 'README.md').read_text()
assert 'Founder Taste Notes' not in progress, 'Personal taste notes remain in progress'
assert 'Fit Over Fame' not in progress, 'Removed pillar remains in progress'
assert 'popularity-neutral' in progress, 'Approved V1 policy is missing'
assert 'not approved' in progress, 'Later-phase approval gates are missing'
assert 'src/Presentation' in readme, 'README uses an obsolete host path'
assert 'not verified' in readme, 'README must distinguish intent from verified implementation'
assert 'Anti-Popularity Bias' not in readme, 'README contradicts popularity-neutral V1'
print('PASS renewal_docs: decisions, gates, removed content, and qualified README')
