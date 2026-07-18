# ADR-001: Use C# / .NET 8 as the backend language and runtime

**Status:** Accepted  
**Date:** March 2026  
**Authors:** Finn Porter, Claude (Anthropic)  
**Project:** DistributeAid Needs Assessment

---

## Context

DistributeAid is building a new Needs Assessment system to replace a series of Google Forms and spreadsheets that have outgrown their capacity. The system needs to collect, store, and track structured aid needs from frontline organisations and hubs across multiple crisis contexts.

The team needed to choose a backend language and runtime. The constraints shaping this decision were:

- **Open source, volunteer-driven development.** Contributors will vary in experience and background. The codebase needs to be approachable and well-structured enough that a new volunteer can find their way around without deep institutional knowledge.
- **No US infrastructure dependency.** DistributeAid is actively trying to reduce reliance on US-based cloud providers for political and ethical reasons. The chosen stack must run cleanly on EU-based infrastructure.
- **Performance matters.** The system will handle potentially large volumes of needs data across many organisations, with historical versioning and background staleness jobs. Interpreted runtimes are acceptable but a compiled language is preferred.
- **Existing JavaScript/TypeScript and Python codebase.** Most current DA projects use JS/TS or Python. While popular languages, they are not totally suited to the job
- **One experienced C# developer on the founding engineering team.** Not a deciding factor, but relevant for initial momentum.

Candidates considered: Go, Rust, C# / .NET 8, Node.js (TypeScript), Python (FastAPI).

---

## Decision

Use **C# with .NET 8** for the backend API and background workers.

Use **React (Vite, standalone SPA) or similar FE solution** for the frontend, deployed separately and communicating with the backend via REST API only.

---

## Reasoning

### C# runs anywhere now

.NET 8 is fully cross-platform and Linux-first. It has no dependency on Azure, Windows, or any Microsoft cloud service. The runtime runs in a standard Linux Docker container and deploys to Kubernetes on any infrastructure. The association between C# and Azure is a tooling habit, not a platform constraint.

### Compiled performance without the learning cliff

C# compiles to native code via AOT in .NET 8, or to optimised IL via the JIT. Either way it is significantly faster than Python or Node.js for CPU-bound work, and close to Go for typical web API workloads. The staleness background jobs — which will scan large numbers of assessment records and compute time deltas — benefit directly from this.

Unlike Rust, which also compiles and is fast, C# does not have a steep learning curve. The borrow checker is the single biggest barrier to Rust adoption for new contributors and would potentially drive away volunteers. C# has none of that; it uses standard garbage collection. A developer who knows Java, TypeScript, or Python can read C# code and understand it within a short time, even if they cannot write it fluently yet.

### The solution structure maps cleanly to the architecture

.NET's concept of a *solution containing multiple projects* is a modular monolith structure to benefit the system needs. The four modules (Core, Assessments, Staleness, Analytics) become four class library projects. Module boundaries are enforced by project references — if `DA.NA.Staleness` tries to reach directly into `DA.NA.Assessments`, it will not compile. This is a structural guarantee, not a convention.

```
DistributeAid.NeedsAssessment.sln
├── DA.NA.Core/          ← entities, DbContext, shared interfaces
├── DA.NA.Assessments/   ← submission lifecycle, versioning
├── DA.NA.Staleness/     ← background jobs, notifications
├── DA.NA.Analytics/     ← reporting, exports, trends
├── DA.NA.Api/           ← ASP.NET Core entry point, composition root
└── DA.NA.Tests/         ← xUnit
```

Each project is a natural extraction point if it ever needs to become its own service. The solution structure makes this path obvious to any future contributor.

### Entity Framework Core is excellent for this data model

The data model is deeply relational: organisations contain projects, projects contain versioned assessments, assessments contain items that reference a shared item catalogue with unit conversion factors. EF Core handles this well, including the self-referencing foreign keys (`supersedes_id` on assessments, `hub_of_org_id` on organisations, which prepares for giving hubs visibility on their supported groups), enum-to-string conversions, and complex LINQ queries for the staleness checks.

Running migrations, seeding reference data, and querying history chains are all straightforward with EF Core in a way that would require more manual work with a lighter ORM or raw query layer.

### Approachability for volunteers

The concern about C# being hard to learn is mostly about tooling setup and project structure, not the language itself. The language syntax is readable and explicit. By providing a well-structured solution with clear module boundaries, a README, and seed data that produces a working API on first run, new contributors have a concrete entry point. Code can always be refactored to be more performant by a more experienced developer — what matters first is that people can read it and contribute.

### Separation of frontend and backend

A deliberate choice is made to keep the React frontend as a completely separate deployment. This means frontend volunteers do not need to understand the backend stack to contribute, and backend changes do not require touching the frontend. Frameworks that blur this boundary (Next.js full-stack, Razor Pages) were explicitly ruled out to avoid messes and resposibility conflicts.

---

## Alternatives considered

### Go

Go was considered. It compiles to a single static binary, has excellent concurrency primitives for background jobs, and is fast. However:

- Nobody on the current team knows Go. AFAIK
- The community culture around single-letter variables and abbreviation-heavy code is a genuine readability concern for a volunteer-driven codebase.
- The ORM ecosystem (sqlc, GORM) is less mature than EF Core for the kind of relational queries this system needs.
- Setting up a Go project from scratch for a team unfamiliar with it creates unnecessary early friction.

Go remains a reasonable long-term option if the team composition changes significantly.

### Rust

Ruled out. The borrow checker is a meaningful barrier for contributors of mixed experience levels. The performance ceiling is higher than C# but not needed at this scale. The learning investment is not justified.

### Node.js / TypeScript

DA already uses TypeScript extensively. The argument for consistency is given. However:

- A TypeScript monolith tends toward entangled frontend/backend boundaries, especially with Next.js, which will cause maintainablilty issues fast.
- Async JavaScript is fast for I/O-bound work but less suited to the background computation the staleness module will do.
- A move toward a compiled language for this project specifically is desirable.

TypeScript likely remains the right choice for the frontend.

### Python / FastAPI

Python is well understood across the team or easy to learn. FastAPI is a good framework. However:

- Python's performance characteristics are worse than compiled alternatives for background jobs at scale.
- A move toward a compiled language for this project specifically is desirable.
- Type safety in Python, while improving, is still not as strict as C# at compile time.

---

## Consequences

### Positive

- Clean module boundaries enforced at compile time via project references.
- Strong typing catches a large class of bugs before they reach production.
- EF Core migrations give a clear, auditable history of schema changes.
- Single deployable binary (or container) at MVP; clear extraction path for individual modules later.
- Frontend contributors are fully decoupled from backend language choice.

### Negative / risks

- New volunteers unfamiliar with C# face a setup and structure learning curve. This is mitigated by a well-documented solution with a working first-run experience.
- EF Core migration management requires discipline — migrations must be committed alongside entity changes or the database and code fall out of sync. This is a minor process risk, not a technical one.
- The founding team has one C# developer. If that person steps back before others are onboarded, there is a knowledge gap. Mitigated by code clarity, documentation, and ADRs like this one.

---

## Review cadence

Revisit if: the volunteer contributor pool changes significantly toward Go or Rust expertise, or if performance profiling reveals bottlenecks that C# cannot address at reasonable cost.
