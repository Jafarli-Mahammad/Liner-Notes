#!/usr/bin/env python3
"""Generate private credentials for the local fixture-only Compose stack."""
import argparse
import os
from pathlib import Path
import secrets


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path(".env.phase9.local"))
    args = parser.parse_args()
    path = args.output.absolute()
    if not path.name.startswith(".env."):
        parser.error("output must use an ignored .env.* filename")
    value = f"DB_PASSWORD={secrets.token_hex(24)}\nJWT_SECRET={secrets.token_hex(48)}\n"
    try:
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with os.fdopen(descriptor, "w") as output:
            output.write(value)
    except FileExistsError:
        parser.error("environment file already exists; preserve it or choose a new filename")
    print("Created private local environment file; credential values were not printed.")


if __name__ == "__main__":
    main()
