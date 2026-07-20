# CI + Coverage Gates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add self-contained CI (no CD) that runs both test suites with coverage on every PR, enforces patch-coverage + total-floor gates, posts one consolidated PR comment, and exposes the same checks as root `yarn` scripts for local agent self-checks.

**Architecture:** Both suites emit lcov. A small pure-function library normalizes lcov source paths to repo-root-relative so a single `diff-cover` call lines up with the git diff. Root `yarn` scripts orchestrate coverage locally; a GitHub Actions workflow runs the same scripts across parallel `web`/`api` jobs feeding one `coverage` aggregation job.

**Tech Stack:** GitHub Actions, vitest + `@vitest/coverage-v8`, .NET 8 xUnit + `coverlet.collector` (lcov), `diff-cover` (Python), Node's built-in `node:test` runner (no new root dependency).

## Global Constraints

- **No CD / deployment** — CI only.
- **Self-contained** — no Codecov or any SaaS; no secret beyond the default `GITHUB_TOKEN`; no coverage data leaves GitHub.
- **One coverage format** — both suites emit **lcov**; `diff-cover` consumes it.
- **Single policy file** — all thresholds live in root `coverage.config.json`: `{ "patchMin", "webTotalMin", "apiTotalMin" }`. `patchMin` = **80**.
- **Same commands locally and in CI** — CI calls the exact root `yarn` scripts an agent runs locally.
- **Node** `>=20 <25` (repo engines); package manager **yarn 4.12** (`nodeLinker: node-modules`).
- **Web workspace:** `@zagreus/web` at `apps/web`. **API test project:** `apps/api/DA.NA.Tests/DA.NA.Tests.csproj`. **API solution:** `apps/api/DistributeAid.NeedsAssessment.sln`.
- **API product projects** (coverage scope, exclude everything else): `DA.NA.Api`, `DA.NA.Core`, `DA.NA.Assessments`, `DA.NA.Staleness`, `DA.NA.Analytics`, plus `tools/DA.NA.Provision`. Exclude the test project and EF `Migrations`.
- Spec: `docs/superpowers/specs/2026-07-19-ci-coverage-gates-design.md`.

---

### Task 1: Web coverage instrumentation (vitest → lcov)

**Files:**
- Modify: `apps/web/package.json` (devDependency)
- Modify: `apps/web/vitest.config.mts` (add `coverage` block)

**Interfaces:**
- Consumes: nothing.
- Produces: `apps/web/coverage/lcov.info` (lcov) and `apps/web/coverage/coverage-summary.json` when the web suite runs with coverage.

- [ ] **Step 1: Add the coverage provider dependency**

```bash
yarn workspace @zagreus/web add -D @vitest/coverage-v8@^3.2.7
```

- [ ] **Step 2: Add the coverage config to `apps/web/vitest.config.mts`**

Add a `coverage` key inside the existing `test: { ... }` object (keep the existing `environment`, `globals`, `setupFiles`, `include` keys as-is):

```ts
    coverage: {
      provider: "v8",
      reporter: ["text", "lcov", "json-summary"],
      reportsDirectory: "./coverage",
      all: true,
      include: ["src/**/*.{ts,tsx}"],
      exclude: [
        "src/**/*.test.{ts,tsx}",
        "src/**/*.d.ts",
        "src/**/types.ts",
      ],
    },
```

- [ ] **Step 3: Run the web suite with coverage**

Run: `yarn workspace @zagreus/web test --coverage`
Expected: tests pass; a text coverage table prints; `apps/web/coverage/lcov.info` and `apps/web/coverage/coverage-summary.json` are created.

- [ ] **Step 4: Verify the lcov file has source records**

Run: `grep -c '^SF:' apps/web/coverage/lcov.info`
Expected: a number ≥ 1 (one `SF:` record per instrumented source file).

- [ ] **Step 5: Ignore generated coverage output**

Add to `apps/web/.gitignore` (create the file if absent):

```
/coverage
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/package.json apps/web/vitest.config.mts apps/web/.gitignore yarn.lock
git commit -m "test(web): emit lcov coverage from vitest"
```

