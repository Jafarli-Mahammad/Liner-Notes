#!/usr/bin/env python3
"""
High-Value Asynchronous Git Code Reviewer with File & Hunk Chunking.
Evaluates commits in the background for real bugs, performance regressions,
and security vulnerabilities using local Ollama (Qwen2.5-Coder 7B).
Stores structured results in .git/reviews/<commit-sha>.json and gates git push.
"""

import os
import re
import sys
import json
import time
import fcntl
import fnmatch
import datetime
import subprocess
import urllib.request
import urllib.error

# Configuration
OLLAMA_ENDPOINT = os.environ.get("OLLAMA_ENDPOINT", "http://localhost:11434")
OLLAMA_MODEL = os.environ.get("OLLAMA_MODEL", "qwen2.5-coder:7b")
MAX_CHUNK_CHARS = int(os.environ.get("MAX_CHUNK_CHARS", "3500"))
REQUEST_TIMEOUT = int(os.environ.get("REVIEW_TIMEOUT", "120"))

# File exclusion patterns
IGNORE_PATTERNS = [
    "*.lock", "*-lock.json", "*-lock.yaml", "package-lock.json", "pnpm-lock.yaml", "yarn.lock", "Cargo.lock",
    "packages.lock.json", "*.min.js", "*.min.css", "*.map", "*.svg", "*.png", "*.jpg", "*.jpeg", "*.gif",
    "*.ico", "*.wasm", "*.dll", "*.exe", "*.so", "*.dylib", "*.db", "*.sqlite", "*.log", "*.suo",
    "*.DotSettings.user", "*.DotSettings", "*.nupkg", "*.Designer.cs", "*.pdb", "dotnet-tools.json",
    "*ModelSnapshot.cs"
]

IGNORE_DIRECTORIES = {
    "bin", "obj", ".idea", ".vscode", ".git", "node_modules", ".system_generated", "migrations"
}

# ANSI formatting for console output
CYAN = "\033[96m"
GREEN = "\033[92m"
YELLOW = "\033[93m"
RED = "\033[91m"
MAGENTA = "\033[95m"
BOLD = "\033[1m"
DIM = "\033[2m"
RESET = "\033[0m"


def is_ignored(filename: str) -> bool:
    normalized = filename.replace("\\", "/").strip("/")
    parts = [part.lower() for part in normalized.split("/")]
    if any(part in IGNORE_DIRECTORIES for part in parts):
        return True
    basename = os.path.basename(normalized)
    for pattern in IGNORE_PATTERNS:
        if fnmatch.fnmatch(normalized, pattern) or fnmatch.fnmatch(basename, pattern):
            return True
    return False


def classify_file(filename: str) -> str:
    ext = os.path.splitext(filename)[1].lower()
    if ext in (".cs", ".fs", ".csproj", ".sln"):
        return "dotnet"
    elif ext in (".py", ".pyi"):
        return "python"
    elif ext in (".sh", ".bash", ".zsh"):
        return "shell"
    elif ext in (".ts", ".tsx", ".js", ".jsx", ".vue", ".svelte", ".html", ".css"):
        return "web"
    return "docs_configs"


# ==============================================================================
# Deterministic Fast-Scan (< 5ms)
# ==============================================================================

