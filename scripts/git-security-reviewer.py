#!/usr/bin/env python3
"""
CLI wrapper for Liner Notes AI Security Reviewer.
Runs the unified chunked review engine on staged files or a specified commit.
"""

import sys
import subprocess
import os

def main():
    repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    reviewer = os.path.join(repo_root, "scripts", "git-background-reviewer.py")

    target = sys.argv[1] if len(sys.argv) > 1 else "--staged"
    cmd = [sys.executable, reviewer, target]
    
    print(f"\033[95m🛡️  Running AI Security & QA Reviewer on {target}...\033[0m\n")
    proc = subprocess.run(cmd)

    last_report = os.path.join(repo_root, ".git", "LAST_REVIEW_REPORT.md")
    if os.path.exists(last_report):
        print("\n" + "=" * 60)
        with open(last_report, "r", encoding="utf-8") as f:
            print(f.read())
        print("=" * 60 + "\n")

    return proc.returncode

if __name__ == "__main__":
    sys.exit(main())