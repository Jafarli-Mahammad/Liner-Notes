#!/usr/bin/env python3
"""
AI Pre-Commit QA Reviewer powered by local Ollama (Qwen2.5-Coder 7B).
Optimized specifically for high-signal code quality, domain-aware grounding,
deterministic anti-pattern detection, and zero-fluff gatekeeping.
"""

import os
import re
import sys
import json
import fnmatch
import datetime
import subprocess
import urllib.request
import urllib.error

# Configuration
OLLAMA_ENDPOINT = os.environ.get("OLLAMA_ENDPOINT", "http://localhost:11434")
OLLAMA_MODEL = os.environ.get("OLLAMA_MODEL", "qwen2.5-coder:7b")
MAX_DIFF_CHARS = int(os.environ.get("MAX_DIFF_CHARS", "20000"))

# File exclusion patterns
IGNORE_PATTERNS = [
    "*.lock", "*-lock.json", "*-lock.yaml", "package-lock.json", "pnpm-lock.yaml", "yarn.lock", "Cargo.lock",
    "packages.lock.json", "*.min.js", "*.min.css", "*.map", "*.svg", "*.png", "*.jpg", "*.jpeg", "*.gif",
    "*.ico", "*.wasm", "*.dll", "*.exe", "*.so", "*.dylib", "*.db", "*.sqlite", "*.log", "*.suo",
    "*.DotSettings.user", "*.DotSettings", ".idea/*", ".vscode/*", "zap-report.html", "codebase-memory.db",
    "*.nupkg", "obj/*", "bin/*"
]

# ANSI Colors
CYAN = "\033[96m"
GREEN = "\033[92m"
YELLOW = "\033[93m"
RED = "\033[91m"
BOLD = "\033[1m"
DIM = "\033[2m"
RESET = "\033[0m"


def is_ignored(filename: str) -> bool:
    basename = os.path.basename(filename)
    for pattern in IGNORE_PATTERNS:
        if fnmatch.fnmatch(filename, pattern) or fnmatch.fnmatch(basename, pattern):
            return True
    return False


def classify_files(files: list[str]) -> dict[str, list[str]]:
    categories = {
        "dotnet": [],
        "python": [],
        "shell": [],
        "web": [],
        "docs_configs": []
    }
    for f in files:
        ext = os.path.splitext(f)[1].lower()
        if ext in (".cs", ".fs", ".csproj", ".sln"):
            categories["dotnet"].append(f)
        elif ext in (".py", ".pyi"):
            categories["python"].append(f)
        elif ext in (".sh", ".bash", ".zsh"):
            categories["shell"].append(f)
        elif ext in (".ts", ".tsx", ".js", ".jsx", ".vue", ".svelte", ".html", ".css"):
            categories["web"].append(f)
        else:
            categories["docs_configs"].append(f)
    return categories


def run_deterministic_qa_checks(file_diffs: dict[str, str]) -> list[str]:
    """Runs instant (sub-millisecond) pattern checks only on relevant language files."""
    findings = []
    for filepath, diff in file_diffs.items():
        ext = os.path.splitext(filepath)[1].lower()
        added_lines = [line[1:] for line in diff.splitlines() if line.startswith("+") and not line.startswith("+++")]

        for line in added_lines:
            s_line = line.strip()
            # C# checks
            if ext in (".cs", ".fs"):
                if re.search(r"\.(?:Result\b|Wait\(\)|GetAwaiter\(\)\.GetResult\(\))", s_line):
                    findings.append(f"[{filepath}] Potential sync-over-async blocking call: `{s_line}`")
                if re.search(r"\basync\s+void\s+(?!On[A-Z]|.*EventHandler|.*Click|.*Command)", s_line):
                    findings.append(f"[{filepath}] Potential async void method: `{s_line}`")

            # Python checks
            elif ext in (".py", ".pyi"):
                if re.search(r"^\s*except\s*:\s*(?:#.*)?$", s_line):
                    findings.append(f"[{filepath}] Bare except clause catching BaseException: `{s_line}`")

    return findings


