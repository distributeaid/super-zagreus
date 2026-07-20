# Zagreus — Dev Container Design

**Purpose:** A sandboxed VS Code dev container so Claude Code can run with `--dangerously-skip-permissions` (auto mode) safely — container isolation plus a default-deny egress firewall.
**Document status:** Approved design — v1.0
**Date:** 2026-07-20
**Author:** Corey (with Claude)

---

## 1. Overview

The repo gets a `.devcontainer/` setup that provides the full Zagreus toolchain (.NET 8, Node 22 + Yarn 4, PostgreSQL 16, Claude Code) inside a container. The container is the trust boundary: Claude runs in auto mode inside it, with outbound network restricted to an allowlist, no access to host credentials, and the workspace as the only shared filesystem.

Approach: Microsoft's .NET 8 devcontainer image as the base (the API toolchain is the fussier one), Node and Claude Code layered on via devcontainer features/installers, and the egress firewall copied largely verbatim from Anthropic's reference `claude-code` devcontainer with an extended allowlist.

### Goals
- Fresh clone → "Reopen in Container" → everything builds, tests, and runs.
- `claude --dangerously-skip-permissions` is safe to run inside: no host secrets, allowlisted egress only.
- Full app flow works in-container: Postgres, API (`dotnet run`), web (`yarn dev`), and Google/Microsoft sign-in from the host browser.
- Host workflow (README quick start with host `da-postgres`) keeps working unchanged.

### Non-goals
- No CI usage of this container.
- No devcontainer support for editors other than VS Code (the files are standard, so others may work, but only VS Code is tested/documented).
- No production hardening — this is a local dev sandbox.

## 2. Architecture & files

```
.devcontainer/
  devcontainer.json     # VS Code entry point; docker-compose based, service "dev"
  docker-compose.yml    # services: dev (toolchain) + db (postgres:16)
  Dockerfile            # dev image, based on mcr.microsoft.com/devcontainers/dotnet:8.0
  init-firewall.sh      # egress allowlist, adapted from anthropic/claude-code reference
  smoke-test.sh         # automated in-container verification suite (see section 6)
docs/
  dev-container.md      # first-run and daily-use guide
```

### `dev` service
- **Base image:** `mcr.microsoft.com/devcontainers/dotnet:8.0`.
- **Added in Dockerfile / features:**
  - Node 22 with corepack enabled (repo requires node `>=20 <25`, `yarn@4.12.0` via `packageManager`) — via the standard `node` devcontainer feature.
  - Claude Code via its official install script.
  - `dotnet-ef` global tool (migrations).
  - Firewall prerequisites: `iptables`, `ipset`, `dnsutils`, `jq`, `aggregate`.
- **Capabilities:** `NET_ADMIN` and `NET_RAW` (required for iptables/ipset).
- **Lifecycle:** `postStartCommand` runs `sudo init-firewall.sh` on every container start (the script must be root-owned and the `vscode` user gets a narrow sudoers entry for it, matching the upstream reference).
- **Workspace:** standard bind mount of the repo.
- **Forwarded ports:** 3000 (web), 54764 (API http). The https port 54763 is not forwarded; in-container dev uses the http endpoint.

