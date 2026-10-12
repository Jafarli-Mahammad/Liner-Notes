#!/usr/bin/env python3
"""Exercise only disposable synthetic accounts in a uniquely owned Compose stack."""
import argparse
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import time
from urllib import error, parse, request
import uuid

ROOT = Path(__file__).resolve().parents[2]


def field(obj, name):
    return next((value for key, value in obj.items() if key.lower() == name.lower()), None)


class Check:
    def __init__(self, env_file, project, port):
        self.base = ["docker", "compose", "--env-file", str(env_file), "-p", project]
        self.origin = f"http://127.0.0.1:{port}"

    def compose(self, *args, expected=0):
        result = subprocess.run(self.base + list(args), cwd=ROOT, capture_output=True, text=True, timeout=900)
        if result.returncode != expected:
            # Never dump expanded settings, operator capabilities or capture output.
            raise RuntimeError(f"Compose {args[0]} failed (exit {result.returncode})")
        return result.stdout

    def wait(self):
        for _ in range(60):
            try:
                status, _ = self.http("GET", "/health")
                if status == 200:
                    return
            except RuntimeError:
                pass
            time.sleep(1)
        raise RuntimeError("local web did not become healthy")

    def http(self, method, path, payload=None, token=None, form=False):
        headers = {}
        data = None
        if payload is not None:
            data = (parse.urlencode(payload) if form else json.dumps(payload)).encode()
            headers["Content-Type"] = "application/x-www-form-urlencoded" if form else "application/json"
        if token:
            headers["Authorization"] = "Bearer " + token
        req = request.Request(self.origin + path, data=data, headers=headers, method=method)
        try:
            with request.urlopen(req, timeout=30) as response:
                body = response.read()
                return response.status, json.loads(body) if response.headers.get_content_type() == "application/json" else body
        except error.HTTPError as response:
            return response.code, None
        except OSError:
            raise RuntimeError("local HTTP request failed") from None

    def worker(self, user, week, action, extra=(), expected=0):
        args = ["--action", action, "--origin", "Synthetic"]
        args += ["--Schedule:Users:0", user] if action == "schedule" else ["--user", user, "--week", week]
        output = self.compose("run", "--rm", "--no-deps", *extra, "worker", *args, expected=expected)
        for line in reversed(output.splitlines()):
            try:
                return json.loads(line)
            except json.JSONDecodeError:
                pass
        raise RuntimeError("Worker did not emit its result")

    def lifecycle(self):
        now = datetime.now(timezone.utc)
        iso = now.isocalendar()
        week = f"{iso.year}-W{iso.week:02}"
        suffix = uuid.uuid4().hex
        status, auth = self.http("POST", "/api/auth/register", {
            "userName": "phase9_" + suffix,
            "email": "phase9_" + suffix + "@example.invalid",
            "password": secrets.token_urlsafe(24) + "aA1!",
            "seedArtists": [{"artistName": artist} for artist in ("Jakuzi", "Son Feci Bisiklet", "M.O.O.N.")]
        })
        assert status == 201, "disposable account registration failed"
        token = field(auth, "accessToken")
        user = str(uuid.UUID(field(field(auth, "user"), "id")))
        self.worker(user, week, "reconcile")
        generated = self.worker(user, week, "generate")
        assert field(generated, "status") == "generated", "synthetic generation failed"
        self.worker(user, week, "reconcile")
        captured = self.worker(user, week, "capture")
        assert field(captured, "status") == "local_captured", "encrypted capture failed"
        status, export = self.http("GET", "/api/export/my-data", token=token)
        assert status == 200 and field(export, "exportVersion") == "2.2", "export contract changed"
        digest = field(export, "digests")[0]
        assert field(digest, "status") == "LocalCaptured" and field(digest, "sentAt") is None
        assert 0 < len(field(digest, "recommendations")) <= 5, "fixture picks missing"
        assert len(field(field(export, "localEmail"), "copies")) == 1, "shared capture unavailable to web"
        print("PASS Compose: registration, synthetic generation, encrypted capture and export")

        self.compose("up", "-d", "--no-deps", "--force-recreate", "web")
        self.wait()
        assert field(self.worker(user, week, "capture"), "status") == "already_captured"
        assert self.http("GET", "/api/export/my-data", token=token)[0] == 200
        print("PASS Compose: application recreation retains keys, copies and access")

        due = (now - timedelta(days=now.weekday())).replace(hour=0, minute=0, second=0, microsecond=0)
        sql = f'''UPDATE public."Users" SET "DeliveryDay"='Monday', "DeliveryHourUtc"=0,
                  "NextDigestAt"='{due.isoformat()}' WHERE "Id"='{user}';'''
        self.compose("exec", "-T", "db", "psql", "-U", "postgres", "-d", "liner_notes_manual_qa", "-v", "ON_ERROR_STOP=1", "-c", sql)
        self.worker(user, week, "reconcile")
        scheduled = self.worker(user, week, "schedule")
        assert field(scheduled, "status") == "completed", "bounded schedule failed"
        ticks = field(scheduled, "ticks")
        assert ticks and field(field(ticks[0], "jobs")[0], "status") == "advanced", "bounded schedule did not advance"
        repeated = self.worker(user, week, "schedule")
        assert field(field(repeated, "ticks")[0], "jobs") == [], "schedule advanced twice"
        print("PASS Compose: bounded UTC scheduling and idempotent advancement")

        output = self.compose("run", "--rm", "--no-deps", "operator", "--action", "unsubscribe-link", "--user", user, "--week", week)
        link = next((line for line in reversed(output.splitlines()) if line.startswith(self.origin + "/api/digests/unsubscribe?")), None)
        assert link, "operator could not recover stored unsubscribe capability"
        parts = parse.urlsplit(link)
        capability = parse.parse_qs(parts.query)["token"][0]
        assert self.http("GET", parts.path + "?" + parts.query)[0] == 200
        before = self.http("GET", "/api/export/my-data", token=token)[1]
        assert field(field(before, "subscriber"), "emailUnsubscribedAtUtc") is None, "GET changed subscription"
        assert self.http("POST", parts.path, {"token": capability}, form=True)[0] == 200
        stopped = self.worker(user, week, "capture", expected=2)
        assert field(stopped, "reason") == "email_unsubscribed", "unsubscribe did not suppress capture"
        print("PASS Compose: read-only unsubscribe GET, confirmation POST and capture suppression")

        assert self.http("DELETE", "/api/subscribers/me", token=token)[0] == 204
        assert self.http("GET", "/api/export/my-data", token=token)[0] in (401, 404)
        remaining = self.compose("exec", "-T", "web", "sh", "-c", "find /app/local-state/copies -type f | wc -l")
        assert int(remaining.strip()) == 0, "owned capture remained after deletion"
        identity = self.compose("exec", "-T", "web", "id", "-u")
        assert int(identity.strip()) != 0, "web runs as root"
        print("PASS Compose: deletion invalidates access, removes owned copies and uses non-root web")
        stopped = self.worker(user, week, "capture", extra=("-e", "DOTNET_ENVIRONMENT=Production"), expected=2)
        assert field(stopped, "status") == "stopped", "Production Worker guard changed"
        output = self.compose("run", "--rm", "--no-deps", "worker", "--action", "capture",
                              "--user", user, "--week", week, "--origin", "Live", expected=2)
        assert field(json.loads(output.splitlines()[-1]), "status") == "stopped", "live-origin guard changed"
        print("PASS Compose: Production Worker and live origins remain rejected")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=18089)
    parser.add_argument("--build", action="store_true")
    args = parser.parse_args()
    if not 1024 <= args.port <= 65535:
        parser.error("port must be 1024–65535")
    project = "liner-phase9-qa-" + uuid.uuid4().hex[:12]
    with tempfile.TemporaryDirectory(prefix="liner-phase9-compose-") as directory:
        env_file = Path(directory) / ".env.phase9.local"
        subprocess.run(["python3", str(ROOT / "scripts/phase9_init_local.py"), "--output", str(env_file)], check=True, capture_output=True)
        with env_file.open("a") as output:
            output.write(f"LINER_WEB_PORT={args.port}\n")
        check = Check(env_file, project, args.port)
        try:
            if args.build:
                check.compose("--profile", "local-worker", "--profile", "local-operator", "build")
            override = Path(directory) / "invalid-migration.yaml"
            override.write_text('services:\n  migrate:\n    command: ["invalid-phase9-command"]\n')
            # The same task-owned project has no user data yet. A failed migration
            # must leave web unstarted; replacing the override then permits startup.
            check.compose("-f", "compose.yaml", "-f", str(override), "up", "-d", "web", expected=1)
            state = check.compose("ps", "--all", "--format", "json", "web")
            assert all(json.loads(line).get("State") != "running" for line in state.splitlines() if line.strip()), "web started after migration failure"
            print("PASS Compose: failed migration prevents web startup")
            check.compose("up", "-d", "web")
            check.compose("run", "--rm", "--no-deps", "migrate")
            print("PASS Compose: fresh migrations and repeated application are idempotent")
            check.wait()
            check.lifecycle()
        finally:
            # Only resources created under this random, task-owned project name.
            check.compose("down", "--volumes", "--remove-orphans")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, AssertionError, subprocess.TimeoutExpired) as error:
        raise SystemExit(f"FAIL Compose verification: {error}") from None
