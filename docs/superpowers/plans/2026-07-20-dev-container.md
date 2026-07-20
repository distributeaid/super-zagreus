# Dev Container Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A sandboxed VS Code dev container (with egress firewall, Postgres sidecar, and automated smoke tests) so Claude Code can run with `--dangerously-skip-permissions` safely.

**Architecture:** Docker-compose-based devcontainer: a `dev` service built on Microsoft's .NET 8 devcontainer image (Node 22 + Claude Code added via devcontainer features) and a `db` service running postgres:16. A default-deny iptables/ipset firewall (adapted from Anthropic's reference) runs on every container start; a checked-in smoke-test script re-verifies the whole environment on every container creation and on demand.

**Tech Stack:** Dev Containers spec (docker-compose variant), Debian iptables/ipset, .NET 8 SDK, Node 22 + Yarn 4 (corepack), PostgreSQL 16.

**Spec:** `docs/superpowers/specs/2026-07-20-dev-container-design.md`

## Global Constraints

- Database service name `db`, image `postgres:16`, env exactly: `POSTGRES_USER=da_user`, `POSTGRES_PASSWORD=da_password`, `POSTGRES_DB=da_needs_assessment` (matches host `da-postgres` and `appsettings.json`). Data volume: `zagreus-pgdata`. Do NOT publish 5432 to the host (host already runs `da-postgres` there).
- Connection-string override only via container env: `ConnectionStrings__Default=Host=db;Port=5432;Database=da_needs_assessment;Username=da_user;Password=da_password`. No checked-in app-config changes.
- Node 22 (repo requires `>=20.0.0 <25.0.0`), Yarn `4.12.0` activated via corepack (`packageManager` field in root `package.json`).
- API solution path: `apps/api/DistributeAid.NeedsAssessment.sln`. Web workspace name: `@zagreus/web` (test script = `vitest run`). Migrations are committed; the API applies them itself on startup.
- Forwarded ports: 3000 (web), 54764 (API http). 54763 (https) intentionally not forwarded.
- Container user is `vscode`; its ONLY sudo right is `/usr/local/bin/init-firewall.sh` (default passwordless-sudo files are removed — otherwise an auto-mode agent could just flush iptables).
- Named volumes: `claude-config` → `/home/vscode/.claude`, `dotnet-usersecrets` → `/home/vscode/.microsoft/usersecrets`, `zagreus-pgdata` → db data. Host `~/.claude` is never mounted.
- Workspace folder inside the container: `/workspaces/zagreus`.
- Nothing outside `.devcontainer/`, `docs/`, and a one-line README pointer may change.
- All firewall/config file contents in this plan come from the approved spec; the firewall base script is Anthropic's reference `anthropics/claude-code/.devcontainer/init-firewall.sh` with an extended allowlist.

## Execution environment notes (read first)