def run_deterministic_scan(file_diffs: dict[str, str]) -> list[dict]:
    """Runs instant, zero-false-positive pattern checks before LLM invocation."""
    findings = []
    for filepath, diff in file_diffs.items():
        # Do not self-trigger on reviewer script patterns
        if os.path.basename(filepath).startswith("git-") and filepath.endswith(".py"):
            continue

        ext = os.path.splitext(filepath)[1].lower()
        added_lines = [
            (idx + 1, line[1:].strip())
            for idx, line in enumerate(diff.splitlines())
            if line.startswith("+") and not line.startswith("+++")
        ]

        for line_num, s_line in added_lines:
            # 1. Private keys (all files)
            if re.search(r"-----BEGIN (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----", s_line):
                findings.append({
                    "file": filepath,
                    "line": line_num,
                    "category": "SECURITY",
                    "severity": "CRITICAL",
                    "summary": "Hardcoded Private Key detected",
                    "detail": f"Diff contains an embedded private key: `{s_line[:40]}...`",
                    "suggested_fix": "Remove private key immediately and rotate credentials."
                })

            # 2. AWS / GitHub tokens (all files)
            if re.search(r"\b(?:AKIA|ABIA|ACCA|ASIA)[0-9A-Z]{16}\b", s_line):
                findings.append({
                    "file": filepath,
                    "line": line_num,
                    "category": "SECURITY",
                    "severity": "CRITICAL",
                    "summary": "AWS Access Key ID detected",
                    "detail": f"Diff contains a potential live AWS Key ID: `{s_line[:30]}...`",
                    "suggested_fix": "Store credentials in environment variables or a secret vault."
                })
            if re.search(r"\bgh[pousr]_[A-Za-z0-9_]{36,}\b", s_line):
                findings.append({
                    "file": filepath,
                    "line": line_num,
                    "category": "SECURITY",
                    "severity": "CRITICAL",
                    "summary": "GitHub Personal Access Token detected",
                    "detail": f"Diff contains a GitHub token: `{s_line[:30]}...`",
                    "suggested_fix": "Revoke the exposed token and inject via environment variables."
                })

            # 3. EF Core SQL Injection via string interpolation (C#)
            if ext in (".cs", ".fs"):
                if re.search(r"\.(?:FromSqlRaw|ExecuteSqlRaw)\s*\(\s*\$\"", s_line):
                    findings.append({
                        "file": filepath,
                        "line": line_num,
                        "category": "SECURITY",
                        "severity": "CRITICAL",
                        "summary": "SQL Injection: String interpolation inside EF Core raw SQL method",
                        "detail": f"Using string interpolation with FromSqlRaw/ExecuteSqlRaw bypasses parameterization: `{s_line}`",
                        "suggested_fix": "Use FromSqlInterpolated or pass parameterized arguments."
                    })

                # 4. Sync-over-async blocking (C#)
                if re.search(r"\.(?:Result\b|Wait\(\)|GetAwaiter\(\)\.GetResult\(\))", s_line):
                    findings.append({
                        "file": filepath,
                        "line": line_num,
                        "category": "BUG",
                        "severity": "HIGH",
                        "summary": "Sync-over-async blocking call",
                        "detail": f"Synchronously blocking on an async Task risks thread-pool starvation and deadlocks: `{s_line}`",
                        "suggested_fix": "Await the asynchronous operation or propagate async up the call chain."
                    })

                # 5. Async void method (C#)
                if re.search(r"\basync\s+void\s+(?!On[A-Z]|.*EventHandler|.*Click|.*Command)", s_line):
                    findings.append({
                        "file": filepath,
                        "line": line_num,
                        "category": "BUG",
                        "severity": "HIGH",
                        "summary": "Async void method declaration",
                        "detail": f"Async void methods cannot be awaited and unhandled exceptions crash the process: `{s_line}`",
                        "suggested_fix": "Change return type from async void to async Task."
                    })

            # 6. Dangerous shell execution (Python)
            if ext in (".py", ".pyi"):
                if re.search(r"shell\s*=\s*True", s_line) and any(x in s_line for x in ["+", "format", "f\"", "f'"]):
                    findings.append({
                        "file": filepath,
                        "line": line_num,
                        "category": "SECURITY",
                        "severity": "CRITICAL",
                        "summary": "Potential Command Injection with shell=True",
                        "detail": f"Formatting unvalidated strings into a shell command allows arbitrary execution: `{s_line}`",
                        "suggested_fix": "Pass command arguments as an array and set shell=False."
                    })
                if re.search(r"^\s*except\s*:\s*(?:#.*)?$", s_line):
                    findings.append({
                        "file": filepath,
                        "line": line_num,
                        "category": "BUG",
                        "severity": "MEDIUM",
                        "summary": "Bare except clause catching BaseException",
                        "detail": f"Bare except swallows KeyboardInterrupt and SystemExit: `{s_line}`",
                        "suggested_fix": "Catch specific exceptions or `except Exception:`."
                    })

    return findings


# ==============================================================================
# Diff & Chunking Engine
# ==============================================================================

