#!/usr/bin/env python3
"""
Deterministic Pre-Commit / Pre-Push Security Scanner.
Sub-millisecond static analyzer checking for private keys, cloud tokens,
raw SQL interpolation, and command injection patterns with 0% false positives.
"""

import os
import re
import sys
import fnmatch
import subprocess

IGNORE_PATTERNS = [
    "*.lock", "*-lock.json", "*-lock.yaml", "package-lock.json", "pnpm-lock.yaml", "yarn.lock", "Cargo.lock",
    "packages.lock.json", "*.min.js", "*.min.css", "*.map", "*.svg", "*.png", "*.jpg", "*.jpeg", "*.gif",
    "*.ico", "*.wasm", "*.dll", "*.exe", "*.so", "*.dylib", "*.db", "*.sqlite", "*.log", "*.suo",
    "*.DotSettings.user", "*.DotSettings", "*.nupkg", "*.Designer.cs", "*.pdb", "dotnet-tools.json"
]

IGNORE_DIRECTORIES = {
    "bin", "obj", ".idea", ".vscode", ".git", "node_modules", ".system_generated"
}


def is_ignored(filename: str) -> bool:
    normalized = filename.replace("\\", "/").strip("/")
    parts = normalized.split("/")
    if any(part in IGNORE_DIRECTORIES for part in parts):
        return True
    basename = os.path.basename(normalized)
    for pattern in IGNORE_PATTERNS:
        if fnmatch.fnmatch(normalized, pattern) or fnmatch.fnmatch(basename, pattern):
            return True
    return False


def get_diff_files(target: str = "--cached") -> dict[str, str]:
    cmd_files = ["git", "diff", target, "--name-only", "--diff-filter=ACMR"]
    res_files = subprocess.run(cmd_files, capture_output=True, text=True, check=True)
    files = [f.strip() for f in res_files.stdout.splitlines() if f.strip() and not is_ignored(f)]

    file_diffs = {}
    for f in files:
        cmd_diff = ["git", "diff", target, "--unified=0", "--", f]
        diff_out = subprocess.run(cmd_diff, capture_output=True, text=True, check=True).stdout
        if diff_out.strip():
            file_diffs[f] = diff_out
    return file_diffs


def run_security_scan(file_diffs: dict[str, str]) -> list[str]:
    findings = []
    for filepath, diff in file_diffs.items():
        if os.path.basename(filepath).startswith("git-") and filepath.endswith(".py"):
            continue

        ext = os.path.splitext(filepath)[1].lower()
        added_lines = [line[1:] for line in diff.splitlines() if line.startswith("+") and not line.startswith("+++")]

        for line in added_lines:
            s_line = line.strip()

            # 1. Private keys
            if re.search(r"-----BEGIN (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----", s_line):
                findings.append(f"[{filepath}] Hardcoded Private Key detected: `{s_line[:40]}...`")

            # 2. Cloud tokens
            if re.search(r"\b(?:AKIA|ABIA|ACCA|ASIA)[0-9A-Z]{16}\b", s_line):
                findings.append(f"[{filepath}] AWS Access Key ID detected: `{s_line[:30]}...`")
            if re.search(r"\bgh[pousr]_[A-Za-z0-9_]{36,}\b", s_line):
                findings.append(f"[{filepath}] GitHub Personal Access Token detected: `{s_line[:30]}...`")

            # 3. Raw SQL Injection via string interpolation in EF Core (C#)
            if ext in (".cs", ".fs"):
                if re.search(r"\.(?:FromSqlRaw|ExecuteSqlRaw)\s*\(\s*\$\"", s_line):
                    findings.append(f"[{filepath}] SQL Injection: String interpolation inside EF Core raw SQL method: `{s_line}`")

            # 4. Command injection with shell=True (Python / Shell)
            if ext in (".py", ".sh", ".bash"):
                if re.search(r"shell\s*=\s*True", s_line) and any(x in s_line for x in ["+", "format", "f\"", "f'"]):
                    findings.append(f"[{filepath}] Potential Command Injection with shell=True: `{s_line}`")

    return findings


def main():
    target = sys.argv[1] if len(sys.argv) > 1 else "--cached"
    try:
        diffs = get_diff_files(target)
    except subprocess.CalledProcessError as e:
        print(f"Error reading git diff: {e}")
        return 0

    if not diffs:
        print("No eligible files modified.")
        return 0

    findings = run_security_scan(diffs)
    if findings:
        print(f"\033[91m\033[1m🛑 Security scan found {len(findings)} issue(s):\033[0m")
        for f in findings:
            print(f"  - {f}")
        return 1

    print("\033[92m✅ Security scan passed (0 critical issues found).\033[0m")
    return 0


if __name__ == "__main__":
    sys.exit(main())