### `db` service
- `postgres:16` (matches the README's host `da-postgres`).
- Environment: `POSTGRES_USER=da_user`, `POSTGRES_PASSWORD=da_password`, `POSTGRES_DB=da_needs_assessment` — identical to the existing host setup and `appsettings.json`.
- Named volume `zagreus-pgdata` for data; survives rebuilds.
- Reachable from `dev` as host `db:5432` on the compose network. Not published to the host (the host already has its own `da-postgres` on 5432; no port conflict).

### Connection-string override
`appsettings.json` points at `Host=localhost`. Inside the container the DB host is `db`, so `devcontainer.json` sets `ConnectionStrings__Default=Host=db;Port=5432;Database=da_needs_assessment;Username=da_user;Password=da_password` in `containerEnv`. ASP.NET Core's env-var configuration precedence makes this win over `appsettings.json` with no checked-in config changes; the host workflow is untouched.

## 3. Egress firewall

`init-firewall.sh` is Anthropic's reference script with an extended allowlist.

- **Default-deny outbound.** iptables OUTPUT policy DROP; only allowlisted destinations, DNS, localhost, and established connections pass. Inbound is limited to established/related traffic (VS Code port forwarding tunnels over the existing connection, so forwarded ports keep working).
- **Domain allowlist resolved at start.** Each allowed domain resolves to IPs loaded into an `ipset`; GitHub's published ranges (web/api/git) come from `api.github.com/meta`. Because IPs are resolved once at container start, a stale DNS answer is fixed by restarting the container.
- **Compose-internal traffic allowed.** The container's own subnet (and thus the `db` service) is fully permitted.
- **Self-verification.** The script ends by asserting that a non-allowlisted domain (`https://example.com`) is unreachable and an allowlisted one (`https://api.anthropic.com`) is reachable; it exits nonzero with a loud message otherwise.

**Allowlist contents:**

| Group | Domains |
|---|---|
| Upstream defaults | `api.anthropic.com`, `statsig.anthropic.com`, `statsig.com`, `sentry.io`, `registry.npmjs.org`, GitHub IP ranges |
| Yarn | `repo.yarnpkg.com` |
| .NET / NuGet | `api.nuget.org`, `dot.net`, `builds.dotnet.microsoft.com`, `ci.dot.net`, `pkgs.dev.azure.com` |
| OAuth token verification (API outbound) | `oauth2.googleapis.com`, `www.googleapis.com`, `login.microsoftonline.com`, `graph.microsoft.com` |

Adding a domain later = one line in the script's domain list + container restart; documented in `docs/dev-container.md`.

## 4. Claude Code auth & app secrets

- **Claude credentials:** named volume `claude-config` mounted at the container user's `~/.claude`. One-time `claude` login in-container (browser flow from the host); persists across rebuilds. The host `~/.claude` is never mounted, so auto mode cannot touch real host credentials or settings.
- **dotnet user-secrets:** named volume mounted at `~/.microsoft/usersecrets`; one-time in-container setup per `docs/local-development.md`, persists across rebuilds.
- **Web secrets:** `apps/web/.env.local` is gitignored but rides along with the workspace bind mount — no new mechanism. `NEXTAUTH_URL` and OAuth redirect URIs stay `http://localhost:3000` because ports are forwarded to the host and the browser doing sign-in runs on the host.

## 5. Documentation

- New `docs/dev-container.md`: prerequisites (Docker Desktop, VS Code + Dev Containers extension), first-run steps (reopen in container → `claude` login → user-secrets → EF migrations → provision user), daily use (`claude --dangerously-skip-permissions`, run commands for API/web), firewall explanation, how to extend the allowlist, the smoke-test script (when it runs, how to run it manually), and troubleshooting (run `smoke-test.sh` first; firewall self-test failure, stale DNS → restart).
- README gets a one-line pointer to `docs/dev-container.md` in the quick-start section.

## 6. Verification & automated smoke tests

Repeatable checks are automated in a checked-in script, `.devcontainer/smoke-test.sh`, so the container can be re-verified cheaply after image rebuilds, base-image bumps, allowlist edits, or toolchain upgrades — the recurring maintenance events for this setup.

### `smoke-test.sh` (automated, run in-container)

Runs each check, prints pass/fail per check, exits nonzero if any fail:

1. **Toolchain present:** `dotnet --version` reports 8.x, `node --version` in the supported range, `yarn --version` is 4.x, `claude --version` succeeds, `dotnet-ef` is on PATH.
2. **Firewall enforcing:** `curl -m 5 https://example.com` fails; each critical allowlisted service (`api.anthropic.com`, `registry.npmjs.org`, `api.nuget.org`, `github.com`) is reachable. This extends the start-time self-test to the full allowlist, so a domain silently dropping out of the ipset (e.g. after an upstream script update) is caught.
3. **Database reachable:** TCP connect to `db:5432` and a `SELECT 1` as `da_user` succeed.
4. **API builds and tests pass:** `dotnet build` and `dotnet test` on the solution.
5. **Web installs and tests pass:** `yarn install --immutable` and the web workspace's test command.

The script needs no secrets and no interactive login, so any session (including Claude in auto mode) can run it.

**When it runs:**
- Automatically once per container creation: `postCreateCommand` runs it after the firewall is up, so a broken rebuild is caught immediately rather than mid-task.
- On demand any time (`bash .devcontainer/smoke-test.sh`), documented in `docs/dev-container.md` as the first debugging step and the required check after editing the firewall allowlist or Dockerfile.

The firewall's own self-test (section 3) additionally runs on every container start via `postStartCommand`.

### Manual, one-time-per-machine checks

Not automatable (they need human login or a host browser), so they stay a short documented checklist in `docs/dev-container.md`:

1. Claude: `claude --dangerously-skip-permissions` starts after the one-time login.
2. End-to-end sign-in: `dotnet run` + `yarn dev`, then Google/Microsoft sign-in from the host browser at `http://localhost:3000`.
3. Host workflow regression: nothing outside `.devcontainer/`, `docs/`, and the README pointer changes; host quick start behaves as before (verified once at implementation review).
