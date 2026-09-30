#!/usr/bin/env python3
"""
AI Pre-Commit Security Reviewer powered by local Qwen2.5-Coder (7B) via Ollama.
(DeepSeek-R1 14B configuration preserved as commented-out option)
Dedicated to detecting genuine, exploitable vulnerabilities, secret leaks,
SQL injection, and unsafe untrusted data handling without false alarms.
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

# DeepSeek-R1 (14B) - commented out:
# OLLAMA_MODEL = os.environ.get("OLLAMA_SECURITY_MODEL", "deepseek-r1:14b")

# Active Model: Qwen2.5-Coder
OLLAMA_MODEL = os.environ.get("OLLAMA_SECURITY_MODEL", "qwen2.5-coder:7b")

MAX_DIFF_CHARS = int(os.environ.get("MAX_DIFF_CHARS", "20000"))
REQUEST_TIMEOUT = int(os.environ.get("SECURITY_REVIEW_TIMEOUT", "180"))

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
MAGENTA = "\033[95m"
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


def run_deterministic_security_scan(file_diffs: dict[str, str]) -> list[str]:
    """
    Sub-millisecond static analyzer checking for definitive leaks and high-risk injection patterns
    only on relevant files.
    """
    findings = []

    for filepath, diff in file_diffs.items():
        # Do not let reviewer scripts self-trigger on their own pattern definitions
        if os.path.basename(filepath).startswith("git-") and filepath.endswith(".py"):
            continue

        ext = os.path.splitext(filepath)[1].lower()
        added_lines = [line[1:] for line in diff.splitlines() if line.startswith("+") and not line.startswith("+++")]

        for line in added_lines:
            s_line = line.strip()

            # 1. Private keys (all files)
            if re.search(r"-----BEGIN (?:RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----", s_line):
                findings.append(f"[{filepath}] Hardcoded Private Key detected: `{s_line[:40]}...`")

            # 2. Cloud & SaaS tokens (all files)
            if re.search(r"\b(?:AKIA|ABIA|ACCA|ASIA)[0-9A-Z]{16}\b", s_line):
                findings.append(f"[{filepath}] AWS Access Key ID detected: `{s_line[:30]}...`")
            if re.search(r"\bgh[pousr]_[A-Za-z0-9_]{36,}\b", s_line):
                findings.append(f"[{filepath}] GitHub Personal Access Token detected: `{s_line[:30]}...`")

            # 3. EF Core SQLi via string interpolation in raw queries (C# only)
            if ext in (".cs", ".fs"):
                if re.search(r"\.(?:FromSqlRaw|ExecuteSqlRaw|ExecuteSqlInterpolated)\s*\(\s*\$\"", s_line):
                    findings.append(f"[{filepath}] SQL Injection: String interpolation inside EF Core raw SQL method: `{s_line}`")

            # 4. Dangerous shell execution with untrusted input (Python / Shell only)
            if ext in (".py", ".sh", ".bash"):
                if re.search(r"shell\s*=\s*True", s_line) and any(x in s_line for x in ["+", "format", "f\"", "f'"]):
                    findings.append(f"[{filepath}] Potential Command Injection with shell=True and string formatting: `{s_line}`")

            # 5. Raw hardcoded password literals
            if re.search(r"""(?i)\b(?:password|passwd|client_secret)\s*[:=]\s*["'][^"'\$\{\}]{10,}["']""", s_line):
                if not any(safe_word in s_line.lower() for safe_word in ["mock", "test", "fake", "dummy", "example", "placeholder", "localhost", "secret_name", "env."]):
                    findings.append(f"[{filepath}] Potential Hardcoded Secret Literal: `{s_line[:45]}...`")

    return findings


def get_available_ollama_model() -> tuple[bool, str, list[str]]:
    try:
        req = urllib.request.Request(f"{OLLAMA_ENDPOINT}/api/tags", method="GET")
        with urllib.request.urlopen(req, timeout=3) as response:
            if response.status != 200:
                print(f"{DIM}[Security Hook] Ollama returned HTTP status {response.status}. Skipping AI check.{RESET}")
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

            # DeepSeek fallback (commented out):
            # for m in models:
            #     if "deepseek" in m.lower():
            #         return True, m, models

            return True, "", models
    except urllib.error.URLError as e:
        print(f"{DIM}[Security Hook] Ollama connection error ({e.reason}) at {OLLAMA_ENDPOINT}. Skipping AI check.{RESET}")
        return False, "", []
    except json.JSONDecodeError as e:
        print(f"{DIM}[Security Hook] Failed to parse Ollama tags response as JSON: {e}. Skipping AI check.{RESET}")
        return False, "", []
    except Exception as e:
        print(f"{DIM}[Security Hook] Unexpected error discovering Ollama models: {e}. Skipping AI check.{RESET}")
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


def extract_thinking(text: str) -> str:
    match = re.search(r"<think>(.*?)</think>", text, flags=re.DOTALL)
    return match.group(1).strip() if match else ""


def build_system_prompt(categories: dict[str, list[str]], deterministic_findings: list[str]) -> str:
    langs = []
    rules = []

    if categories["dotnet"]:
        langs.append("C# / ASP.NET Core (.NET)")
        rules.append(
            "- C# / ASP.NET Core:\n"
            "  * Injection: Flag EF Core raw queries using string interpolation ($) instead of parameterized queries.\n"
            "  * Auth/BOLA: Flag public API controller actions exposing sensitive data modification without [Authorize] or permission policies.\n"
            "  * Secrets: Flag raw connection strings containing plaintext DB passwords."
        )

    if categories["python"] or categories["shell"]:
        langs.append("Python / Shell DevOps Scripting")
        rules.append(
            "- Developer Tooling & Scripts:\n"
            "  * IMPORTANT GROUNDING: Local CLI scripts, git hooks, and build tools run under developer privileges.\n"
            "    DO NOT flag them for missing authentication, role-based authorization, or user login policies!\n"
            "  * Injection: Flag unsafe command construction with unvalidated inputs passed to shell=True, exec, or eval.\n"
            "  * Secrets: Reading environment variables via os.environ or os.getenv is standard and SAFE. Do NOT flag this."
        )

    if categories["web"]:
        langs.append("Frontend / Web")
        rules.append(
            "- Frontend:\n"
            "  * Flag dangerous innerHTML injections or sensitive backend secrets packaged into client-side code."
        )

    stack_str = ", ".join(langs) if langs else "General Code"
    rules_str = "\n".join(rules) if rules else "- Focus on genuine exploitable vulnerabilities and secret leaks."

    findings_block = ""
    if deterministic_findings:
        findings_block = (
            "\n### 🚨 AUTOMATED PRE-SCAN SECURITY FINDINGS (Confirm these in your report):\n" +
            "\n".join(f"- {finding}" for finding in deterministic_findings) +
            "\n"
        )
    else:
        findings_block = "\n### ✅ AUTOMATED PRE-SCAN: Static pattern check found no hardcoded keys or SQL interpolation.\n"

    return (
        "You are an elite Principal DevSecOps and Application Security Auditor.\n"
        f"Staged Technology Stack: {stack_str}\n\n"
        "### STRICT GROUNDING & ANTI-HALLUCINATION RULES:\n"
        "1. GROUNDING: Base findings strictly on demonstrable attack surfaces in the diff. Never hallucinate invisible dependencies or libraries.\n"
        "2. SAFE PATTERNS (DO NOT FLAG):\n"
        "   - Reading environment variables (os.environ, IConfiguration, process.env) is standard and SAFE.\n"
        "   - Local scripts, pre-commit hooks, and tooling do NOT require user authorization or JWT policies.\n"
        "   - Localhost URLs, test values, and mock tokens are SAFE.\n"
        "3. SILENCE ON SECURE CODE: If there are no genuine, exploitable vulnerabilities or exposed production secrets, you MUST output '- None identified' and APPROVE.\n"
        "4. DO NOT OUTPUT RAW JSON: You must format your response strictly using the Markdown headers below.\n\n"
        "### DOMAIN SECURITY CRITERIA:\n"
        f"{rules_str}\n"
        f"{findings_block}\n"
        "### OUTPUT FORMAT (Follow exactly):\n\n"
        "### 🛡️ Security Assessment Summary\n"
        "[1-2 crisp sentences summarizing the security posture of the changes]\n\n"
        "### ⚙️ Threat Analysis\n"
        "[Technical evaluation of inputs, execution boundaries, and secrets]\n\n"
        "### 🚨 Vulnerabilities\n"
        "- [Actionable, exploitable vulnerability with code reference, or '- None identified']\n\n"
        "### 🔒 Hardening Suggestions\n"
        "- [Concrete defense-in-depth improvement directly applicable to this diff, or '- None']\n\n"
        "### 🎯 Verdict\n"
        "[VERDICT: APPROVE] or [VERDICT: REJECT]"
    )


def parse_verdict(response_text: str, deterministic_findings: list[str]) -> str:
    # If the deterministic scanner caught a confirmed hardcoded key or raw SQLi interpolation, reject immediately
    if deterministic_findings:
        return "REJECT"

    clean = strip_thinking(response_text).strip()
    if not clean:
        return "REJECT"

    # 1. Handle JSON response fallback
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

    # 2. Check for actionable vulnerabilities listed under Vulnerabilities section
    vuln_match = re.search(r"###\s*🚨\s*Vulnerabilities\s*[\r\n]+(.*?)(?:\n###|\Z)", clean, re.DOTALL | re.IGNORECASE)
    if vuln_match:
        vuln_content = vuln_match.group(1).strip()
        has_real_vulns = any(
            l.strip().startswith("-") and not re.search(r"\bnone(?:\s+identified)?\b", l, re.IGNORECASE)
            for l in vuln_content.splitlines()
        )
        if has_real_vulns:
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
        "keep_alive": 0  # Evict model from GPU immediately after review
    }

    req = urllib.request.Request(
        f"{OLLAMA_ENDPOINT}/api/chat",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"}
    )

    full_response = ""
    in_think_block = False
    print(f"\n{BOLD}{MAGENTA}🛡️  Security Agent ({model_name}) reviewing {len(files)} staged file(s)...{RESET}\n")
    if truncated:
        print(f"{DIM}(Note: Staged diff exceeded pre-commit limit; truncated at file boundaries){RESET}\n")

    try:
        with urllib.request.urlopen(req, timeout=REQUEST_TIMEOUT) as response:
            for line in response:
                if not line:
                    continue
                chunk = json.loads(line.decode("utf-8"))
                token = chunk.get("message", {}).get("content", "") or chunk.get("response", "")
                full_response += token

                if "<think>" in token:
                    in_think_block = True
                if "</think>" in token:
                    in_think_block = False
                    sys.stdout.write(RESET)
                    sys.stdout.flush()
                    continue

                sys.stdout.write((DIM if in_think_block else RESET) + token)
                sys.stdout.flush()
        print(f"{RESET}\n")
    except Exception as e:
        print(f"\n{YELLOW}⚠️ Error during Ollama security review: {e}{RESET}\n")
        return "", "ERROR"

    verdict = parse_verdict(full_response, deterministic_findings)
    return full_response, verdict


def save_security_report(repo_root: str, branch: str, model_name: str, files: list[str], review_text: str, verdict: str):
    git_dir = os.path.join(repo_root, ".git")
    if not os.path.exists(git_dir):
        return

    report_path = os.path.join(git_dir, "LAST_SECURITY_REPORT.md")
    now_str = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    badge = "🟢 **PASSED (APPROVE)**" if verdict == "APPROVE" else "🔴 **REJECTED (SECURITY RISKS FOUND)**"

    answer = strip_thinking(review_text)
    reasoning = extract_thinking(review_text)
    reasoning_block = (
        f"\n<details>\n<summary>🧠 Model Reasoning (click to expand)</summary>\n\n{reasoning}\n\n</details>\n"
        if reasoning else ""
    )

    report_content = f"""# 🛡️ Local Security Pre-Commit Report

