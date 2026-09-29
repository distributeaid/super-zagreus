# CI + Coverage Gates — Design

**Date:** 2026-07-19
**Branch:** `feat/ci-coverage-gates`
**Status:** Approved design, ready for planning

## Summary

Add Continuous Integration (no CD) to the Zagreus monorepo, centered on code
coverage that feeds three consumers:

1. **PR gate** — GitHub Actions runs both test suites with coverage and enforces
   coverage policy on every pull request.
2. **Local agent self-check** — a root `yarn` command produces the same
   machine-readable coverage summary the CI gate uses, so an agent can verify its
   own work locally before finishing, with no drift from CI.
3. **Coverage diff on PRs** — a consolidated PR comment showing patch (changed-line)
   coverage and which changed lines are uncovered.

The system is **self-contained**: no third-party SaaS (no Codecov), no token beyond
the default `GITHUB_TOKEN`, and no coverage data leaves GitHub. The same tool
(`diff-cover`) and the same `yarn` scripts run locally and in CI, so "what the agent
sees" and "what the gate enforces" are identical.

## Context

- Monorepo: `apps/api` (.NET 8 / xUnit) and `apps/web` (Next.js 16 / vitest).
- No CI exists today. Remote: `github.com/distributeaid/super-zagreus` (GitHub Actions).
- Test suites already exist and pass: 7 vitest test files (web), 13 xUnit test files (api).
- Neither suite has coverage tooling configured yet.
- Tooling present: Node 24, .NET 8.0.419, Python 3.14 (locally); `diff-cover` is a
  `pip`-installable tool CI installs on demand.

## Gate policy (decided)

**Patch coverage + total floor**, both enforced:

- **Patch gate:** changed lines must clear a threshold (`patchMin`, default **99%**).
  This is the primary signal — it asks "is the new code tested?" The default is set
  deliberately high: this is a small, test-disciplined codebase where new code is
  expected to ship with tests, so the patch gate is treated as a near-total
  requirement rather than a lenient floor. It is adjustable in one place
  (`coverage.config.json`) if a specific PR needs relief.
- **Total floor:** each suite's overall line coverage must not drop below a
  configured floor (`webTotalMin`, `apiTotalMin`). Floors are explicit, documented
  numbers seeded from the **measured current baseline**, set as high as the baseline
  allows (rounded down only a couple points for slack), not a moving target — this
  keeps the floor from blocking unrelated PRs while still ratcheting overall coverage
  upward as the strict patch gate lands well-tested new code.

All three thresholds live in a single root `coverage.config.json` — one place humans
and agents read the policy.

## Architecture

Single workflow `.github/workflows/ci.yml`, triggered on `pull_request` and on
`push` to `main` (to refresh the total-coverage baseline). Three jobs:

```
┌── web ─────────────┐   ┌── api ─────────────┐
│ setup node/yarn    │   │ setup dotnet 8     │
│ typecheck          │   │ dotnet test +      │
│ vitest --coverage  │   │   coverlet (lcov)  │
│ → upload lcov      │   │ → upload lcov      │
└─────────┬──────────┘   └─────────┬──────────┘
          └────────────┬───────────┘
                 ┌── coverage ──────────────────┐  (needs: web, api)
                 │ download both lcov artifacts │
                 │ normalize paths → repo-root  │
                 │ diff-cover: patch gate       │
                 │ floor script: total floors   │
                 │ post/update one PR comment   │
                 │ fail job if any gate fails    │
                 └──────────────────────────────┘
```

- `web` and `api` **fail on test failures** — the existing correctness gate, now
  always-on in CI.
- `coverage` adds the two coverage gates and the consolidated PR comment.
- **Concurrency guard** cancels superseded runs on the same PR ref.
- **PR comment** is posted/updated via first-party `actions/github-script` using a
  hidden marker, so re-pushes update one comment in place (no comment spam).

## Components

### Coverage instrumentation (both suites emit lcov)

A single report format (lcov) keeps the `diff-cover` invocation simple.

- **Web (vitest):**
  - Add dev dependency `@vitest/coverage-v8`.
  - `vitest.config.ts` → `coverage: { provider: 'v8', reporter: ['text', 'lcov',
    'json-summary'], reportsDirectory: './coverage' }`.
  - Output: `apps/web/coverage/lcov.info` + `coverage-summary.json`.
- **API (.NET / coverlet):**
  - Add `coverlet.collector` PackageReference to `DA.NA.Tests.csproj`.
  - A `coverlet.runsettings` forces `Format=lcov`.
  - Run via `dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings`.
  - Output normalized to `apps/api/coverage/lcov.info`.

### Path normalization (the one Approach-A wrinkle)

lcov `SF:` source paths from each tool are relative to that suite's own root. A small
step rewrites them to be **repo-root-relative** (`apps/web/...`, `apps/api/...`) so a
single `diff-cover` call and the git diff line up. This is the one contained
integration detail; the implementation plan must nail it (and prove it with the
negative test below).

### Root `yarn` scripts (shared entry — CI calls the same ones)

| Script | Does |
|---|---|
| `yarn coverage:web` | vitest run + coverage |
| `yarn coverage:api` | dotnet test + coverlet |
| `yarn coverage` | both of the above, sequentially |
| `yarn coverage:diff` | `diff-cover` patch gate + floor check vs base branch; emits markdown + JSON |

### Gate logic

- **Patch gate:**
  `diff-cover apps/web/coverage/lcov.info apps/api/coverage/lcov.info
  --compare-branch=origin/main --fail-under=<patchMin>`.
- **Total floor:** `scripts/coverage-floor.mjs` reads each suite's line-coverage %
  (from lcov / json-summary) and exits non-zero if a suite is below its floor.
- **Config:** root `coverage.config.json`:
  `{ "patchMin": 99, "webTotalMin": <baseline>, "apiTotalMin": <baseline> }`.

### Agent-friendly output

`yarn coverage:diff` writes `coverage-diff.json` with: patch coverage %, per-suite
floor pass/fail, and per-file uncovered changed lines. An agent's self-check reads
this structured result — identical to what the CI gate decided on.

## Baseline & rollout

During implementation (measured, not guessed):

1. Run `yarn coverage` on the branch to get current web + api line-coverage %.
2. Set `webTotalMin` / `apiTotalMin` a couple points below the measured numbers
   (aim high — as close to the baseline as the slack allows).
3. `patchMin` starts at 99%, adjustable in one place.

## Verification

Verify the gates actually work, not merely that CI is green:

- **Local:** `yarn coverage` and `yarn coverage:diff` produce reports and correct
  pass/fail exit codes.
- **Negative test:** a throwaway change adding an uncovered function → patch gate
  goes **red**, PR comment lists the uncovered lines.
- **Positive test:** same code with a test → **green**.
- **Comment upsert:** a second push updates the existing PR comment in place.

## Documentation

A short doc (in `docs/`, linked from README): how to read coverage locally, what the
gates mean, how an agent uses `yarn coverage:diff` in a self-check loop, and how to
adjust thresholds.

## Out of scope (YAGNI)

- CD / deployment automation.
- Codecov / any SaaS, coverage badges, hosted history dashboards.
- Mutation testing.
- Coverage on non-PR branches beyond the `main` baseline refresh.
- Ratchet automation (auto-raising floors as coverage climbs) — documented as a
  future tweak, not built now.
