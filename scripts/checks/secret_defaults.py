import json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
config = json.loads((root / 'src/Presentation/appsettings.json').read_text())
assert not config.get('ConnectionStrings', {}).get('DefaultConnection'), 'Production database configuration must not carry default credentials'
source = (root / 'src/DataAccess/DependencyInjection.cs').read_text()
assert 'Password=postgres' not in source, 'DataAccess silently falls back to a database password'
ignore = (root / '.gitignore').read_text()
for rule in ['.env.*', 'appsettings.Local.json', 'recordings/']:
    assert rule in ignore, f'Missing local secret/recording ignore rule: {rule}'
development = json.loads((root / 'src/Presentation/appsettings.Development.json').read_text())
assert development['ConnectionStrings']['DefaultConnection'].startswith('Host=127.0.0.1;'), 'Development database must bind to loopback'
assert 'Password=postgres' in development['ConnectionStrings']['DefaultConnection'], 'Requested local development default missing'
assert development['Jwt']['SecretKey'] != config['Jwt']['SecretKey'], 'Development signing key must not become the production default'
print('PASS secret_defaults: explicit loopback development defaults; production stays unconfigured')
