#!/bin/bash
# Zagreus dev container smoke test.
# Verifies toolchain, firewall enforcement, database, and both workspaces.
# Needs no secrets and no interactive login, so it can run unattended
# (postCreateCommand) and under Claude auto mode. Exits nonzero on any failure.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FAILURES=0

pass() { echo "PASS: $1"; }
fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }

check() { # check <name> <command> [args...]
  local name="$1"; shift
  if "$@" >/dev/null 2>&1; then pass "$name"; else fail "$name"; fi
}

echo "== Toolchain =="
check "dotnet SDK 8.x" bash -c 'dotnet --version | grep -q "^8\."'
check "node 20-24" node -e 'const v = +process.versions.node.split(".")[0]; process.exit(v >= 20 && v < 25 ? 0 : 1)'
check "yarn 4.x" bash -c 'yarn --version | grep -q "^4\."'
check "claude CLI" claude --version
check "dotnet-ef tool" dotnet-ef --version

echo "== Firewall =="
if curl -m 5 -s https://example.com >/dev/null 2>&1; then
  fail "egress blocked (example.com must be unreachable)"
else
  pass "egress blocked (example.com unreachable)"
fi
for host in api.anthropic.com registry.npmjs.org api.nuget.org api.github.com; do
  check "allowlisted: $host" curl -m 5 -sI "https://$host"
done
if sudo -n iptables -L >/dev/null 2>&1; then
  fail "sudo lockdown (vscode must NOT run arbitrary sudo commands)"
else
  pass "sudo lockdown (arbitrary sudo denied)"
fi

echo "== Database =="
check "postgres db:5432 (SELECT 1)" env PGPASSWORD=da_password \
  psql -h db -U da_user -d da_needs_assessment -c "SELECT 1"

echo "== API workspace =="
check "dotnet build" dotnet build "$REPO_ROOT/apps/api/DistributeAid.NeedsAssessment.sln"
check "dotnet test" dotnet test "$REPO_ROOT/apps/api/DistributeAid.NeedsAssessment.sln"

echo "== Web workspace =="
check "yarn install" bash -c "cd '$REPO_ROOT' && yarn install --immutable"
check "web tests" bash -c "cd '$REPO_ROOT' && yarn workspace @zagreus/web test"

echo
if [ "$FAILURES" -gt 0 ]; then
  echo "SMOKE TEST FAILED: $FAILURES check(s) failed."
  echo "Re-run a failed check without '>/dev/null' to see its output, e.g.:"
  echo "  dotnet test $REPO_ROOT/apps/api/DistributeAid.NeedsAssessment.sln"
  exit 1
fi
echo "SMOKE TEST PASSED"
