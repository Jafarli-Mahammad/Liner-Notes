from pathlib import Path


def validate_documents(progress, readme):
    assert 'Founder Taste Notes' not in progress, 'Personal taste notes remain in progress'
    assert 'Fit Over Fame' not in progress, 'Removed pillar remains in progress'
    assert 'popularity-neutral' in progress, 'Approved V1 policy is missing'
    assert any(term in progress for term in ('not approved', 'not authorized')), 'Later-phase approval gates are missing'
    assert 'src/Presentation' in readme, 'README uses an obsolete host path'
    assert any(term in readme for term in ('not verified', 'unverified')), 'README must distinguish intent from verified implementation'
    assert 'Anti-Popularity Bias' not in readme, 'README contradicts popularity-neutral V1'


if __name__ == '__main__':
    root = Path(__file__).resolve().parents[2]
    validate_documents((root / 'PROGRESS.md').read_text(), (root / 'README.md').read_text())
    print('PASS renewal_docs: decisions, gates, removed content, and qualified README')