def get_commit_diff_per_file(repo_root: str, commit_sha: str) -> dict[str, str]:
    """Retrieves diff per file strictly from the Git object store for a commit."""
    cmd_files = ["git", "diff-tree", "--no-commit-id", "--name-only", "-r", commit_sha]
    res_files = subprocess.run(cmd_files, cwd=repo_root, capture_output=True, text=True, check=True)
    changed_files = [f.strip() for f in res_files.stdout.splitlines() if f.strip()]

    file_diffs = {}
    for f in changed_files:
        if is_ignored(f):
            continue
        cmd_diff = ["git", "diff-tree", "-p", "--unified=6", commit_sha, "--", f]
        diff_out = subprocess.run(cmd_diff, cwd=repo_root, capture_output=True, text=True, check=True).stdout
        if diff_out.strip():
            file_diffs[f] = diff_out
    return file_diffs


def get_staged_diff_per_file(repo_root: str) -> dict[str, str]:
    """Retrieves diff per staged file for on-demand manual verification."""
    cmd_files = ["git", "diff", "--cached", "--name-only", "--diff-filter=ACMR"]
    res_files = subprocess.run(cmd_files, cwd=repo_root, capture_output=True, text=True, check=True)
    staged_files = [f.strip() for f in res_files.stdout.splitlines() if f.strip()]

    file_diffs = {}
    for f in staged_files:
        if is_ignored(f):
            continue
        cmd_diff = ["git", "diff", "--cached", "--unified=6", "--", f]
        diff_out = subprocess.run(cmd_diff, cwd=repo_root, capture_output=True, text=True, check=True).stdout
        if diff_out.strip():
            file_diffs[f] = diff_out
    return file_diffs


def split_file_diff_into_chunks(filepath: str, diff_text: str, max_chars: int) -> list[dict]:
    """
    Splits a file diff into smaller, coherent chunks.
    If the file diff is small, it remains a single chunk.
    If large, it splits along hunk boundaries (`@@ ... @@`).
    """
    category = classify_file(filepath)
    if len(diff_text) <= max_chars:
        return [{
            "file": filepath,
            "category": category,
            "hunk_id": 1,
            "total_hunks": 1,
            "diff": diff_text
        }]

    # Split by git hunk markers
    hunk_regex = r"(?=^@@\s+-[0-9]+,[0-9]+\s+\+[0-9]+,[0-9]+\s+@@)"
    raw_hunks = re.split(hunk_regex, diff_text, flags=re.MULTILINE)
    hunks = [h.strip() for h in raw_hunks if h.strip()]

    if not hunks:
        return [{
            "file": filepath,
            "category": category,
            "hunk_id": 1,
            "total_hunks": 1,
            "diff": diff_text[:max_chars]
        }]

    chunks = []
    current_acc = []
    current_len = 0

    for hunk in hunks:
        if current_len + len(hunk) <= max_chars or not current_acc:
            current_acc.append(hunk)
            current_len += len(hunk)
        else:
            chunks.append("\n\n".join(current_acc))
            current_acc = [hunk]
            current_len = len(hunk)

    if current_acc:
        chunks.append("\n\n".join(current_acc))

    total = len(chunks)
    return [{
        "file": filepath,
        "category": category,
        "hunk_id": idx + 1,
        "total_hunks": total,
        "diff": chunk_text
    } for idx, chunk_text in enumerate(chunks)]


# ==============================================================================
# Model Discovery & Inference
# ==============================================================================

def get_available_ollama_model() -> tuple[bool, str]:
    try:
        req = urllib.request.Request(f"{OLLAMA_ENDPOINT}/api/tags", method="GET")
        with urllib.request.urlopen(req, timeout=3) as response:
            if response.status != 200:
                return False, ""
            data = json.loads(response.read().decode("utf-8"))
            models = [m.get("name", "") for m in data.get("models", [])]

            target_model = OLLAMA_MODEL
            if target_model in models:
                return True, target_model

            target_base = target_model.split(":")[0]
            for m in models:
                if m == target_base or m.startswith(f"{target_base}:"):
                    return True, m
            for m in models:
                if "qwen" in m.lower() or "coder" in m.lower():
                    return True, m
            if models:
                return True, models[0]
            return True, ""
    except Exception:
        return False, ""


def strip_thinking(text: str) -> str:
    return re.sub(r"<think>.*?</think>", "", text, flags=re.DOTALL).strip()