def get_available_ollama_model() -> tuple[bool, str, list[str]]:
    """
    Checks if Ollama is running and returns (is_running, resolved_model_name, available_models).
    """
    try:
        req = urllib.request.Request(f"{OLLAMA_ENDPOINT}/api/tags", method="GET")
        with urllib.request.urlopen(req, timeout=3) as response:
            if response.status != 200:
                print(f"{DIM}[QA Hook] Ollama returned HTTP status {response.status}. Skipping AI check.{RESET}")
                return False, "", []
            data = json.loads(response.read().decode("utf-8"))
            models = [m.get("name", "") for m in data.get("models", [])]

            target_model = OLLAMA_MODEL
            if target_model in models:
                return True, target_model, models

            target_base = target_model.split(":")[0]
            for m in models:
                if m == target_base or m.startswith(f"{target_base}:"):
                    return True, m, models

            for m in models:
                if "qwen" in m.lower() or "coder" in m.lower():
                    return True, m, models

            return True, "", models
    except urllib.error.URLError as e:
        print(f"{DIM}[QA Hook] Ollama connection error ({e.reason}) at {OLLAMA_ENDPOINT}. Skipping AI check.{RESET}")
        return False, "", []
    except json.JSONDecodeError as e:
        print(f"{DIM}[QA Hook] Failed to parse Ollama tags response as JSON: {e}. Skipping AI check.{RESET}")
        return False, "", []
    except Exception as e:
        print(f"{DIM}[QA Hook] Unexpected error discovering Ollama models: {e}. Skipping AI check.{RESET}")
        return False, "", []


def get_git_info() -> tuple[str, str, str]:
    try:
        root = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True, check=True).stdout.strip()
        branch = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()
        author = subprocess.run(["git", "config", "user.name"], capture_output=True, text=True).stdout.strip() or "Developer"
        return root, branch, author
    except (subprocess.CalledProcessError, FileNotFoundError, OSError):
        return ".", "unknown", "Developer"


def get_staged_diff_per_file() -> dict[str, str]:
    cmd_files = ["git", "diff", "--cached", "--name-only", "--diff-filter=ACMR"]
    res_files = subprocess.run(cmd_files, capture_output=True, text=True, check=True)
    staged_files = [f.strip() for f in res_files.stdout.splitlines() if f.strip()]

    candidate_files = [f for f in staged_files if not is_ignored(f)]

    file_diffs = {}
    for f in candidate_files:
        cmd_diff = ["git", "diff", "--cached", "--unified=6", "--", f]
        diff_out = subprocess.run(cmd_diff, capture_output=True, text=True, check=True).stdout
        if diff_out.strip():
            file_diffs[f] = diff_out
    return file_diffs


def build_clean_diff_text(file_diffs: dict[str, str], max_chars: int) -> tuple[str, bool]:
    """Assembles diff text by whole files to prevent mid-line or broken syntax truncation."""
    accumulated = []
    current_len = 0
    truncated = False

    for filepath, diff in file_diffs.items():
        if current_len + len(diff) <= max_chars or not accumulated:
            accumulated.append(diff)
            current_len += len(diff)
        else:
            truncated = True
            accumulated.append(f"\n... [Diff for `{filepath}` omitted: pre-commit max char limit reached] ...\n")

    return "\n".join(accumulated), truncated


def strip_thinking(text: str) -> str:
    return re.sub(r"<think>.*?</think>", "", text, flags=re.DOTALL).strip()