---

### Task 2: API coverage instrumentation (.NET/coverlet → lcov)

**Files:**
- Modify: `apps/api/DA.NA.Tests/DA.NA.Tests.csproj` (add `coverlet.collector`)
- Create: `apps/api/coverlet.runsettings`

**Interfaces:**
- Consumes: nothing.
- Produces: `apps/api/coverage/lcov.info` (lcov) after the API suite runs with coverage. Paths in this file are **absolute** at this stage (normalized in Task 4).

- [ ] **Step 1: Add the coverlet collector package**

Add this `PackageReference` inside the existing `<ItemGroup>` that lists the other test packages in `apps/api/DA.NA.Tests/DA.NA.Tests.csproj`:

```xml
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
```

- [ ] **Step 2: Create `apps/api/coverlet.runsettings`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>lcov</Format>
          <Exclude>[DA.NA.Tests]*</Exclude>
          <ExcludeByAttribute>GeneratedCodeAttribute</ExcludeByAttribute>
          <ExcludeByFile>**/Migrations/*.cs</ExcludeByFile>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

- [ ] **Step 3: Run the API suite with coverage into a fixed location**

Run (from repo root):

```bash
dotnet test apps/api/DistributeAid.NeedsAssessment.sln \
  --collect:"XPlat Code Coverage" \
  --settings apps/api/coverlet.runsettings \
  --results-directory apps/api/coverage/raw
```

Expected: tests pass; a file `apps/api/coverage/raw/<guid>/coverage.info` is produced (lcov format is named `coverage.info`).

- [ ] **Step 4: Copy the produced lcov to the fixed path**

Run:

```bash
cp "$(find apps/api/coverage/raw -name 'coverage.info' | head -1)" apps/api/coverage/lcov.info
grep -c '^SF:' apps/api/coverage/lcov.info
```

Expected: prints a number ≥ 1. (Step 4 is temporary manual glue; Task 4 automates the find+copy in the `coverage:api` script.)

- [ ] **Step 5: Ignore generated coverage output**

Add to `apps/api/.gitignore` (create if absent):

```
/coverage
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/DA.NA.Tests/DA.NA.Tests.csproj apps/api/coverlet.runsettings apps/api/.gitignore
git commit -m "test(api): emit lcov coverage from coverlet"
```

---

### Task 3: lcov library — path normalization + line-coverage math (TDD)

**Files:**
- Create: `scripts/lib/lcov.mjs`
- Test: `scripts/lib/lcov.test.mjs`

**Interfaces:**
- Consumes: nothing.
- Produces (imported by Tasks 4 and 5):
  - `normalizeLcovPaths(lcov: string, suiteDir: string, repoRoot: string): string` — rewrites every `SF:` record so its path is relative to `repoRoot`. Absolute `SF:` paths are made relative to `repoRoot`; relative ones are first resolved against `suiteDir`. All other lines pass through unchanged.
  - `computeLineCoverage(lcov: string): { found: number, hit: number, pct: number }` — sums `LF:` (lines found) and `LH:` (lines hit) across all records; `pct = found === 0 ? 100 : (hit / found) * 100`.

- [ ] **Step 1: Write the failing tests**

Create `scripts/lib/lcov.test.mjs`:

```js
import { test } from "node:test";
import assert from "node:assert/strict";
import { normalizeLcovPaths, computeLineCoverage } from "./lcov.mjs";

test("normalizeLcovPaths rewrites a relative SF path to repo-root-relative", () => {
  const input = "SF:src/data/me.ts\nDA:1,1\nend_of_record\n";
  const out = normalizeLcovPaths(input, "/repo/apps/web", "/repo");
  assert.equal(out, "SF:apps/web/src/data/me.ts\nDA:1,1\nend_of_record\n");
});

test("normalizeLcovPaths rewrites an absolute SF path to repo-root-relative", () => {
  const input = "SF:/repo/apps/api/DA.NA.Core/Foo.cs\nend_of_record\n";
  const out = normalizeLcovPaths(input, "/repo/apps/api", "/repo");
  assert.equal(out, "SF:apps/api/DA.NA.Core/Foo.cs\nend_of_record\n");
});

test("normalizeLcovPaths leaves non-SF lines untouched", () => {
  const input = "TN:\nLF:10\nLH:7\n";
  assert.equal(normalizeLcovPaths(input, "/repo/apps/web", "/repo"), input);
});

test("computeLineCoverage sums LF/LH and computes pct", () => {
  const input = "SF:a\nLF:10\nLH:8\nend_of_record\nSF:b\nLF:10\nLH:2\nend_of_record\n";
  assert.deepEqual(computeLineCoverage(input), { found: 20, hit: 10, pct: 50 });
});

test("computeLineCoverage returns 100 pct when no lines found", () => {
  assert.deepEqual(computeLineCoverage(""), { found: 0, hit: 0, pct: 100 });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `node --test scripts/lib/`
Expected: FAIL — `Cannot find module ... lcov.mjs` (module not created yet).

- [ ] **Step 3: Implement `scripts/lib/lcov.mjs`**

```js
import path from "node:path";

/**
 * Rewrite every `SF:` record so its path is relative to `repoRoot`.
 * Absolute paths are made relative to repoRoot directly; relative paths
 * are resolved against `suiteDir` first. All other lines pass through.
 */
