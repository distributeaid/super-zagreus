# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Zagreus is the DistributeAid partner portal — a Yarn monorepo (`apps/*`) with two workspaces:

- **`apps/api`** — .NET 8 / ASP.NET Core / EF Core / PostgreSQL Needs Assessment API. Owns auth, org/project/assessment data, and OAuth ID-token verification.
- **`apps/web`** — Next.js 16 / React 19 / TypeScript / Auth.js / Tailwind web app. Google/Microsoft sign-in and the partner dashboard + needs intake.

The web app is a thin server-rendered client over the API; the API is the source of truth for authorization (org + role) and all business data.

## Commands

Backend (.NET) — run from repo root:

```bash
dotnet build apps/api/DistributeAid.NeedsAssessment.sln
dotnet test  apps/api/DistributeAid.NeedsAssessment.sln
dotnet test  apps/api/DistributeAid.NeedsAssessment.sln --filter "FullyQualifiedName~NeedsLifecycleSmokeTests"   # single test class
cd apps/api/DA.NA.Api && dotnet run                          # API → http://localhost:54764 (Swagger at root)
dotnet ef migrations add <Name> --project apps/api/DA.NA.Core --startup-project apps/api/DA.NA.Api
```

Frontend (web) — the workspace is `@zagreus/web`:

```bash
yarn workspace @zagreus/web test                             # vitest run
yarn workspace @zagreus/web test src/data/needs.test.ts      # single test file
yarn workspace @zagreus/web typecheck                        # tsc --noEmit
yarn workspace @zagreus/web build
yarn workspace @zagreus/web dev                              # → http://localhost:3000
```

Running the full stack locally needs three processes: `docker start da-postgres`, the API, and the web dev server. First-time toolchain/DB setup is in `apps/api/README.md`; secrets and OAuth setup (`apps/web/.env.local`, .NET user-secrets, provisioning your sign-in email) are in `docs/local-development.md`.

## Auth flow (spans both workspaces)

Partner users have no password — the round trip is: web app runs Google/Microsoft sign-in (Auth.js) → the provider **ID token** is exchanged for the app's own JWT via `POST /api/auth/session` (`exchangeProviderToken` in `apps/web/src/data/sessionExchange.ts`, called from the Auth.js `jwt` callback in `apps/web/src/auth.ts`) → the API verifies the ID token against the provider's signing keys and maps the **verified email** to a provisioned `User`, returning the app JWT. DA team users instead use password login (`POST /api/auth/login`). Both paths yield the same `{ token, expiresAt, user }` (8-hour expiry).

- The app JWT lives on the Auth.js session. `apps/web/src/proxy.ts` (Next.js middleware) guards `/dashboard` and `/needs` via `sessionAccess` (`apps/web/src/data/access.ts`): `allow` / `denied` (→ `/access-denied`, backend rejected the account) / re-authenticate (→ `/login`).
- `apps/web/src/data/apiClient.ts` is the **server-only** fetch layer: it reads the JWT from the session, attaches `Bearer`, and treats `401 → redirect(/login)`, `404 → null`. All backend calls go through `apiGet/apiPost/apiPatch/apiDelete`.
- **Authorization is enforced by the API, not the web app.** The JWT carries an `orgId` claim; org users only ever see their own org's data. In controllers, `User.OrgId()` (`ClaimsPrincipalExtensions`) returns the caller's org (null for DA users), and endpoints return `404` (not `403`) for cross-org access so other orgs are invisible. Role gates use the `DaUser` / `DaAdmin` / `OrgAdmin` policies defined in `apps/api/DA.NA.Api/Program.cs`.

## Assessment lifecycle (the core domain model)

A project's needs are modeled as an assessment lifecycle: a **Draft** is editable (items added/edited/removed, each change persisted immediately = "auto-save"); **submit** flips it to **Submitted** (immutable) and resets the 90-day freshness clock; the latest Submitted assessment is "current". To change confirmed needs you start a new draft.

The **working-draft endpoint** (`POST /api/projects/{projectId}/assessments/working-draft`, ADR-002) has get-or-create semantics and enforces **at most one open draft per project** (backed by a DB index, migration `AddOneOpenDraftPerProjectIndex`). It returns the existing open draft, or atomically creates one seeded with a copy of the latest Submitted assessment's items (the "living list"). The web needs-intake flow (`apps/web/src/data/needsActions.ts`, `"use server"` actions) calls it, then uses the plain item `POST/PATCH/DELETE .../items` and `submit` endpoints, calling `revalidatePath("/needs")` after each mutation. `selectNeedsMode` (`apps/web/src/data/needs.ts`) chooses editor vs. read-only view from whether a Draft exists.

## Backend project layout

`DA.NA.Core` holds entities, enums (`UserRole`, `AssessmentStatus`, etc.), the `AppDbContext`, migrations, and seed data. `DA.NA.Api` is the ASP.NET entry point (controllers, auth, Swagger). `DA.NA.Assessments`, `DA.NA.Staleness`, `DA.NA.Analytics` are **placeholders** for future extraction. `DA.NA.Tests` is xUnit. `tools/DA.NA.Provision` is a console tool that authorizes a user by email.

Reference data (units, item types) is seeded on startup with **stable fixed GUIDs** (e.g. unit "item" = `11111111-...`) — usable directly in requests/tests. `GET /api/categories` and `GET /api/units` are the only unauthenticated endpoints.

## Conventions (enforced in review — see `docs/style-guide.md`)

- **Backend: never return tracked EF entities from controller actions.** Always project to an anonymous object/DTO via `.Select(...)`. Tracked entity graphs serialize their EF back-references into cycles and throw a 500 *after* the DB write already succeeded (looks like a client bug). Existing controllers show the pattern.
- **Every frontend-called endpoint gets an integration test through the real HTTP pipeline** (`TestBase`/`ApiFactory`) that asserts the status code AND deserializes the response body — mocked-HTTP unit tests can't catch response-serialization bugs. Multi-step flows get a lifecycle smoke test (see `DA.NA.Tests/Assessments/NeedsLifecycleSmokeTests.cs`).
- Integration tests boot the real API in memory (`ApiFactory : WebApplicationFactory<Program>`), swapping PostgreSQL for one shared open SQLite in-memory connection; environment `"Testing"` makes `Program.cs` skip `SeedData` so tests seed exactly what they need.
- `apps/api/Directory.Build.props` promotes specific compiler warnings to errors (CS4014 unawaited async, CS1998, CS0162/0168/0219 dead code) — a build fails on them.
- **TypeScript:** JSDoc docstring on every exported function/constant; all `import`s at the top of the file (in tests that mock modules, use `vi.hoisted(...)` so real imports stay at top). **Tests:** Vitest, use `it` not `test`, every case inside a `describe` named for the unit under test.

## Reference docs

- `docs/local-development.md` — credentials & secrets setup
- `docs/style-guide.md` — full code/test conventions
- `apps/api/README.md` — first-time .NET/Docker/Postgres setup, Bruno collection, fixed unit GUIDs
- `apps/api/docs/auth.md` — roles, provisioning, token handling
- `apps/api/docs/adr/` — architecture decision records
- `apps/api/devtools/bruno/` — Bruno API client collection (one `.bru` per endpoint; add one when adding an endpoint)