def build_system_prompt(categories: dict[str, list[str]], deterministic_findings: list[str]) -> str:
    langs = []
    rules = []

    if categories["dotnet"]:
        langs.append("C# / .NET (Clean Architecture, MediatR, EF Core)")
        rules.append(
            "- C# / .NET:\n"
            "  * Async: Detect sync-over-async (.Result, .Wait()), missing await on Task returns, or async void.\n"
            "  * EF Core & CQRS: Detect N+1 queries in loops, missing AsNoTracking() on read-only queries, or mutating state in MediatR query handlers.\n"
            "  * Null safety: Detect unguarded null dereferences on reference types."
        )

    if categories["python"]:
        langs.append("Python")
        rules.append(
            "- Python:\n"
            "  * Detect swallowed exceptions, mutable default args, or unclosed file/stream handles.\n"
            "  * Detect failure to check subprocess return codes or unhandled JSON decoding."
        )

    if categories["shell"]:
        langs.append("Shell / Bash")
        rules.append(
            "- Shell:\n"
            "  * Detect unquoted variable expansions in commands, failure to handle exit codes, or broken pipe handling."
        )

    if categories["web"]:
        langs.append("Frontend / TypeScript / JavaScript")
        rules.append(
            "- Frontend:\n"
            "  * Detect unhandled Promise rejections, memory leaks (un-cleaned listeners/intervals), or state mutation bugs."
        )

    stack_str = ", ".join(langs) if langs else "General Code"
    rules_str = "\n".join(rules) if rules else "- Focus on syntax, unhandled error cases, and logic flow."

    findings_block = ""
    if deterministic_findings:
        findings_block = (
            "\n### ⚠️ AUTOMATED PRE-SCAN FINDINGS (Verify these carefully):\n" +
            "\n".join(f"- {finding}" for finding in deterministic_findings) +
            "\n"
        )

    return (
        f"You are an elite Principal Software Engineer acting as a strict Git pre-commit QA gatekeeper.\n"
        f"Staged Technology Stack: {stack_str}\n\n"
        "### STRICT GROUNDING & ANTI-FLUFF RULES:\n"
        "1. GROUNDING: Evaluate ONLY the code visible in the diff and surrounding context. Never speculate on unseen code or dependencies.\n"
        "2. FORBIDDEN GENERIC ADVICE: Do NOT suggest 'add unit tests', 'consider logging', 'check race conditions', or 'refactor for maintainability'. Every reported issue MUST cite a concrete, demonstrable defect in the diff.\n"
        "3. SILENCE ON CLEAN CODE: If there are zero critical logic bugs, memory leaks, or performance bottlenecks, you MUST output '- None identified' and APPROVE.\n"
        "4. DO NOT OUTPUT RAW JSON: You must format your response strictly using the Markdown headers below.\n\n"
        "### DOMAIN CRITERIA:\n"
        f"{rules_str}\n"
        f"{findings_block}\n"
        "### OUTPUT FORMAT (Follow exactly):\n\n"
        "### 🔍 Summary\n"
        "[1-2 crisp sentences describing the architectural or logic changes]\n\n"
        "### ⚙️ Analysis\n"
        "[Technical evaluation of the code against the criteria]\n\n"
        "### 🚀 QA & Performance Issues\n"
        "- [Actionable defect with code/line reference, or '- None identified']\n\n"
        "### 💡 Actionable Improvement\n"
        "- [One concrete technical improvement directly applicable to this diff, or '- None']\n\n"
        "### 🎯 Verdict\n"
        "[VERDICT: APPROVE] or [VERDICT: REJECT]"
    )