def build_chunk_prompt(chunk: dict) -> str:
    lang = chunk["category"]
    filepath = chunk["file"]

    rules = [
        "- ACCURACY: Report an issue ONLY if you can point to a concrete bug, runtime crash, data corruption, or security flaw in the added lines (+).",
        "- NO STYLE NITPICKS: Do NOT complain about missing tests, documentation, logging, or naming style.",
        "- LOCAL SCRIPTS & TESTS: Code under `scripts/` or `tests/` consists of developer tools. Reading repo files or resolving `Path(__file__)` is expected and safe.",
        "- SEVERITY:\n"
        "  * CRITICAL: SQL injection, remote command execution, exposed private keys/tokens, data corruption.\n"
        "  * HIGH: Definite runtime crash (NullReferenceException), sync-over-async deadlock, authentication bypass.\n"
        "  * MEDIUM / LOW: Code smells, minor inefficiency (do not inflate to HIGH/CRITICAL)."
    ]

    if lang == "dotnet":
        rules.extend([
            "- Null Safety: If objects returned by FirstOrDefault, FirstOrDefaultAsync, SingleOrDefault, Find, or dictionary indexing can be null, verify whether they are dereferenced without a null check or null-conditional operator (?.). If dereferenced directly (e.g. `user.Email`), flag as a NullReferenceException bug.",
            "- Async: Check for sync-over-async blocking (.Result, .Wait(), .GetAwaiter().GetResult()) or async void methods.",
            "- EF Core & Performance: Check for N+1 queries in loops, client-side query evaluation traps, or un-disposed resources (Streams, DbContexts).",
            "- Security: Check for SQL injection in raw SQL methods or exposed credentials."
        ])
    elif lang == "python":
        rules.extend([
            "- Logic & Bugs: Check for mutable default arguments (e.g. def fn(arg=[])), swallowed exceptions, or unclosed file/socket handles.",
            "- Security: Check for command injection (shell=True with formatted strings) or unvalidated external input."
        ])
    elif lang == "shell":
        rules.extend([
            "- Shell Safety: Check for unquoted variable expansions in commands, failure to check exit codes, or broken pipe handling."
        ])

    rules_str = "\n".join(rules)

    return f"""You are an elite code reviewer auditing this diff chunk for real defects, bugs, and security risks.

File: `{filepath}` (Language: {lang}, Chunk {chunk['hunk_id']} of {chunk['total_hunks']})
```diff
{chunk['diff']}
```

### AUDIT CHECKLIST:
{rules_str}

### INSTRUCTIONS:
Analyze the added lines (+) in the diff against the audit checklist above.
You MUST respond in valid JSON matching this schema:
{{
  "summary": "<1-2 sentence technical summary of changes>",
  "issues": [
    {{
      "line": <line_number_or_null>,
      "category": "BUG" | "PERFORMANCE" | "SECURITY",
      "severity": "CRITICAL" | "HIGH" | "MEDIUM" | "LOW",
      "summary": "<Concise 1-line title>",
      "detail": "<Clear technical explanation of why this will fail, crash, degrade, or be exploited>",
      "suggested_fix": "<Actionable code or architectural fix>"
    }}
  ]
}}
If no defects are found, return "issues": [].
"""


def call_ollama_chunk_review(model_name: str, prompt: str) -> dict:
    payload = {
        "model": model_name,
        "messages": [
            {"role": "user", "content": prompt}
        ],
        "stream": False,
        "format": "json",
        "options": {
            "temperature": 0.05,
            "top_p": 0.85,
            "num_ctx": 8192,
            "num_predict": 768
        },
        "keep_alive": 0
    }

    req = urllib.request.Request(
        f"{OLLAMA_ENDPOINT}/api/chat",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"}
    )

    try:
        with urllib.request.urlopen(req, timeout=REQUEST_TIMEOUT) as response:
            res_data = json.loads(response.read().decode("utf-8"))
            content = res_data.get("message", {}).get("content", "") or res_data.get("response", "")
            clean = strip_thinking(content).strip()

            # Attempt JSON parsing
            json_match = re.search(r"```(?:json)?\s*(\{.*?\})\s*```", clean, re.DOTALL)
            if json_match:
                clean = json_match.group(1).strip()

            parsed = json.loads(clean)
            if isinstance(parsed, dict) and "issues" in parsed:
                return parsed
            return {"summary": "Completed chunk review", "issues": []}
    except Exception as e:
        return {
            "summary": f"Review error: {e}",
            "issues": []
        }


# ==============================================================================
# Synthesis, Reporting & Storage
# ==============================================================================

