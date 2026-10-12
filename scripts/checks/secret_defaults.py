import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]

presentation = json.loads((root / "src/Presentation/appsettings.json").read_text())
assert not presentation.get("ConnectionStrings", {}).get("DefaultConnection"), (
    "Production database configuration must not carry default credentials"
)
assert not presentation.get("Jwt", {}).get("SecretKey"), (
    "Production JWT signing key must be supplied outside committed settings"
)

for relative_path in (
    "src/Presentation/appsettings.Development.json",
    "src/Worker/appsettings.Development.json",
):
    config = json.loads((root / relative_path).read_text())
    assert not config.get("ConnectionStrings", {}).get("DefaultConnection"), (
        f"{relative_path} must not include a committed database connection string"
    )

development_jwt = json.loads(
    (root / "src/Presentation/appsettings.Development.json").read_text()
).get("Jwt", {})
assert not development_jwt.get("SecretKey"), (
    "Development JWT signing key must be supplied through user-secrets or environment"
)

source = (root / "src/DataAccess/DependencyInjection.cs").read_text()
assert "Password=postgres" not in source, (
    "DataAccess must not silently fall back to the generic database password"
)

ignore = (root / ".gitignore").read_text()
for rule in (".env.*", "appsettings.Local.json", "recordings/"):
    assert rule in ignore, f"Missing local secret/recording ignore rule: {rule}"

local_config = (root / "docs/renewal/local-configuration.md").read_text()
assert "dotnet user-secrets" in local_config, (
    "Local setup must document private user-secrets configuration"
)
print("PASS secret_defaults: database and signing credentials stay outside committed settings")