def parse_verdict(response_text: str) -> str:
    """
    Robustly parses verdict. Fails closed (REJECT) if rejections or unresolved issues are found.
    Handles JSON responses, raw markdown, and token patterns.
    """
    clean = strip_thinking(response_text).strip()
    if not clean:
        return "REJECT"

    # 1. Handle JSON response fallback (e.g. {"response": "REJECT"})
    json_candidate = clean
    json_match = re.search(r"```(?:json)?\s*(\{.*?\})\s*```", clean, re.DOTALL)
    if json_match:
        json_candidate = json_match.group(1).strip()
    elif clean.startswith("{") and clean.endswith("}"):
        json_candidate = clean

    if json_candidate.startswith("{"):
        try:
            data = json.loads(json_candidate)
            val = str(data.get("response", "") or data.get("verdict", "")).strip().upper()
            if "REJECT" in val:
                return "REJECT"
            if "APPROVE" in val:
                return "APPROVE"
        except (json.JSONDecodeError, ValueError, TypeError, AttributeError):
            pass

    # 2. Check for actionable bulleted issues under QA Issues
    issues_match = re.search(r"###\s*🚀\s*QA & Performance Issues\s*[\r\n]+(.*?)(?:\n###|\Z)", clean, re.DOTALL | re.IGNORECASE)
    if issues_match:
        content = issues_match.group(1).strip()
        has_real_issues = any(
            l.strip().startswith("-") and not re.search(r"\bnone(?:\s+identified)?\b", l, re.IGNORECASE)
            for l in content.splitlines()
        )
        if has_real_issues:
            return "REJECT"

    # 3. Check explicit ### 🎯 Verdict section
    verdict_section = re.search(r"###\s*🎯\s*Verdict\s*[\r\n]+(.*?)(?:\n\n|\Z)", clean, re.IGNORECASE | re.DOTALL)
    if verdict_section:
        verdict_text = verdict_section.group(1).strip()
        if re.search(r"\bREJECT\b", verdict_text, re.IGNORECASE):
            return "REJECT"
        if re.search(r"\bAPPROVE\b", verdict_text, re.IGNORECASE):
            return "APPROVE"

    # 4. Check explicit bracketed or formatted tokens
    if re.search(r"\[VERDICT:\s*REJECT\]", clean, re.IGNORECASE) or re.search(r"\bVERDICT:\s*REJECT\b", clean, re.IGNORECASE):
        return "REJECT"
    if re.search(r"\[VERDICT:\s*APPROVE\]", clean, re.IGNORECASE) or re.search(r"\bVERDICT:\s*APPROVE\b", clean, re.IGNORECASE):
        return "APPROVE"

    # 5. Raw REJECT presence anywhere in text
    if re.search(r"\bREJECT\b", clean, re.IGNORECASE):
        return "REJECT"

    # 6. Explicit APPROVE presence
    if re.search(r"\bAPPROVE\b", clean, re.IGNORECASE):
        return "APPROVE"

    # 7. Fail-safe: unknown or non-standard response defaults to REJECT
    return "REJECT"


def stream_review_from_ollama(model_name: str, diff_text: str, files: list[str], categories: dict[str, list[str]], deterministic_findings: list[str], truncated: bool) -> tuple[str, str]:
    system_prompt = build_system_prompt(categories, deterministic_findings)
    user_prompt = (
        f"Staged Files ({len(files)}):\n" + "\n".join(f"- `{f}`" for f in files) +
        f"\n\n```diff\n{diff_text}\n```"
    )

    payload = {
        "model": model_name,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_prompt}
        ],
        "stream": True,
        "options": {
            "temperature": 0.1,
            "top_p": 0.85,
            "num_ctx": 16384,
        },
        "keep_alive": 0  # Evict model from GPU immediately after inference
    }

    req = urllib.request.Request(
        f"{OLLAMA_ENDPOINT}/api/chat",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"}
    )

    full_response = ""
    print(f"\n{BOLD}{CYAN}🤖 QA Agent ({model_name}) reviewing {len(files)} staged file(s)...{RESET}\n")
    if truncated:
        print(f"{DIM}(Note: Staged diff exceeded pre-commit limit; truncated at file boundaries){RESET}\n")

    try:
        with urllib.request.urlopen(req, timeout=180) as response:
            for line in response:
                if not line:
                    continue
                chunk = json.loads(line.decode("utf-8"))
                token = chunk.get("message", {}).get("content", "") or chunk.get("response", "")
                sys.stdout.write(token)
                sys.stdout.flush()
                full_response += token
        print("\n")
    except Exception as e:
        print(f"\n{YELLOW}⚠️ Error during Ollama inference: {e}{RESET}\n")
        return "", "ERROR"

    verdict = parse_verdict(full_response)
    return full_response, verdict