export function normalizeLcovPaths(lcov, suiteDir, repoRoot) {
  return lcov
    .split("\n")
    .map((line) => {
      if (!line.startsWith("SF:")) return line;
      const p = line.slice(3);
      const abs = path.isAbsolute(p) ? p : path.resolve(suiteDir, p);
      const rel = path.relative(repoRoot, abs).split(path.sep).join("/");
      return `SF:${rel}`;
    })
    .join("\n");
}

/** Sum LF/LH across all records; pct is 100 when no lines were found. */
export function computeLineCoverage(lcov) {
  let found = 0;
  let hit = 0;
  for (const line of lcov.split("\n")) {
    if (line.startsWith("LF:")) found += Number(line.slice(3));
    else if (line.startsWith("LH:")) hit += Number(line.slice(3));
  }
  const pct = found === 0 ? 100 : (hit / found) * 100;
  return { found, hit, pct };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `node --test scripts/lib/`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add scripts/lib/lcov.mjs scripts/lib/lcov.test.mjs
git commit -m "feat(ci): add lcov path-normalization + line-coverage library"
```

---

### Task 4: Root orchestration — normalize CLI, config, and `coverage` scripts

**Files:**
- Create: `scripts/normalize-lcov.mjs`
- Create: `coverage.config.json`
- Modify: `package.json` (root `scripts`)

**Interfaces:**
- Consumes: `normalizeLcovPaths` from `scripts/lib/lcov.mjs` (Task 3); `apps/web/coverage/lcov.info` (Task 1); `apps/api/coverage/raw/**/coverage.info` (Task 2).
- Produces: repo-root-relative `apps/web/coverage/lcov.info` and `apps/api/coverage/lcov.info`; the `yarn coverage`, `yarn coverage:web`, `yarn coverage:api`, and `yarn test:scripts` commands.

- [ ] **Step 1: Create `scripts/normalize-lcov.mjs` (CLI wrapper)**

```js
#!/usr/bin/env node
// Usage: node scripts/normalize-lcov.mjs <lcov-file> <suite-dir>
// Rewrites SF: paths in <lcov-file> in place, relative to repo root (cwd).
import fs from "node:fs";
import path from "node:path";
import { normalizeLcovPaths } from "./lib/lcov.mjs";

const [file, suiteDir] = process.argv.slice(2);
if (!file || !suiteDir) {
  console.error("usage: normalize-lcov.mjs <lcov-file> <suite-dir>");
  process.exit(2);
}
const repoRoot = process.cwd();
const abs = path.resolve(repoRoot, file);
const out = normalizeLcovPaths(fs.readFileSync(abs, "utf8"), path.resolve(repoRoot, suiteDir), repoRoot);
fs.writeFileSync(abs, out);
console.log(`normalized ${file} relative to ${repoRoot}`);
```

- [ ] **Step 2: Create `coverage.config.json` with initial thresholds**

`webTotalMin`/`apiTotalMin` are seeded from real measurements in Task 5, Step 1. Start with a low placeholder floor of `0` so nothing fails before the baseline is measured:

```json
{
  "patchMin": 80,
  "webTotalMin": 0,
  "apiTotalMin": 0
}
```

- [ ] **Step 3: Add root scripts to `package.json`**

Add a `"scripts"` block to the root `package.json` (it currently has none), keeping existing top-level keys intact:

```json
  "scripts": {
    "test:scripts": "node --test scripts/",
    "coverage:web": "yarn workspace @zagreus/web test --coverage && node scripts/normalize-lcov.mjs apps/web/coverage/lcov.info apps/web",
    "coverage:api": "rm -rf apps/api/coverage/raw && dotnet test apps/api/DistributeAid.NeedsAssessment.sln --collect:\"XPlat Code Coverage\" --settings apps/api/coverlet.runsettings --results-directory apps/api/coverage/raw && node scripts/copy-api-lcov.mjs && node scripts/normalize-lcov.mjs apps/api/coverage/lcov.info apps/api",
    "coverage": "yarn coverage:web && yarn coverage:api"
  },
```

- [ ] **Step 4: Create `scripts/copy-api-lcov.mjs`** (replaces the Task 2 manual find+copy)

```js
#!/usr/bin/env node
// Copies the coverlet-produced coverage.info to apps/api/coverage/lcov.info.
import fs from "node:fs";
import path from "node:path";

const rawDir = "apps/api/coverage/raw";
function findInfo(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      const found = findInfo(full);
      if (found) return found;
    } else if (entry.name === "coverage.info") {
      return full;
    }
  }
  return null;
}
const src = findInfo(rawDir);
if (!src) {
  console.error(`no coverage.info under ${rawDir}`);
  process.exit(1);
}
fs.copyFileSync(src, "apps/api/coverage/lcov.info");
console.log(`copied ${src} -> apps/api/coverage/lcov.info`);
```

- [ ] **Step 5: Run the full local coverage build**

Run: `yarn coverage`
Expected: both suites run and pass; `apps/web/coverage/lcov.info` and `apps/api/coverage/lcov.info` both exist.

- [ ] **Step 6: Verify both lcov files are repo-root-relative**

Run: `grep -h '^SF:' apps/web/coverage/lcov.info apps/api/coverage/lcov.info | grep -v '^SF:apps/' | head`
Expected: **no output** (every `SF:` path starts with `apps/`). If any line prints, normalization is wrong — fix before committing.

- [ ] **Step 7: Commit**

```bash
git add scripts/normalize-lcov.mjs scripts/copy-api-lcov.mjs coverage.config.json package.json
git commit -m "feat(ci): add root coverage scripts with repo-relative lcov normalization"
```

---

### Task 5: Coverage gates — total-floor script + `coverage:diff` (patch gate)

**Files:**
- Create: `scripts/coverage-floor.mjs`
- Test: `scripts/coverage-floor.test.mjs`
- Modify: `coverage.config.json` (seed real baselines)
- Modify: `package.json` (add `coverage:diff`)

**Interfaces:**
- Consumes: `computeLineCoverage` from `scripts/lib/lcov.mjs` (Task 3); `coverage.config.json`; both normalized lcov files (Task 4).
- Produces: `coverage-floor.md` (markdown summary) and a nonzero exit code when a suite is below its floor; the `yarn coverage:diff` command that runs the patch gate (`diff-cover`) then the floor gate.

- [ ] **Step 1: Write the failing test for the floor evaluator**

Create `scripts/coverage-floor.test.mjs`. It tests a pure `evaluateFloors` export (the CLI wraps it):

```js
import { test } from "node:test";
import assert from "node:assert/strict";
import { evaluateFloors } from "./coverage-floor.mjs";

