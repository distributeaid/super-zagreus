# Zagreus

DistributeAid partner portal — a monorepo containing the Needs Assessment API and the
partner-facing web app. Partner (hub/frontline) users sign in with Google or Microsoft and
manage their organisation's needs assessments; authorization (org + role) is enforced by
the API.

## What's here

| Workspace | Stack | Purpose |
|---|---|---|
| [`apps/api`](apps/api) | .NET 8, ASP.NET Core, EF Core, PostgreSQL | Needs Assessment API — auth, org/project/assessment data, OAuth ID-token verification |
| [`apps/web`](apps/web) | Next.js 16, React 19, TypeScript, Auth.js, Tailwind | Web app — Google/Microsoft sign-in and the partner dashboard |

The current slice covers end-to-end auth + a dashboard: a provisioned user signs in with
Google/Microsoft, and lands on a dashboard showing their project and its 90-day freshness
status.

## Quick start

1. **First-time toolchain + database + API** (.NET, Docker, Postgres):
   see [apps/api/README.md](apps/api/README.md).
2. **Credentials & secrets** (JWT key, Google/Microsoft OAuth, `apps/web/.env.local`,
   provisioning your sign-in email): see
   [docs/local-development.md](docs/local-development.md).

Once set up, run the three processes (each in its own terminal):

```bash
docker start da-postgres                          # database
cd apps/api/DA.NA.Api && dotnet run               # API  → http://localhost:54764
yarn workspace @zagreus/web dev                   # web  → http://localhost:3000
```

## Repository layout

```
apps/
  api/    .NET solution (DA.NA.Core / DA.NA.Api / DA.NA.Tests / …)
          tools/DA.NA.Provision — console tool to authorize a user by email
  web/    Next.js app (src/app, src/data, src/components)
docs/
  local-development.md          credentials & secrets setup
  superpowers/specs/            PRD + technical design
  superpowers/plans/            implementation plans
```

## Tooling

- **Node** 20–24, **Yarn 4** (via Corepack) — the web workspace is `@zagreus/web`.
- **.NET 8 SDK**, **Docker** (PostgreSQL 16), **dotnet-ef** for migrations.

### Common commands

```bash
# Backend
dotnet build apps/api/DistributeAid.NeedsAssessment.sln
dotnet test  apps/api/DistributeAid.NeedsAssessment.sln

# Frontend
yarn workspace @zagreus/web test
yarn workspace @zagreus/web typecheck
yarn workspace @zagreus/web build
```

## License

[AGPL-3.0-only](LICENSE).
