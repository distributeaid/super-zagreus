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

`/usr/local/bin/zagreus-init-firewall.sh` (source:
`.devcontainer/init-firewall.sh`) runs on every container start. Everything
outbound is dropped except DNS, the compose network, and an allowlist of
domains (Anthropic/Claude, npm, Yarn, NuGet/.NET, GitHub, VS Code, and the
Google/Microsoft OAuth token endpoints the API calls). IPs are resolved once
at start. IPv6 egress is dropped entirely. The bootstrap that rebuilds the
allowlist on every start is fail-closed throughout — there is never an
instant of open egress, even on a re-run.

- **Add a domain:** add one line to the `for domain in` list in
  `.devcontainer/init-firewall.sh`, rebuild the container, run the smoke
  test.
- **A previously working domain fails:** its IPs may have rotated since
  container start — restart the container (re-resolves everything).
- **Firewall self-test fails on start:** the container refuses to come up
  quietly; check the creation log, fix, rebuild.

The `vscode` user's only sudo right is running the `zagreus-init-firewall.sh`
script — an agent inside the container cannot disable filtering.

## One-time manual checks (not automated)

- `claude --dangerously-skip-permissions` starts after login.
- Full sign-in: run API + web, sign in with Google/Microsoft from a host
  browser at http://localhost:3000.
