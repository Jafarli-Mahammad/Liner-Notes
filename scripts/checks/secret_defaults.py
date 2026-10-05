import json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
config = json.loads((root / 'src/Presentation/appsettings.json').read_text())
assert not config.get('ConnectionStrings', {}).get('DefaultConnection'), 'Tracked database configuration must not carry default credentials'
source = (root / 'src/DataAccess/DependencyInjection.cs').read_text()
assert 'Password=postgres' not in source, 'DataAccess silently falls back to a database password'
ignore = (root / '.gitignore').read_text()
for rule in ['.env.*', 'appsettings.Local.json', 'recordings/']:
    assert rule in ignore, f'Missing local secret/recording ignore rule: {rule}'
print('PASS secret_defaults: no tracked database credential fallback; local configuration ignored')