- You are working in a git worktree on branch `claude/dev-container-setup-c38135`; push commits to the remote branch `docs/dev-container-spec` (open PR #14) with `git push origin HEAD:docs/dev-container-spec`.
- Host is macOS with Docker Desktop. Use the devcontainer CLI via `npx -y @devcontainers/cli` from the repo root (the worktree root). All `devcontainer`/`docker` commands talk to the Docker unix socket, which the Claude Code command sandbox blocks — run them with the sandbox disabled when they fail with socket/permission errors.
- Because this is a worktree, `.git` is a file pointing outside the mounted workspace, so **git inside the container will not work during these tests**. That's fine: no verification step below uses git inside the container. (From a normal clone, git works.)
- Container builds download several GB (base image, features, postgres). Expect `up` to take minutes on first run.
- Between tasks that change `.devcontainer/`, recreate the container: `docker compose -p zagreus-devcontainer -f .devcontainer/docker-compose.yml down` then `npx -y @devcontainers/cli up --workspace-folder .` (add `--remove-existing-container` to force a rebuild after Dockerfile changes).

---

### Task 1: Base container (compose + Dockerfile + devcontainer.json, no firewall yet)

**Files:**
- Create: `.devcontainer/docker-compose.yml`
- Create: `.devcontainer/Dockerfile`
- Create: `.devcontainer/devcontainer.json`

**Interfaces:**
- Produces: a `dev` container where `dotnet` (8.x), `node` (22.x), `yarn` (4.x), `claude`, `dotnet-ef`, and `psql` all work, with the repo at `/workspaces/zagreus` and Postgres at `db:5432`. Task 2 adds `COPY init-firewall.sh` + sudoers to this Dockerfile and lifecycle commands to this devcontainer.json; Task 3 appends the smoke test to `postCreateCommand`.

- [ ] **Step 1: Write `.devcontainer/docker-compose.yml`**

```yaml
name: zagreus-devcontainer

services:
  dev:
    build:
      context: .
      dockerfile: Dockerfile
    cap_add:
      - NET_ADMIN
      - NET_RAW
    volumes:
      - ..:/workspaces/zagreus:cached
      - claude-config:/home/vscode/.claude
      - dotnet-usersecrets:/home/vscode/.microsoft/usersecrets
    environment:
      ConnectionStrings__Default: "Host=db;Port=5432;Database=da_needs_assessment;Username=da_user;Password=da_password"
    command: sleep infinity
    depends_on:
      - db

  db:
    image: postgres:16
    restart: unless-stopped
    environment:
      POSTGRES_USER: da_user
      POSTGRES_PASSWORD: da_password
      POSTGRES_DB: da_needs_assessment
    volumes:
      - zagreus-pgdata:/var/lib/postgresql/data

volumes:
  claude-config:
  dotnet-usersecrets:
  zagreus-pgdata:
```

- [ ] **Step 2: Write `.devcontainer/Dockerfile`**

```dockerfile
FROM mcr.microsoft.com/devcontainers/dotnet:8.0

# Firewall prerequisites (iptables/ipset used by init-firewall.sh, task 2),
# postgres client for the smoke test, jq/aggregate for GitHub IP-range handling.
RUN apt-get update && apt-get install -y --no-install-recommends \
    iptables \
    ipset \
    iproute2 \
    dnsutils \
    aggregate \
    jq \
    postgresql-client \
  && apt-get clean && rm -rf /var/lib/apt/lists/*

# Pre-create the named-volume mount points with vscode ownership so fresh
# volumes are initialized writable by the container user (no sudo chown needed).
RUN mkdir -p /home/vscode/.claude /home/vscode/.microsoft/usersecrets \
  && chown -R vscode:vscode /home/vscode/.claude /home/vscode/.microsoft

USER vscode
RUN dotnet tool install --global dotnet-ef --version 8.*
ENV PATH=$PATH:/home/vscode/.dotnet/tools
```

- [ ] **Step 3: Write `.devcontainer/devcontainer.json`**

```json
{
  "name": "Zagreus Sandbox",
  "dockerComposeFile": "docker-compose.yml",
  "service": "dev",
  "workspaceFolder": "/workspaces/zagreus",
  "features": {
    "ghcr.io/devcontainers/features/node:1": { "version": "22" },
    "ghcr.io/anthropics/devcontainer-features/claude-code:1": {}
  },
  "forwardPorts": [3000, 54764],
  "postCreateCommand": "corepack enable",
  "remoteUser": "vscode",
  "customizations": {
    "vscode": {
      "extensions": [
        "anthropic.claude-code",
        "ms-dotnettools.csdevkit",
        "dbaeumer.vscode-eslint",
        "esbenp.prettier-vscode"
      ]
    }
  }
}
```

Contingency (verify in Step 5): if the `ghcr.io/anthropics/devcontainer-features/claude-code:1` feature cannot be pulled, remove that feature line and instead change `postCreateCommand` to `"corepack enable && curl -fsSL https://claude.ai/install.sh | bash"` (installer puts `claude` in `~/.local/bin`, which the base image has on PATH).

- [ ] **Step 4: Build and start the container**

Run (from repo root): `npx -y @devcontainers/cli up --workspace-folder .`
Expected: ends with a JSON line containing `"outcome":"success"`. First run takes several minutes.

- [ ] **Step 5: Verify toolchain and database**

Run each; all must succeed:

```bash
npx -y @devcontainers/cli exec --workspace-folder . dotnet --version        # 8.0.x
npx -y @devcontainers/cli exec --workspace-folder . node --version          # v22.x.y
npx -y @devcontainers/cli exec --workspace-folder . yarn --version          # 4.12.0
npx -y @devcontainers/cli exec --workspace-folder . claude --version        # prints a version
npx -y @devcontainers/cli exec --workspace-folder . dotnet-ef --version     # prints a version
npx -y @devcontainers/cli exec --workspace-folder . bash -c 'PGPASSWORD=da_password psql -h db -U da_user -d da_needs_assessment -c "SELECT 1"'   # prints "1"
```

If `yarn --version` fails because corepack couldn't write its shims as `vscode`, change `postCreateCommand`'s `corepack enable` to `corepack enable --install-directory "$HOME/.local/bin"` and recreate.

- [ ] **Step 6: Verify host `da-postgres` is untouched**

Run: `docker ps -a --format '{{.Names}}\t{{.Ports}}' | grep da-postgres`
Expected: unchanged from before (still bound to host 5432); the compose `db` service must show no host port binding in `docker ps`.

- [ ] **Step 7: Commit**

```bash
git add .devcontainer/docker-compose.yml .devcontainer/Dockerfile .devcontainer/devcontainer.json
git commit -m "feat(devcontainer): base dev container with dotnet+node toolchain and postgres sidecar"
```

---

### Task 2: Egress firewall

**Files:**
- Create: `.devcontainer/init-firewall.sh`
- Modify: `.devcontainer/Dockerfile` (append COPY + sudoers block)
- Modify: `.devcontainer/devcontainer.json` (lifecycle commands)

**Interfaces:**
- Consumes: Task 1's container; `vscode` user.
- Produces: `/usr/local/bin/init-firewall.sh` runnable only via `sudo` by `vscode`; run on every start via `postStartCommand`. Default-deny egress with the spec's allowlist; compose-internal subnet fully open. Task 3's smoke test probes this firewall.

- [ ] **Step 1: Write `.devcontainer/init-firewall.sh`**

This is Anthropic's reference script with three changes: (a) extended domain list per the spec, (b) the host-IP /24 rule replaced with a loop allowing all directly-attached (compose) subnets so `db` is reachable, (c) an added end-check that api.anthropic.com is reachable.

```bash
#!/bin/bash
set -euo pipefail  # Exit on error, undefined vars, and pipeline failures
IFS=$'\n\t'       # Stricter word splitting

# 1. Extract Docker DNS info BEFORE any flushing
DOCKER_DNS_RULES=$(iptables-save -t nat | grep "127\.0\.0\.11" || true)

# Flush existing rules and delete existing ipsets
iptables -F
iptables -X
iptables -t nat -F
iptables -t nat -X
iptables -t mangle -F
iptables -t mangle -X
ipset destroy allowed-domains 2>/dev/null || true

# 2. Selectively restore ONLY internal Docker DNS resolution
if [ -n "$DOCKER_DNS_RULES" ]; then
    echo "Restoring Docker DNS rules..."
    iptables -t nat -N DOCKER_OUTPUT 2>/dev/null || true
    iptables -t nat -N DOCKER_POSTROUTING 2>/dev/null || true
    echo "$DOCKER_DNS_RULES" | xargs -L 1 iptables -t nat
else
    echo "No Docker DNS rules to restore"
fi

# First allow DNS and localhost before any restrictions
# Allow outbound DNS
iptables -A OUTPUT -p udp --dport 53 -j ACCEPT
# Allow inbound DNS responses
iptables -A INPUT -p udp --sport 53 -j ACCEPT
# Allow outbound SSH
iptables -A OUTPUT -p tcp --dport 22 -j ACCEPT
# Allow inbound SSH responses
iptables -A INPUT -p tcp --sport 22 -m state --state ESTABLISHED -j ACCEPT
# Allow localhost
iptables -A INPUT -i lo -j ACCEPT
iptables -A OUTPUT -o lo -j ACCEPT

# Create ipset with CIDR support
ipset create allowed-domains hash:net

# Fetch GitHub meta information and aggregate + add their IP ranges
echo "Fetching GitHub IP ranges..."
gh_ranges=$(curl -s https://api.github.com/meta)
if [ -z "$gh_ranges" ]; then
    echo "ERROR: Failed to fetch GitHub IP ranges"
    exit 1
fi

if ! echo "$gh_ranges" | jq -e '.web and .api and .git' >/dev/null; then
    echo "ERROR: GitHub API response missing required fields"
    exit 1
fi

echo "Processing GitHub IPs..."
while read -r cidr; do
    if [[ ! "$cidr" =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}/[0-9]{1,2}$ ]]; then
        echo "ERROR: Invalid CIDR range from GitHub meta: $cidr"
        exit 1
    fi
    echo "Adding GitHub range $cidr"
    ipset add allowed-domains "$cidr"
done < <(echo "$gh_ranges" | jq -r '(.web + .api + .git)[]' | aggregate -q)

# Resolve and add other allowed domains.
# Groups: upstream Claude/VS Code defaults; Claude login; Yarn; .NET/NuGet;
# OAuth token verification for the API (Google/Microsoft).
for domain in \
    "registry.npmjs.org" \
    "api.anthropic.com" \
    "sentry.io" \
    "statsig.anthropic.com" \
    "statsig.com" \
    "marketplace.visualstudio.com" \
    "vscode.blob.core.windows.net" \
    "update.code.visualstudio.com" \
    "claude.ai" \
    "console.anthropic.com" \
    "platform.claude.com" \
    "repo.yarnpkg.com" \
    "api.nuget.org" \
    "dot.net" \
    "builds.dotnet.microsoft.com" \
    "ci.dot.net" \
    "oauth2.googleapis.com" \
    "www.googleapis.com" \
    "login.microsoftonline.com" \
    "graph.microsoft.com"; do
    echo "Resolving $domain..."
    ips=$(dig +noall +answer A "$domain" | awk '$4 == "A" {print $5}')
    if [ -z "$ips" ]; then
        echo "ERROR: Failed to resolve $domain"
        exit 1
    fi

    while read -r ip; do
        if [[ ! "$ip" =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]]; then
            echo "ERROR: Invalid IP from DNS for $domain: $ip"
            exit 1
        fi
        echo "Adding $ip for $domain"
        ipset add allowed-domains "$ip"
    done < <(echo "$ips")
done

# Allow the compose-internal network(s): the db service and the VS Code
# port-forward path live on directly-attached subnets (kernel routes).
for subnet in $(ip -o -f inet route show proto kernel | awk '{print $1}'); do
    echo "Allowing attached subnet $subnet"
    iptables -A INPUT -s "$subnet" -j ACCEPT
    iptables -A OUTPUT -d "$subnet" -j ACCEPT
done

# Set default policies to DROP first
iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP

# First allow established connections for already approved traffic
iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT

# Then allow only specific outbound traffic to allowed domains
iptables -A OUTPUT -m set --match-set allowed-domains dst -j ACCEPT

# Explicitly REJECT all other outbound traffic for immediate feedback
iptables -A OUTPUT -j REJECT --reject-with icmp-admin-prohibited

echo "Firewall configuration complete"
echo "Verifying firewall rules..."
if curl --connect-timeout 5 https://example.com >/dev/null 2>&1; then
    echo "ERROR: Firewall verification failed - was able to reach https://example.com"
    exit 1
else
    echo "Firewall verification passed - unable to reach https://example.com as expected"
fi

# Verify GitHub API access
if ! curl --connect-timeout 5 https://api.github.com/zen >/dev/null 2>&1; then
    echo "ERROR: Firewall verification failed - unable to reach https://api.github.com"
    exit 1
else
    echo "Firewall verification passed - able to reach https://api.github.com as expected"
fi

# Verify Anthropic API access
if ! curl --connect-timeout 5 https://api.anthropic.com >/dev/null 2>&1; then
    echo "ERROR: Firewall verification failed - unable to reach https://api.anthropic.com"
    exit 1
else
    echo "Firewall verification passed - able to reach https://api.anthropic.com as expected"
fi
```

- [ ] **Step 2: Append the firewall install block to `.devcontainer/Dockerfile`**

Add BEFORE the `USER vscode` line (COPY/sudoers must run as root):

```dockerfile
# Install the firewall script; vscode's ONLY sudo right is running it.
# The default devcontainer passwordless-sudo grant is removed so an
# auto-mode agent inside the container cannot simply flush iptables.
COPY init-firewall.sh /usr/local/bin/init-firewall.sh
RUN chmod 755 /usr/local/bin/init-firewall.sh \
  && rm -f /etc/sudoers.d/* \
  && echo "vscode ALL=(root) NOPASSWD: /usr/local/bin/init-firewall.sh" > /etc/sudoers.d/init-firewall \
  && chmod 0440 /etc/sudoers.d/init-firewall
```

- [ ] **Step 3: Wire lifecycle commands in `.devcontainer/devcontainer.json`**

Replace the `postCreateCommand` line and add `postStartCommand`/`waitFor`:

```json
  "postCreateCommand": "corepack enable && sudo /usr/local/bin/init-firewall.sh",
  "postStartCommand": "sudo /usr/local/bin/init-firewall.sh",
  "waitFor": "postStartCommand",
```

(Task 3 appends the smoke test to `postCreateCommand`. The firewall runs in postCreate too so first-create verification and the smoke test see enforced rules; the script is idempotent — it flushes and rebuilds.)

- [ ] **Step 4: Rebuild the container**

```bash
docker compose -p zagreus-devcontainer -f .devcontainer/docker-compose.yml down
npx -y @devcontainers/cli up --workspace-folder . --remove-existing-container --build-no-cache
```

Expected: `"outcome":"success"`, and the up log shows the firewall's own "Firewall verification passed" lines twice (postCreate + postStart).

- [ ] **Step 5: Verify enforcement from inside**

```bash
npx -y @devcontainers/cli exec --workspace-folder . bash -c 'curl -m 5 -s https://example.com; echo "exit=$?"'                     # nonzero exit
npx -y @devcontainers/cli exec --workspace-folder . bash -c 'curl -m 5 -sI https://api.anthropic.com >/dev/null && echo OK'       # OK
npx -y @devcontainers/cli exec --workspace-folder . bash -c 'curl -m 5 -sI https://registry.npmjs.org >/dev/null && echo OK'      # OK
npx -y @devcontainers/cli exec --workspace-folder . bash -c 'PGPASSWORD=da_password psql -h db -U da_user -d da_needs_assessment -c "SELECT 1"'  # still works
npx -y @devcontainers/cli exec --workspace-folder . bash -c 'sudo iptables -F 2>&1; echo "exit=$?"'                               # DENIED: sudo only allows init-firewall.sh
```

The last check must FAIL (nonzero, "not allowed" message) — that's the hardening working.

- [ ] **Step 6: Commit**

```bash
git add .devcontainer/init-firewall.sh .devcontainer/Dockerfile .devcontainer/devcontainer.json
git commit -m "feat(devcontainer): default-deny egress firewall with project allowlist"
```

---

### Task 3: Automated smoke test

**Files:**
- Create: `.devcontainer/smoke-test.sh`
- Modify: `.devcontainer/devcontainer.json` (append to `postCreateCommand`)

**Interfaces:**
- Consumes: Task 1 toolchain, Task 2 firewall.
- Produces: `bash .devcontainer/smoke-test.sh` — exits 0 iff all checks pass, prints one `PASS:`/`FAIL:` line per check; runs automatically on container creation. Task 4's docs reference it by this path; no secrets or login required.

- [ ] **Step 1: Write `.devcontainer/smoke-test.sh`**

```bash
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
```

- [ ] **Step 2: Run it in the container — verify it passes**

Run: `npx -y @devcontainers/cli exec --workspace-folder . bash .devcontainer/smoke-test.sh`
Expected: every line `PASS:`, final line `SMOKE TEST PASSED`, exit 0.

- [ ] **Step 3: Verify it actually fails when the firewall is down**

Negative test — a smoke test that can't fail is worthless. Open the firewall as root via docker (bypassing the in-container sudo restriction, which is exactly why only root-on-host can do this), then expect the smoke test to fail:

```bash
docker compose -p zagreus-devcontainer -f .devcontainer/docker-compose.yml exec --user root dev iptables -P OUTPUT ACCEPT
docker compose -p zagreus-devcontainer -f .devcontainer/docker-compose.yml exec --user root dev iptables -F OUTPUT
npx -y @devcontainers/cli exec --workspace-folder . bash .devcontainer/smoke-test.sh; echo "exit=$?"
```

Expected: `FAIL: egress blocked ...` line, `SMOKE TEST FAILED`, `exit=1`.
Then restore: `npx -y @devcontainers/cli exec --workspace-folder . sudo /usr/local/bin/init-firewall.sh` and re-run the smoke test → `SMOKE TEST PASSED`.

- [ ] **Step 4: Wire into container creation**

In `.devcontainer/devcontainer.json`, change `postCreateCommand` to:

```json
  "postCreateCommand": "corepack enable && sudo /usr/local/bin/init-firewall.sh && bash .devcontainer/smoke-test.sh",
```

- [ ] **Step 5: Verify postCreate wiring with a full recreate**

```bash
docker compose -p zagreus-devcontainer -f .devcontainer/docker-compose.yml down
npx -y @devcontainers/cli up --workspace-folder . --remove-existing-container
```

Expected: `"outcome":"success"` and the log contains `SMOKE TEST PASSED`.

- [ ] **Step 6: Commit**

```bash
git add .devcontainer/smoke-test.sh .devcontainer/devcontainer.json
git commit -m "feat(devcontainer): automated smoke test, run on every container creation"
```

---

### Task 4: Documentation

**Files:**
- Create: `docs/dev-container.md`
- Modify: `README.md` (one line in Quick start)

**Interfaces:**
- Consumes: everything above (paths/commands must match exactly: `.devcontainer/smoke-test.sh`, `/usr/local/bin/init-firewall.sh`, ports 3000/54764, volume names).

- [ ] **Step 1: Write `docs/dev-container.md`**

```markdown
# Dev container — sandboxed environment for Claude auto mode

The `.devcontainer/` setup runs the full Zagreus toolchain (.NET 8, Node 22,
Yarn 4, PostgreSQL 16, Claude Code) inside a container with a default-deny
egress firewall. Its purpose: make `claude --dangerously-skip-permissions`
safe to run — the container is the trust boundary. Host credentials
(`~/.claude`, keychain, ssh keys) are never mounted; outbound network is
limited to an allowlist; the app stack talks freely only inside the
container's compose network.

## Prerequisites

- Docker Desktop
- VS Code with the **Dev Containers** extension

## First run

1. Open the repo in VS Code → **Reopen in Container** (first build takes a
   few minutes; it ends by running the smoke test — see below).
2. `claude` → complete the one-time browser login. The credential lives in
   the `claude-config` docker volume and survives rebuilds.
3. Set up dotnet user-secrets and `apps/web/.env.local` per
   [local-development.md](local-development.md). User-secrets persist in the
   `dotnet-usersecrets` volume; `.env.local` rides along with the workspace.
   Keep `NEXTAUTH_URL` and OAuth redirect URIs at `http://localhost:3000` —
   ports 3000 and 54764 are forwarded, and the sign-in browser runs on your
   host.
4. Provision your sign-in email with the DA.NA.Provision tool as usual.

## Daily use

```bash
claude --dangerously-skip-permissions    # sandboxed auto mode
cd apps/api/DA.NA.Api && dotnet run      # API  → http://localhost:54764
yarn workspace @zagreus/web dev          # web  → http://localhost:3000
```

The database is the compose `db` service (`db:5432`, same credentials as the
host `da-postgres`); the API's connection string is overridden by container
env, so host workflows are unaffected.

## Smoke test

`bash .devcontainer/smoke-test.sh` verifies the toolchain, firewall
enforcement, database, and both workspaces' builds/tests. It runs
automatically on every container creation and needs no secrets. Run it:

- as the first debugging step whenever the container misbehaves, and
- always after editing `.devcontainer/` (firewall allowlist, Dockerfile).

Failures print `FAIL:` lines; re-run the named command without output
redirection to investigate.

## The firewall

`/usr/local/bin/init-firewall.sh` (source: `.devcontainer/init-firewall.sh`)
runs on every container start. Everything outbound is dropped except DNS,
the compose network, and an allowlist of domains (Anthropic/Claude, npm,
Yarn, NuGet/.NET, GitHub, VS Code, and the Google/Microsoft OAuth token
endpoints the API calls). IPs are resolved once at start.

- **Add a domain:** add one line to the `for domain in` list in
  `.devcontainer/init-firewall.sh`, rebuild the container, run the smoke
  test.
- **A previously working domain fails:** its IPs may have rotated since
  container start — restart the container (re-resolves everything).
- **Firewall self-test fails on start:** the container refuses to come up
  quietly; check the creation log, fix, rebuild.

The `vscode` user's only sudo right is running the firewall script — an
agent inside the container cannot disable filtering.

## One-time manual checks (not automated)

- `claude --dangerously-skip-permissions` starts after login.
- Full sign-in: run API + web, sign in with Google/Microsoft from a host
  browser at http://localhost:3000.
```

- [ ] **Step 2: Add the README pointer**

In `README.md`, in the **Quick start** section, add as a new numbered item after item 2:

```markdown
3. **Sandboxed Claude auto mode** (optional): open in the dev container —
   see [docs/dev-container.md](docs/dev-container.md).
```

- [ ] **Step 3: Verify docs accuracy**

Check every path, command, port, and volume name in the new docs against the actual files from tasks 1–3 (`grep` them). No other README sections may change.

- [ ] **Step 4: Commit**

```bash
git add docs/dev-container.md README.md
git commit -m "docs: dev container guide and README pointer"
```

---

### Task 5: Fresh-build acceptance run

**Files:** none (verification only; fix regressions in the files above if found)

- [ ] **Step 1: Tear down everything including volumes**

```bash
docker compose -p zagreus-devcontainer -f .devcontainer/docker-compose.yml down -v
```

(`-v` removes `claude-config`, `dotnet-usersecrets`, `zagreus-pgdata` — acceptable now; after real use, never use `-v` casually.)

- [ ] **Step 2: Cold build from scratch**

```bash
npx -y @devcontainers/cli up --workspace-folder .
```

Expected: `"outcome":"success"`; log contains "Firewall verification passed" and `SMOKE TEST PASSED` (fresh volumes, fresh db — proves migrations+seed work via `dotnet test`'s and the API's own startup path, and that the whole clone→container flow needs zero host setup).

- [ ] **Step 3: Confirm working tree contains only intended changes**

Run: `git status --short` and `git log --oneline origin/main..HEAD -- . ':!docs' ':!.devcontainer' ':!README.md'`
Expected: clean tree; the second command lists no commits touching anything outside `.devcontainer/`, `docs/`, `README.md`.

- [ ] **Step 4: Push and hand off**

```bash
git push origin HEAD:docs/dev-container-spec
```

Then use superpowers:finishing-a-development-branch — the work rides on the existing PR #14 (spec + implementation reviewed together).
