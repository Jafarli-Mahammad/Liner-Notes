#!/usr/bin/env bash
# Deterministic QA Reviewer: Build and test suite verification
# Runs solution build and automated tests with zero hallucinations.

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || echo ".")"

echo -e "\033[96m⚙️  Building solution...\033[0m"
dotnet build "$REPO_ROOT/Liner Notes.sln" '/clp:NoSummary;ErrorsOnly'
BUILD_STATUS=$?

if [ $BUILD_STATUS -ne 0 ]; then
    echo -e "\033[91m🛑 Build failed with errors!\033[0m"
    exit 1
fi

echo -e "\033[96m🧪 Running automated test suite...\033[0m"
dotnet test "$REPO_ROOT/Liner Notes.sln" --logger "console;verbosity=quiet"
TEST_STATUS=$?

if [ $TEST_STATUS -ne 0 ]; then
    echo -e "\033[91m🛑 Automated tests failed!\033[0m"
    exit 1
fi

echo -e "\033[92m\033[1m✅ All QA checks passed (Build OK, Tests OK)!\033[0m"
exit 0