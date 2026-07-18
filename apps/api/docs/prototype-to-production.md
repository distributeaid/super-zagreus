# Prototype to Production — Known Gaps

This file tracks deliberate shortcuts taken during prototyping.
Before this codebase moves toward a production system, each item here should be addressed.

---

## Architecture

### No service layer

Controllers talk directly to `AppDbContext`. Business logic lives in controller methods.

**Why it matters:** Reusing logic across controllers requires copy-paste. Testing business rules requires going through HTTP. Complex operations that touch multiple aggregates become hard to reason about.

**What to do:** Introduce a service layer (e.g. `OrganisationService`, `AssessmentService`) between controllers and EF Core. Controllers become thin — validate input, call service, return result. Services own the business rules.

### Logic is in DA.NA.Api, not in the feature projects

`DA.NA.Assessments`, `DA.NA.Staleness`, and `DA.NA.Analytics` are currently empty placeholders. All domain logic sits in `DA.NA.Api`.

**What to do:** As each area grows, migrate its logic into the appropriate project and have `DA.NA.Api` depend on it. The boundary is: `DA.NA.Api` handles HTTP concerns (routing, auth, request parsing, response shaping). Feature projects handle domain logic.

---

## Data

### Hard deletes everywhere

`DELETE` endpoints remove rows permanently. There is no audit trail of what was deleted or by whom.

**What to do:** Add a `DeletedAt` timestamp and `IsDeleted` flag to entities that need it. Filter deleted records from all queries by default. Consider whether assessments should be deletable at all once submitted.

### No pagination

All list endpoints return every row. This will cause problems as data grows.

**What to do:** Add `?page=1&pageSize=50` parameters (or cursor-based pagination) to all list endpoints. Return a wrapper with `{ items, totalCount, page, pageSize }`.

### No audit trail on writes

There is a `CreatedAt` and a `CreatedBy` on assessments, but nothing records who last updated a record or when.

**What to do:** Add `UpdatedAt` and `UpdatedBy` to entities where it matters. Consider an EF Core `SaveChanges` interceptor to populate these automatically.

---

## Auth & Security

### No password reset

A DA admin must delete and recreate a user if they lose access.

**What to do:** Implement a reset flow — email a time-limited reset token, allow the user to set a new password without logging in.

### No refresh tokens

Tokens expire after 8 hours. Users must log in again.

**What to do:** Issue a short-lived access token (15–30 min) and a long-lived refresh token. Clients exchange the refresh token for a new access token without re-entering credentials.

### Auth endpoints have no rate limiting

`POST /api/auth/login` can be called unlimited times. Brute-force attacks are unconstrained.

**What to do:** Add rate limiting (ASP.NET Core has built-in rate limiting middleware as of .NET 7). Lock an account temporarily after N failed attempts.

### CORS is fully open

`AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` is intentional for the prototype but must be locked down.

**What to do:** Replace with an explicit allow-list of origins matching the real frontend domain(s).

---

## API design

### No input length limits

String fields have `[Required]` guards but no maximum length constraints. A caller could pass an arbitrarily long string.

**What to do:** Add `[MaxLength(N)]` to all string fields, matching whatever the database column limits are (or define column limits in EF Core if not already set).

### No versioning

There is a single `/api/` prefix with no version segment.

**What to do:** Add `/api/v1/` routing and use ASP.NET Core API versioning (`Asp.Versioning.Http`) before any external consumers depend on it.

---

## Linting and code style

### Consider relaxing some build-error rules

`Directory.Build.props` promotes five compiler warnings to build errors (CS4014, CS1998, CS0162, CS0168, CS0219). These are the right defaults for a team learning the codebase — they catch bugs before code review.

As the team grows more experienced we may want to revisit:

- **CS0168 / CS0219** (unused variable / assigned but never read) — occasionally noisy in test code or when intentionally discarding values. The modern C# alternative is the discard pattern (`_ = someCall()`) which silences these cleanly.
- **CS1998** (async without await) — legitimate in interface implementations where a sync result is intentional. The fix is `return Task.CompletedTask` with no `async` keyword, but this can look odd to newcomers.

Do not relax CS4014 (missing await) — that one is always a bug.

---

## Testing

### SQLite in tests, PostgreSQL in production

The test suite uses SQLite in-memory to avoid needing a running database. SQLite and PostgreSQL handle some things differently (e.g. case-sensitivity in `LIKE`, some date functions, certain constraints).

**What to do:** For the level of queries currently in this codebase the risk is low. As queries grow more complex, consider adding a separate integration test suite that runs against a real PostgreSQL instance (Testcontainers is the standard approach — spins up a Docker container per test run).

### No unit tests yet

All current tests are integration tests that go through HTTP.

**What to do:** Add unit tests for pure business logic once it moves into a service layer. Unit tests are faster and more targeted; integration tests confirm the wiring is correct. Both have a role.