def save_qa_report(repo_root: str, branch: str, model_name: str, files: list[str], review_text: str, verdict: str):
    git_dir = os.path.join(repo_root, ".git")
    if not os.path.exists(git_dir):
        return

    report_path = os.path.join(git_dir, "LAST_QA_REPORT.md")
    now_str = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    badge = "🟢 **PASSED (APPROVE)**" if verdict == "APPROVE" else "🔴 **REJECTED (ISSUES REPORTED)**"
    body = review_text.strip() if review_text.strip() else "_(Model returned an empty response — treated as REJECT.)_"

    report_content = f"""# 🤖 Local QA Pre-Commit Report

- **Date:** `{now_str}`
- **Branch:** `{branch}`
- **Model:** `{model_name}`
- **Status:** {badge}

---

### 📂 Staged Files ({len(files)})
""" + "\n".join(f"- `{f}`" for f in files) + f"""

---

{body}

---
*Generated automatically by `.git/hooks/pre-commit` via Ollama.*
"""

    try:
        with open(report_path, "w", encoding="utf-8") as f:
            f.write(report_content)
        print(f"{DIM}📄 Full QA report saved to: .git/LAST_QA_REPORT.md{RESET}")
    except Exception as e:
        print(f"{DIM}[QA Hook] Could not save report file: {e}{RESET}")


def prompt_user_confirmation(verdict: str) -> bool:
    tty_path = "/dev/tty"
    if not os.path.exists(tty_path):
        return True

    try:
        with open(tty_path, "r") as tty_in, open(tty_path, "w") as tty_out:
            if verdict == "APPROVE":
                tty_out.write(f"{GREEN}{BOLD}✅ QA Verdict: APPROVED! Proceeding with commit...{RESET}\n\n")
                return True
            else:
                tty_out.write(f"{YELLOW}{BOLD}⚠️  QA Review REJECTED this commit based on issues above.{RESET}\n")
                tty_out.write(f"{BOLD}Proceed with commit anyway? [Y/n]: {RESET}")
                tty_out.flush()
                answer = tty_in.readline().strip().lower()
                if answer in ("", "y", "yes"):
                    tty_out.write(f"{GREEN}Proceeding with commit.{RESET}\n\n")
                    return True
                else:
                    tty_out.write(f"{RED}Commit aborted by user.{RESET}\n\n")
                    return False
    except (KeyboardInterrupt, EOFError):
        print(f"\n{RED}Commit aborted.{RESET}")
        return False
    except Exception:
        return True


def main():
    if os.environ.get("SKIP_QA") == "1":
        return 0

    repo_root, branch, _ = get_git_info()

    try:
        file_diffs = get_staged_diff_per_file()
    except subprocess.CalledProcessError as e:
        print(f"{YELLOW}[QA Hook] Could not read git diff: {e}{RESET}")
        return 0

    if not file_diffs:
        return 0

    files = list(file_diffs.keys())
    categories = classify_files(files)

    # FAST PATH: If only documentation or static configuration files are staged, skip LLM
    has_code = any([categories["dotnet"], categories["python"], categories["shell"], categories["web"]])
    if not has_code:
        print(f"{DIM}[QA Hook] Only documentation or static configs staged. Skipping AI QA check.{RESET}")
        return 0

    deterministic_findings = run_deterministic_qa_checks(file_diffs)

    is_running, resolved_model, available_models = get_available_ollama_model()

    if not is_running:
        return 0

    if not resolved_model:
        available_str = f" (installed: {', '.join(available_models)})" if available_models else ""
        print(f"{YELLOW}[QA Hook] Ollama is running, but no suitable Qwen/Coder model was found{available_str}.{RESET}")
        print(f"{DIM}[QA Hook] To enable AI pre-commit reviews, run: `ollama pull qwen2.5-coder:7b`{RESET}")
        return 0

    diff_text, truncated = build_clean_diff_text(file_diffs, MAX_DIFF_CHARS)

    response, verdict = stream_review_from_ollama(resolved_model, diff_text, files, categories, deterministic_findings, truncated)
    if verdict == "ERROR":
        return 0

    save_qa_report(repo_root, branch, resolved_model, files, response, verdict)

    proceed = prompt_user_confirmation(verdict)
    if not proceed:
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())