const config = { webTotalMin: 70, apiTotalMin: 80 };

test("passes when both suites meet their floor", () => {
  const web = "SF:a\nLF:10\nLH:8\nend_of_record\n"; // 80%
  const api = "SF:b\nLF:10\nLH:9\nend_of_record\n"; // 90%
  const r = evaluateFloors(web, api, config);
  assert.equal(r.ok, true);
});

test("fails when a suite is below its floor", () => {
  const web = "SF:a\nLF:10\nLH:5\nend_of_record\n"; // 50% < 70
  const api = "SF:b\nLF:10\nLH:9\nend_of_record\n"; // 90%
  const r = evaluateFloors(web, api, config);
  assert.equal(r.ok, false);
  assert.equal(r.results.find((x) => x.suite === "web").ok, false);
  assert.equal(r.results.find((x) => x.suite === "api").ok, true);
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `node --test scripts/coverage-floor.test.mjs`
Expected: FAIL — cannot import `evaluateFloors`.

- [ ] **Step 3: Implement `scripts/coverage-floor.mjs`**

```js
#!/usr/bin/env node
import fs from "node:fs";
import { computeLineCoverage } from "./lib/lcov.mjs";

/** Pure: evaluate each suite's line coverage against its configured floor. */
export function evaluateFloors(webLcov, apiLcov, config) {
  const results = [
    { suite: "web", pct: computeLineCoverage(webLcov).pct, floor: config.webTotalMin },
    { suite: "api", pct: computeLineCoverage(apiLcov).pct, floor: config.apiTotalMin },
  ].map((r) => ({ ...r, ok: r.pct >= r.floor }));
  return { ok: results.every((r) => r.ok), results };
}

function markdown({ results }) {
  const rows = results
    .map((r) => `| ${r.suite} | ${r.pct.toFixed(1)}% | ${r.floor}% | ${r.ok ? "✅" : "❌"} |`)
    .join("\n");
  return `### Total coverage floor\n\n| Suite | Coverage | Floor | |\n|---|---|---|---|\n${rows}\n`;
}

// CLI: node scripts/coverage-floor.mjs
if (import.meta.url === `file://${process.argv[1]}`) {
  const config = JSON.parse(fs.readFileSync("coverage.config.json", "utf8"));
  const web = fs.readFileSync("apps/web/coverage/lcov.info", "utf8");
  const api = fs.readFileSync("apps/api/coverage/lcov.info", "utf8");
  const result = evaluateFloors(web, api, config);
  const md = markdown(result);
  fs.writeFileSync("coverage-floor.md", md);
  console.log(md);
  if (!result.ok) {
    console.error("Total coverage floor not met.");
    process.exit(1);
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `node --test scripts/coverage-floor.test.mjs`
Expected: PASS (2 tests).

- [ ] **Step 5: Measure the real baseline and seed the floors**

Run: `yarn coverage` then:

```bash
node -e 'import("./scripts/lib/lcov.mjs").then(m=>{const fs=require("fs");for(const [s,f] of [["web","apps/web/coverage/lcov.info"],["api","apps/api/coverage/lcov.info"]])console.log(s, m.computeLineCoverage(fs.readFileSync(f,"utf8")).pct.toFixed(1))})'
```

Take each printed percentage, subtract ~2 points of slack, floor to an integer, and write those into `coverage.config.json` as `webTotalMin` and `apiTotalMin`. Example: web prints `74.6` → `webTotalMin: 72`.

- [ ] **Step 6: Add the `coverage:diff` script to root `package.json`**

Add inside the `scripts` block from Task 4:

```json
    "coverage:diff": "diff-cover apps/web/coverage/lcov.info apps/api/coverage/lcov.info --compare-branch=origin/main --fail-under=$(node -p \"require('./coverage.config.json').patchMin\") --markdown-report coverage-diff.md --json-report coverage-diff.json && node scripts/coverage-floor.mjs",
```

- [ ] **Step 7: Verify the gate end-to-end locally**

Prerequisite (local only): `pipx install diff-cover` (or `pip install --user diff-cover`).

Run: `yarn coverage && yarn coverage:diff`
Expected: `coverage-diff.md`, `coverage-diff.json`, and `coverage-floor.md` are produced; the command exits `0` when the current branch meets patch + floor thresholds.

- [ ] **Step 8: Ignore generated top-level reports**

Add to root `.gitignore`:

```
/coverage-diff.md
/coverage-diff.json
/coverage-floor.md
```

- [ ] **Step 9: Commit**

```bash
git add scripts/coverage-floor.mjs scripts/coverage-floor.test.mjs coverage.config.json package.json .gitignore
git commit -m "feat(ci): add total-floor gate and coverage:diff patch gate"
```

---

### Task 6: GitHub Actions workflow (parallel web/api → coverage aggregation + PR comment)

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `yarn coverage:web`, `yarn coverage:api`, `yarn coverage:diff`, `yarn test:scripts` (Tasks 4–5); `coverage.config.json`.
- Produces: a PR check that fails on test failure, patch-coverage shortfall, or floor shortfall; one upserted PR comment combining `coverage-diff.md` + `coverage-floor.md`.

- [ ] **Step 1: Create `.github/workflows/ci.yml`**

```yaml
name: CI

on:
  pull_request:
  push:
    branches: [main]

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  web:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 24
      - run: corepack enable
      - run: yarn install --immutable
      - run: yarn test:scripts
      - run: yarn workspace @zagreus/web typecheck
      - run: yarn coverage:web
      - uses: actions/upload-artifact@v4
        with:
          name: lcov-web
          path: apps/web/coverage/lcov.info

  api:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - uses: actions/setup-node@v4
        with:
          node-version: 24
      - run: corepack enable
      - run: yarn coverage:api
      - uses: actions/upload-artifact@v4
        with:
          name: lcov-api
          path: apps/api/coverage/lcov.info

  coverage:
    needs: [web, api]
    runs-on: ubuntu-latest
    permissions:
      contents: read
      pull-requests: write
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - uses: actions/setup-node@v4
        with:
          node-version: 24
      - uses: actions/setup-python@v5
        with:
          python-version: "3.12"
      - run: corepack enable
      - run: pip install diff-cover
      - uses: actions/download-artifact@v4
        with:
          name: lcov-web
          path: apps/web/coverage
      - uses: actions/download-artifact@v4
        with:
          name: lcov-api
          path: apps/api/coverage
      - name: Ensure base branch ref is present for diff-cover
        run: git fetch --no-tags --depth=0 origin main
      - name: Run coverage gates
        id: gates
        run: yarn coverage:diff
      - name: Build PR comment body
        if: always() && github.event_name == 'pull_request'
        run: |
          echo "<!-- coverage-report -->" > comment.md
          echo "## Coverage report" >> comment.md
          cat coverage-diff.md >> comment.md 2>/dev/null || echo "_no patch coverage report_" >> comment.md
          cat coverage-floor.md >> comment.md 2>/dev/null || true
      - name: Upsert PR comment
        if: always() && github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require("fs");
            const body = fs.readFileSync("comment.md", "utf8");
            const marker = "<!-- coverage-report -->";
            const { owner, repo } = context.repo;
            const issue_number = context.payload.pull_request.number;
            const { data: comments } = await github.rest.issues.listComments({ owner, repo, issue_number });
            const existing = comments.find((c) => c.body.includes(marker));
            if (existing) {
              await github.rest.issues.updateComment({ owner, repo, comment_id: existing.id, body });
            } else {
              await github.rest.issues.createComment({ owner, repo, issue_number, body });
            }
```

Notes for the implementer:
- `yarn install --immutable` runs once in `web`; the `api` job needs Node only for the `copy-api-lcov`/`normalize-lcov` scripts inside `coverage:api`, so it does a `setup-node` but relies on `dotnet` for the tests. If `coverage:api` reports missing packages, add `- run: yarn install --immutable` to the `api` job before `yarn coverage:api`.
- The `coverage` step runs `yarn coverage:diff`, which itself calls `diff-cover` and the floor script; a nonzero exit **fails the check**, but `always()` still posts the comment so reviewers see why.

- [ ] **Step 2: Validate the workflow YAML locally**

Run: `python3 -c "import yaml, sys; yaml.safe_load(open('.github/workflows/ci.yml')); print('ok')"`
Expected: prints `ok`. (If PyYAML is missing, run `pip install pyyaml` first, or skip and rely on the push in Step 3 to surface parse errors.)

- [ ] **Step 3: Commit and push to update the existing PR branch**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add coverage-gated PR workflow"
git push
```

- [ ] **Step 4: Verify the workflow runs green on the real PR**

Run: `gh pr checks --watch`
Expected: `web`, `api`, and `coverage` jobs all succeed; a "Coverage report" comment appears on the PR.

- [ ] **Step 5: Negative test — prove the patch gate fails on uncovered code**

Create a throwaway uncovered function, commit, push, and confirm the gate goes red:

```bash
cat > apps/web/src/uncovered-probe.ts <<'EOF'
export function probe(n: number): number {
  if (n > 0) return n * 2;
  return -1;
}
EOF
git add apps/web/src/uncovered-probe.ts
git commit -m "test: temporary uncovered probe (revert)"
git push
gh pr checks --watch
```

Expected: the `coverage` check **fails**; the PR comment lists `apps/web/src/uncovered-probe.ts` uncovered lines under patch coverage.

- [ ] **Step 6: Revert the probe and confirm green returns**

```bash
git rm apps/web/src/uncovered-probe.ts
git commit -m "Revert: remove uncovered probe"
git push
gh pr checks --watch
```

Expected: the `coverage` check passes again; the PR comment updated **in place** (still a single coverage comment).

---

### Task 7: Documentation

**Files:**
- Create: `docs/ci.md`
- Modify: `README.md` (link the new doc)

**Interfaces:**
- Consumes: the scripts and gates from Tasks 4–6.
- Produces: contributor + agent-facing docs.

- [ ] **Step 1: Write `docs/ci.md`**

```markdown
# CI & coverage

CI runs on every pull request via `.github/workflows/ci.yml`: parallel `web` and
`api` jobs run the test suites with coverage, and a `coverage` job enforces the
gates and posts a single PR comment.

## Gates

- **Patch coverage** — changed lines must meet `patchMin` (see `coverage.config.json`).
  Enforced by `diff-cover`.
- **Total floor** — each suite's overall line coverage must stay at or above its
  floor (`webTotalMin` / `apiTotalMin`). Floors are seeded from the measured
  baseline and raised deliberately.

## Running locally (same commands CI uses)

```bash
yarn coverage        # run both suites with coverage → lcov
yarn coverage:diff   # patch gate (diff-cover) + total floor; writes coverage-diff.json
```

`coverage:diff` requires `diff-cover` on PATH: `pipx install diff-cover`.

## Agent self-check loop

`yarn coverage:diff` writes machine-readable `coverage-diff.json` (patch %, per-file
uncovered lines) and exits nonzero when a gate fails — the same result CI enforces.
An agent runs it before finishing work and reads the JSON to see exactly which
changed lines still need tests.

## Adjusting thresholds

Edit `coverage.config.json`. `patchMin` = 80 by default. Raise the floors as
coverage climbs (the "ratchet"); never lower them to make a PR pass.
```

- [ ] **Step 2: Link it from `README.md`**

Under the `## Conventions` section (or the repository layout notes), add:

```markdown
- **CI & coverage:** see [docs/ci.md](docs/ci.md) — PR gates and local coverage commands.
```

- [ ] **Step 3: Commit and push**

```bash
git add docs/ci.md README.md
git commit -m "docs: document CI coverage gates and local commands"
git push
```

- [ ] **Step 4: Final check — the PR is green and documented**

Run: `gh pr checks`
Expected: all checks pass. The PR now contains the full CI + coverage implementation.
```