- **Date:** `{now_str}`
- **Branch:** `{branch}`
- **Model:** `{model_name}`
- **Status:** {badge}

---

### 📂 Staged Files ({len(files)})
""" + "\n".join(f"- `{f}`" for f in files) + f"""

---

{answer.strip()}
{reasoning_block}
---
*Generated automatically by `.git/hooks/pre-commit` via Ollama.*
"""

    try:
        with open(report_path, "w", encoding="utf-8") as f:
            f.write(report_content)
        print(f"{DIM}📄 Full Security report saved to: .git/LAST_SECURITY_REPORT.md{RESET}")
    except Exception as e:
        print(f"{DIM}[Security Hook] Could not save report file: {e}{RESET}")


def prompt_user_confirmation(verdict: str) -> bool:
    if verdict == "APPROVE":
        print(f"{GREEN}{BOLD}✅ Security Verdict: APPROVED! No blocking security risks detected.{RESET}\n")
        return True

    print(f"{RED}{BOLD}🛑 Security Review REJECTED this commit based on risks above.{RESET}\n")
    tty_path = "/dev/tty"
    try:
        if os.path.exists(tty_path):
            with open(tty_path, "r") as tty_in, open(tty_path, "w") as tty_out:
                tty_out.write(f"{BOLD}Proceed with commit anyway? [y/N]: {RESET}")
                tty_out.flush()
                answer = tty_in.readline().strip().lower()
                if answer in ("y", "yes"):
                    tty_out.write(f"{YELLOW}Bypassing security rejection upon user request.{RESET}\n\n")
                    return True
                else:
                    tty_out.write(f"{RED}Commit aborted due to security rejection.{RESET}\n\n")
                    return False
    except (KeyboardInterrupt, EOFError):
        print(f"\n{RED}Commit aborted.{RESET}")
        return False
    except Exception:
        pass

    print(f"{RED}Non-interactive session: Aborting commit due to security rejection.{RESET}\n")
    return False


def main():
    if os.environ.get("SKIP_SECURITY_REVIEW") == "1" or os.environ.get("SKIP_QA") == "1":
        return 0

    repo_root, branch, _ = get_git_info()

    try:
        file_diffs = get_staged_diff_per_file()
    except subprocess.CalledProcessError as e:
        print(f"{YELLOW}[Security Hook] Could not read git diff: {e}{RESET}")
        return 0

    if not file_diffs:
        return 0

    files = list(file_diffs.keys())
    categories = classify_files(files)

    # Deterministic static scan runs in < 2ms
    deterministic_findings = run_deterministic_security_scan(file_diffs)

    # FAST PATH: If only documentation or static configuration files are staged, skip LLM
    has_code = any([categories["dotnet"], categories["python"], categories["shell"], categories["web"]])
    if not has_code:
        if not deterministic_findings:
            print(f"{DIM}[Security Hook] Only documentation or static configs staged (secrets scan clean). Skipping AI security check.{RESET}")
            return 0

    is_running, resolved_model, available_models = get_available_ollama_model()

    if not is_running:
        return 0

    if not resolved_model:
        available_str = f" (installed: {', '.join(available_models)})" if available_models else ""
        print(f"{YELLOW}[Security Hook] Ollama is running, but no suitable Qwen/Coder model was found{available_str}.{RESET}")
        print(f"{DIM}[Security Hook] To enable AI pre-commit security reviews, run: `ollama pull qwen2.5-coder:7b`{RESET}")
        # print(f"{DIM}[Security Hook] For DeepSeek-R1: `ollama pull deepseek-r1:14b`{RESET}")
        return 0

    diff_text, truncated = build_clean_diff_text(file_diffs, MAX_DIFF_CHARS)

    response, verdict = stream_review_from_ollama(resolved_model, diff_text, files, categories, deterministic_findings, truncated)
    if verdict == "ERROR":
        return 0

    save_security_report(repo_root, branch, resolved_model, files, response, verdict)

    proceed = prompt_user_confirmation(verdict)
    if not proceed:
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())