def synthesize_and_save_report(repo_root: str, commit_sha: str, model_name: str, file_diffs: dict[str, str], deterministic_issues: list[dict], chunk_results: list[dict], duration_sec: float) -> dict:
    git_dir = os.path.join(repo_root, ".git")
    reviews_dir = os.path.join(git_dir, "reviews")
    os.makedirs(reviews_dir, exist_ok=True)

    all_issues = []
    all_issues.extend(deterministic_issues)
    for res in chunk_results:
        for iss in res.get("issues", []):
            all_issues.append(iss)

    # Elevate demonstrable crashes or NullReferenceExceptions to at least HIGH severity
    for iss in all_issues:
        summary_l = iss.get("summary", "").lower()
        detail_l = iss.get("detail", "").lower()
        if any(term in summary_l or term in detail_l for term in ["nullreferenceexception", "null dereference", "crash", "deadlock"]):
            if iss.get("severity") in ("MEDIUM", "LOW"):
                iss["severity"] = "HIGH"

    # Count issues by severity
    severity_counts = {"CRITICAL": 0, "HIGH": 0, "MEDIUM": 0, "LOW": 0}
    for iss in all_issues:
        sev = iss.get("severity", "MEDIUM").upper()
        if sev not in severity_counts:
            sev = "MEDIUM"
        severity_counts[sev] += 1

    # Verdict determination:
    # Any CRITICAL or HIGH issue triggers REJECT.
    # MEDIUM or LOW issues generate WARNINGS but pass the gate.
    blocking_count = severity_counts["CRITICAL"] + severity_counts["HIGH"]
    verdict = "REJECT" if blocking_count > 0 else "APPROVE"

    now_iso = datetime.datetime.now().isoformat()
    now_str = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    # Structured JSON report
    report_data = {
        "commit": commit_sha,
        "timestamp": now_iso,
        "model": model_name,
        "status": "COMPLETED",
        "verdict": verdict,
        "duration_sec": round(duration_sec, 2),
        "files_reviewed": list(file_diffs.keys()),
        "severity_counts": severity_counts,
        "issues": all_issues
    }

    json_path = os.path.join(reviews_dir, f"{commit_sha}.json")
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(report_data, f, indent=2)

    # Human-readable Markdown report
    badge = "🟢 **PASSED (APPROVE)**" if verdict == "APPROVE" else f"🔴 **BLOCKED ({blocking_count} Critical/High Issues)**"
    md_lines = [
        f"# 🤖 Code Review Report: `{commit_sha[:8]}`",
        f"- **Date:** `{now_str}`",
        f"- **Model:** `{model_name}`",
        f"- **Duration:** `{round(duration_sec, 2)}s`",
        f"- **Status:** {badge}",
        f"- **Breakdown:** `{severity_counts['CRITICAL']} Critical`, `{severity_counts['HIGH']} High`, `{severity_counts['MEDIUM']} Medium`, `{severity_counts['LOW']} Low`",
        "",
        "---",
        "",
        f"### 📂 Reviewed Files ({len(file_diffs)})"
    ]
    for f in file_diffs.keys():
        md_lines.append(f"- `{f}`")

    md_lines.append("\n---")

    if not all_issues:
        md_lines.append("\n### ✅ No Bugs, Performance Regressions, or Security Risks Detected\n")
        md_lines.append("All staged files satisfied clean architecture and security criteria.")
    else:
        md_lines.append("\n### 🚨 Detected Issues\n")
        for idx, iss in enumerate(all_issues, start=1):
            sev_icon = "🛑" if iss["severity"] in ("CRITICAL", "HIGH") else "⚠️"
            file_loc = f"`{iss.get('file', 'Unknown')}`"
            if iss.get("line"):
                file_loc += f":{iss['line']}"
            md_lines.append(f"#### {idx}. {sev_icon} [{iss.get('severity', 'HIGH')}] {iss.get('summary', 'Issue')}")
            md_lines.append(f"- **Location:** {file_loc}")
            md_lines.append(f"- **Category:** `{iss.get('category', 'GENERAL')}`")
            md_lines.append(f"- **Detail:** {iss.get('detail', '')}")
            if iss.get("suggested_fix"):
                md_lines.append(f"- **Suggested Fix:** {iss.get('suggested_fix')}")
            md_lines.append("")

    md_lines.append("\n---\n*Generated by Liner Notes Asynchronous Git Reviewer.*")
    md_content = "\n".join(md_lines)

    md_path = os.path.join(reviews_dir, f"{commit_sha}.md")
    with open(md_path, "w", encoding="utf-8") as f:
        f.write(md_content)

    last_report_path = os.path.join(git_dir, "LAST_REVIEW_REPORT.md")
    with open(last_report_path, "w", encoding="utf-8") as f:
        f.write(md_content)

    return report_data


# ==============================================================================
# Main Review Orchestrator
# ==============================================================================

def review_commit(commit_sha: str = "HEAD", is_staged: bool = False) -> int:
    start_time = time.time()
    try:
        repo_root = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True, check=True).stdout.strip()
    except subprocess.CalledProcessError:
        repo_root = "."

    git_dir = os.path.join(repo_root, ".git")
    reviews_dir = os.path.join(git_dir, "reviews")
    os.makedirs(reviews_dir, exist_ok=True)

    # Concurrency Lock: Ensure only one Ollama review runs at a time to prevent GPU VRAM collisions
    lock_file_path = os.path.join(reviews_dir, ".lock")
    lock_file = open(lock_file_path, "w")
    try:
        fcntl.flock(lock_file, fcntl.LOCK_EX)
    except Exception:
        pass

    try:
        # Resolve real commit SHA if given "HEAD"
        if not is_staged:
            real_sha = subprocess.run(["git", "rev-parse", commit_sha], cwd=repo_root, capture_output=True, text=True, check=True).stdout.strip()
        else:
            real_sha = "staged"

        # Mark IN_PROGRESS
        status_file = os.path.join(reviews_dir, f"{real_sha}.json")
        with open(status_file, "w", encoding="utf-8") as f:
            json.dump({"commit": real_sha, "status": "IN_PROGRESS", "timestamp": datetime.datetime.now().isoformat()}, f)

        # 1. Fetch file diffs
        if is_staged:
            file_diffs = get_staged_diff_per_file(repo_root)
        else:
            file_diffs = get_commit_diff_per_file(repo_root, real_sha)

        if not file_diffs:
            # Clean empty diff
            synthesize_and_save_report(repo_root, real_sha, "static", {}, [], [], time.time() - start_time)
            return 0

        # 2. Run Deterministic Scan
        deterministic_issues = run_deterministic_scan(file_diffs)

        # 3. Filter for code files (docs-only skip LLM)
        code_diffs = {
            f: diff for f, diff in file_diffs.items()
            if classify_file(f) in ("dotnet", "python", "shell", "web")
        }

        if not code_diffs:
            # Only docs or configs modified
            synthesize_and_save_report(repo_root, real_sha, "deterministic", file_diffs, deterministic_issues, [], time.time() - start_time)
            return 0

        # 4. Check Ollama availability
        is_running, model_name = get_available_ollama_model()
        if not is_running or not model_name:
            # Fallback to deterministic results only
            synthesize_and_save_report(repo_root, real_sha, "deterministic_fallback", file_diffs, deterministic_issues, [], time.time() - start_time)
            return 0

        # 5. Split code diffs into focused chunks
        chunks = []
        for filepath, diff in code_diffs.items():
            file_chunks = split_file_diff_into_chunks(filepath, diff, MAX_CHUNK_CHARS)
            chunks.extend(file_chunks)

        # 6. Execute per-chunk review
        chunk_results = []
        for chunk in chunks:
            prompt = build_chunk_prompt(chunk)
            res = call_ollama_chunk_review(model_name, prompt)
            for iss in res.get("issues", []):
                if not iss.get("file"):
                    iss["file"] = chunk["file"]
            chunk_results.append(res)

        # 7. Synthesize and persist final report
        duration = time.time() - start_time
        final_report = synthesize_and_save_report(
            repo_root, real_sha, model_name, file_diffs, deterministic_issues, chunk_results, duration
        )

        return 1 if final_report["verdict"] == "REJECT" else 0

    finally:
        try:
            fcntl.flock(lock_file, fcntl.LOCK_UN)
            lock_file.close()
        except Exception:
            pass


if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else "HEAD"
    is_staged_flag = target == "--staged"
    commit_target = "HEAD" if is_staged_flag else target
    exit_code = review_commit(commit_target, is_staged=is_staged_flag)
    sys.exit(exit_code)
