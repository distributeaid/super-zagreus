# CI + Coverage Gates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add GitHub Actions CI (no CD) to the Zagreus monorepo that runs both test suites with coverage and enforces a strict patch-coverage gate plus per-suite total floors, with the exact same checks runnable locally.

**Architecture:** Two parallel jobs (`web` via vitest+v8, `api` via `dotnet test`+coverlet) each emit an lcov report and upload it as an artifact. A third `coverage` job (needs both) downloads them, normalizes source paths to repo-root-relative, runs `diff-cover` for the patch gate and a floor script for per-suite totals, and upserts one consolidated PR comment. The identical gate logic is exposed through root `yarn` scripts so an agent verifies its own work locally with zero drift from CI.

**Tech Stack:** GitHub Actions, Yarn 4 workspaces, Node 24 (ESM `.mjs` scripts), vitest 2 + `@vitest/coverage-v8`, .NET 8 + `coverlet.collector`, Python `diff-cover`, lcov as the single interchange format.

## Global Constraints

- **Self-contained:** no third-party SaaS (no Codecov), no token beyond the default `GITHUB_TOKEN`, no coverage data leaves GitHub. (spec: Summary)
- **`patchMin` default = 99** — changed lines are a near-total requirement; adjustable only in `coverage.config.json`. (spec: Gate policy)
- **Total floors** (`webTotalMin`, `apiTotalMin`) are explicit numbers seeded from the **measured** baseline, set as high as the baseline allows (rounded down a couple points). Never guessed. (spec: Gate policy, Baseline & rollout)
- **Single report format: lcov** for both suites — keeps one `diff-cover` invocation and one floor parser. (spec: Components)
- **Same scripts local and in CI:** CI calls the identical root `yarn` scripts; "what the agent sees" == "what the gate enforces". (spec: Summary, Root yarn scripts)
- **One PR comment**, upserted in place via a hidden marker using first-party `actions/github-script` — no comment spam. (spec: Architecture, PR comment)
- **Node** `>=20 <25` (root `engines`); CI pins Node 24. **Yarn** 4.12.0 via Corepack. **.NET** 8. (root `package.json`)
- Coverage output dirs are already git-ignored (`**/coverage`). Do not commit generated lcov/JSON reports.

---

## File Structure

**New files:**
- `coverage.config.json` — root; the single source of gate thresholds.
- `scripts/normalize-lcov.mjs` — rewrites lcov `SF:` paths to repo-root-relative. The one Approach-A wrinkle; unit-tested in isolation.
- `scripts/coverage-floor.mjs` — parses an lcov file, computes total line %, compares to a floor.
- `scripts/coverage-api.mjs` — runs `dotnet test` with coverlet, locates the raw lcov, normalizes it to `apps/api/coverage/lcov.info`.
- `scripts/coverage-diff.mjs` — orchestrates `diff-cover` (patch gate) + both floor checks; writes `coverage-diff.json` + `coverage-report.md`; sets exit code.
- `scripts/__tests__/*` — vitest tests for the three pure-ish scripts, plus lcov fixtures.
- `apps/api/coverlet.runsettings` — forces coverlet `Format=lcov`.
- `.github/workflows/ci.yml` — the workflow.
- `docs/ci-and-coverage.md` — how to read/run coverage locally, what the gates mean, threshold tuning.

**Modified files:**
- root `package.json` — add `coverage:*` scripts + a `vitest` devDependency for the script tests + `scripts/` in workspaces? No — scripts run at root via `node`, no workspace needed.
- `apps/web/package.json` — add `@vitest/coverage-v8` devDep + `test:coverage` script.
- `apps/web/vitest.config.mts` — add `coverage` config block.
- `apps/api/DA.NA.Tests/DA.NA.Tests.csproj` — add `coverlet.collector` PackageReference.
- `README.md` — link the new doc.

**Where the script tests run:** the root `scripts/` tests use vitest run from the root. Add `vitest` as a root devDependency and a root `test:scripts` script. Root has no vitest today; adding it at the root (separate from the web workspace's vitest) keeps script tests independent of the web app.

---

## Task 1: lcov path normalization (`scripts/normalize-lcov.mjs`)

The riskiest, most-testable piece: lcov `SF:` paths differ per tool (vitest-v8 emits absolute paths; coverlet emits absolute paths; either could be relative depending on invocation). `diff-cover` matches these against `git diff` paths, which are **repo-root-relative** (`apps/web/src/...`). This script normalizes both.

**Files:**
- Create: `scripts/normalize-lcov.mjs`
- Create: `scripts/__tests__/normalize-lcov.test.mjs`
- Create: `package.json` (root) changes — add root `vitest` devDep + `test:scripts` script
- Create: `vitest.workspace-scripts.config.mjs` (root vitest config scoped to `scripts/`)

**Interfaces:**
- Produces: `normalizeLcov(content: string, opts: { repoRoot: string, base: string }): string` — default export is the CLI; named export `normalizeLcov` for tests. `base` is the directory relative `SF:` paths resolve against; absolute `SF:` paths are made relative to `repoRoot`. Output paths use forward slashes.
- Produces CLI: `node scripts/normalize-lcov.mjs <input.info> <output.info> --base <dir>` (repoRoot = `process.cwd()`).

- [ ] **Step 1: Add root vitest tooling**

Add to root `package.json` (create the `devDependencies` and `scripts` keys — they don't exist yet):

```json
{
  "name": "zagreus",
  "private": true,
  "packageManager": "yarn@4.12.0",
  "license": "AGPL-3.0-only",
  "workspaces": [
    "apps/*"
  ],
  "engines": {
    "node": ">=20.0.0 <25.0.0"
  },
  "scripts": {
    "test:scripts": "vitest run --config vitest.workspace-scripts.config.mjs"
  },
  "devDependencies": {
    "vitest": "^2.1.8"
  }
}
```

Create `vitest.workspace-scripts.config.mjs`:

```javascript
import { defineConfig } from "vitest/config";

// Root-level vitest, scoped to the CI helper scripts in scripts/.
// Deliberately separate from apps/web's vitest so script tests don't pull in
// the web app's jsdom/react setup.
export default defineConfig({
  test: {
    include: ["scripts/__tests__/**/*.test.mjs"],
    environment: "node",
  },
});
```

Run: `yarn install`
Expected: adds `vitest` to the root; lockfile updates.

- [ ] **Step 2: Write the failing test**

Create `scripts/__tests__/normalize-lcov.test.mjs`:

```javascript
import { describe, it, expect } from "vitest";
import { normalizeLcov } from "../normalize-lcov.mjs";

const repoRoot = "/repo";

describe("normalizeLcov", () => {
  it("makes an absolute SF path relative to the repo root", () => {
    const input = "TN:\nSF:/repo/apps/api/DA.NA.Api/Program.cs\nLF:10\nLH:9\nend_of_record\n";
    const out = normalizeLcov(input, { repoRoot, base: "/repo/apps/api" });
    expect(out).toContain("SF:apps/api/DA.NA.Api/Program.cs");
    expect(out).not.toContain("/repo/apps/api/DA.NA.Api/Program.cs");
  });

  it("resolves a relative SF path against base, then repo-root-relative", () => {
    const input = "SF:src/data/dashboard.ts\nLF:4\nLH:4\nend_of_record\n";
    const out = normalizeLcov(input, { repoRoot, base: "/repo/apps/web" });
    expect(out).toContain("SF:apps/web/src/data/dashboard.ts");
  });

  it("leaves non-SF, non-function lines untouched", () => {
    const input = "TN:\nDA:1,1\nLF:1\nLH:1\nend_of_record\n";
    const out = normalizeLcov(input, { repoRoot, base: "/repo/apps/web" });
    expect(out).toBe(input);
  });

  it("strips FN/FNDA records whose names may contain commas", () => {
    const input = [
      "SF:/repo/apps/api/X.cs",
      "FN:316,System.Void X::.ctor(System.Guid,System.Decimal)",
      "FNDA:1,System.Void X::.ctor(System.Guid,System.Decimal)",
      "DA:316,1",
      "LF:1",
      "LH:1",
      "end_of_record",
      "",
    ].join("\n");
    const out = normalizeLcov(input, { repoRoot, base: "/repo/apps/api" });
    expect(out).not.toContain("FN:");
    expect(out).not.toContain("FNDA:");
    expect(out).toContain("DA:316,1");
    expect(out).toContain("SF:apps/api/X.cs");
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `yarn test:scripts`
Expected: FAIL — cannot import `normalizeLcov` (module/export missing).

- [ ] **Step 4: Write minimal implementation**

Create `scripts/normalize-lcov.mjs`:

```javascript
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import path from "node:path";
import { dirname } from "node:path";

/**
 * Rewrite every `SF:` source path in an lcov report to be repo-root-relative
 * with forward slashes, so a single diff-cover call lines up with `git diff`.
 *
 * Absolute SF paths are made relative to repoRoot directly. Relative SF paths
 * are first resolved against `base` (the directory the coverage tool ran in),
 * then made relative to repoRoot.
 */
export function normalizeLcov(content, { repoRoot, base }) {
  const toPosix = (p) => p.split(path.sep).join("/").split("\\").join("/");
  return content
    .split(/\r?\n/)
    // Drop function-record lines. Their name field can contain commas (e.g.
    // coverlet emits C# method signatures like `.ctor(System.Guid,System.Decimal)`),
    // which breaks diff-cover's comma-split lcov parser. diff-cover computes patch
    // coverage from line records (DA/LF/LH), so FN/FNDA carry no signal we need.
    .filter((line) => !line.startsWith("FN:") && !line.startsWith("FNDA:"))
    .map((line) => {
      if (!line.startsWith("SF:")) return line;
      const sf = line.slice(3).trim();
      const abs = path.isAbsolute(sf) ? sf : path.resolve(base, sf);
      return `SF:${toPosix(path.relative(repoRoot, abs))}`;
    })
    .join("\n");
}

function main() {
  const [input, output, flag, baseArg] = process.argv.slice(2);
  if (!input || !output || flag !== "--base" || !baseArg) {
    console.error("usage: normalize-lcov.mjs <input.info> <output.info> --base <dir>");
    process.exit(2);
  }
  const repoRoot = process.cwd();
  const base = path.resolve(repoRoot, baseArg);
  const out = normalizeLcov(readFileSync(input, "utf8"), { repoRoot, base });
  mkdirSync(dirname(output), { recursive: true });
  writeFileSync(output, out);
}

// Run as CLI only when invoked directly (not when imported by tests).
if (import.meta.url === `file://${process.argv[1]}`) {
  main();
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `yarn test:scripts`
Expected: PASS — 4 tests. (`toPosix` in the implementation still normalizes any stray backslashes defensively; CI runs on `ubuntu-latest`, so Windows paths are not a supported input.)

- [ ] **Step 6: Commit**

```bash
git add scripts/normalize-lcov.mjs scripts/__tests__/normalize-lcov.test.mjs \
  vitest.workspace-scripts.config.mjs package.json yarn.lock
git commit -m "feat(ci): lcov SF-path normalizer for repo-root-relative coverage"
```

---

## Task 2: total-floor parser (`scripts/coverage-floor.mjs`)

Parses an lcov file, computes overall line coverage (`sum(LH)/sum(LF)`), and compares to a floor.

**Files:**
- Create: `scripts/coverage-floor.mjs`
- Create: `scripts/__tests__/coverage-floor.test.mjs`

**Interfaces:**
- Produces: `lineCoveragePct(lcov: string): number` — 0–100, one decimal (returns `100` when `LF` sum is 0).
- Produces: `checkFloor(lcov: string, floor: number): { pct: number, floor: number, pass: boolean }`.
- Produces CLI: `node scripts/coverage-floor.mjs <lcov.info> <floor>` → prints one line, exits 0 (pass) or 1 (fail).

- [ ] **Step 1: Write the failing test**

Create `scripts/__tests__/coverage-floor.test.mjs`:

```javascript
import { describe, it, expect } from "vitest";
import { lineCoveragePct, checkFloor } from "../coverage-floor.mjs";

const lcov = [
  "SF:apps/web/src/a.ts", "LF:10", "LH:9", "end_of_record",
  "SF:apps/web/src/b.ts", "LF:10", "LH:8", "end_of_record",
  "",
].join("\n");

describe("coverage-floor", () => {
  it("computes total line coverage across files", () => {
    // 17 hit / 20 found = 85.0
    expect(lineCoveragePct(lcov)).toBe(85.0);
  });

  it("returns 100 when there are no lines found", () => {
    expect(lineCoveragePct("TN:\nend_of_record\n")).toBe(100);
  });

  it("passes when pct >= floor", () => {
    expect(checkFloor(lcov, 80).pass).toBe(true);
  });

  it("fails when pct < floor", () => {
    const r = checkFloor(lcov, 90);
    expect(r.pass).toBe(false);
    expect(r.pct).toBe(85.0);
    expect(r.floor).toBe(90);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `yarn test:scripts`
Expected: FAIL — cannot import from `coverage-floor.mjs`.

- [ ] **Step 3: Write minimal implementation**

Create `scripts/coverage-floor.mjs`:

```javascript
import { readFileSync } from "node:fs";

/** Total line coverage % (0–100, one decimal) from an lcov report. */
export function lineCoveragePct(lcov) {
  let found = 0;
  let hit = 0;
  for (const line of lcov.split(/\r?\n/)) {
    if (line.startsWith("LF:")) found += Number(line.slice(3));
    else if (line.startsWith("LH:")) hit += Number(line.slice(3));
  }
  if (found === 0) return 100;
  return Math.round((hit / found) * 1000) / 10;
}

export function checkFloor(lcov, floor) {
  const pct = lineCoveragePct(lcov);
  return { pct, floor, pass: pct >= floor };
}

function main() {
  const [file, floorArg] = process.argv.slice(2);
  if (!file || floorArg === undefined) {
    console.error("usage: coverage-floor.mjs <lcov.info> <floor>");
    process.exit(2);
  }
  const { pct, floor, pass } = checkFloor(readFileSync(file, "utf8"), Number(floorArg));
  console.log(`${pass ? "PASS" : "FAIL"} total line coverage ${pct}% (floor ${floor}%) — ${file}`);
  process.exit(pass ? 0 : 1);
}

if (import.meta.url === `file://${process.argv[1]}`) {
  main();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `yarn test:scripts`
Expected: PASS — all tests across both script test files.

- [ ] **Step 5: Commit**

```bash
git add scripts/coverage-floor.mjs scripts/__tests__/coverage-floor.test.mjs
git commit -m "feat(ci): lcov total line-coverage floor checker"
```

---

## Task 3: gate config (`coverage.config.json`)

The single place humans and agents read the policy. Floors are placeholders until Task 6 measures the baseline.

**Files:**
- Create: `coverage.config.json`

**Interfaces:**
- Produces: JSON shape `{ patchMin: number, webTotalMin: number, apiTotalMin: number }` consumed by `scripts/coverage-diff.mjs` (Task 7).

- [ ] **Step 1: Write the config**

Create `coverage.config.json`:

```json
{
  "patchMin": 99,
  "webTotalMin": 0,
  "apiTotalMin": 0
}
```

`webTotalMin` / `apiTotalMin` are set to `0` for now (never blocks) and replaced with real measured floors in Task 6. `patchMin` is the decided 99.

- [ ] **Step 2: Commit**

```bash
git add coverage.config.json
git commit -m "feat(ci): coverage.config.json with patchMin=99, floors TBD-by-baseline"
```

---

## Task 4: web coverage instrumentation

Wire vitest v8 coverage → normalized `apps/web/coverage/lcov.info`.

**Files:**
- Modify: `apps/web/package.json` (add devDep + `test:coverage` script)
- Modify: `apps/web/vitest.config.mts` (add `coverage` block)
- Test (manual verification): run the script, inspect output.

**Interfaces:**
- Produces: `yarn workspace @zagreus/web test:coverage` writes raw `apps/web/coverage/lcov.info` + `apps/web/coverage/coverage-summary.json`.
- Produces: root script `coverage:web` (added in Task 6) will additionally normalize it.

- [ ] **Step 1: Add the coverage provider**

Run: `yarn workspace @zagreus/web add -D @vitest/coverage-v8@^2.1.8`
Expected: version matches the existing `vitest@^2.1.8` (provider major/minor must match vitest).

- [ ] **Step 2: Configure coverage in `apps/web/vitest.config.mts`**

Replace the `test: { ... }` line with:

```typescript
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./vitest.setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
    coverage: {
      provider: "v8",
      reporter: ["text", "lcov", "json-summary"],
      reportsDirectory: "./coverage",
      include: ["src/**/*.{ts,tsx}"],
      exclude: ["src/**/*.test.{ts,tsx}", "src/**/*.d.ts"],
    },
  },
```

- [ ] **Step 3: Add the `test:coverage` script**

In `apps/web/package.json` `scripts`, add:

```json
    "test:coverage": "vitest run --coverage",
```

- [ ] **Step 4: Run coverage and verify the report exists**

Run: `yarn workspace @zagreus/web test:coverage`
Expected: tests pass; a coverage table prints; `apps/web/coverage/lcov.info` and `apps/web/coverage/coverage-summary.json` exist.

Run: `head -5 apps/web/coverage/lcov.info`
Expected: `SF:` lines present (note whether absolute or `src/...` relative — Task 6 normalization handles both).

- [ ] **Step 5: Commit**

```bash
git add apps/web/package.json apps/web/vitest.config.mts yarn.lock
git commit -m "feat(ci): vitest v8 coverage (lcov + json-summary) for web"
```

---

## Task 5: API coverage instrumentation

Wire coverlet → lcov → normalized `apps/api/coverage/lcov.info` via a small runner script.

**Files:**
- Modify: `apps/api/DA.NA.Tests/DA.NA.Tests.csproj` (add `coverlet.collector`)
- Create: `apps/api/coverlet.runsettings`
- Create: `scripts/coverage-api.mjs`

**Interfaces:**
- Consumes: `normalizeLcov` CLI from Task 1.
- Produces: `node scripts/coverage-api.mjs` runs `dotnet test` with coverlet, then writes normalized `apps/api/coverage/lcov.info`.

- [ ] **Step 1: Add coverlet to the test project**

In `apps/api/DA.NA.Tests/DA.NA.Tests.csproj`, add to the first `<ItemGroup>` of PackageReferences:

```xml
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
```

- [ ] **Step 2: Force lcov output via runsettings**

Create `apps/api/coverlet.runsettings`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>lcov</Format>
          <ExcludeByAttribute>GeneratedCodeAttribute</ExcludeByAttribute>
          <!-- Auto-generated EF Core migrations (incl. *.Designer.cs and the
               ModelSnapshot) are scaffolded, not hand-written — exclude them so
               the floor and the 99% patch gate measure real code only. -->
          <ExcludeByFile>**/Migrations/*.cs</ExcludeByFile>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

- [ ] **Step 3: Write the API coverage runner**

Create `scripts/coverage-api.mjs`:

```javascript
import { execFileSync } from "node:child_process";
import { readdirSync, statSync, rmSync } from "node:fs";
import path from "node:path";

// Run the .NET suite with coverlet (lcov), then normalize the emitted
// coverage.info into apps/api/coverage/lcov.info (repo-root-relative paths).
const repoRoot = process.cwd();
const apiDir = path.join(repoRoot, "apps/api");
const resultsDir = path.join(apiDir, "coverage/raw");
const outFile = path.join(apiDir, "coverage/lcov.info");

rmSync(resultsDir, { recursive: true, force: true });

execFileSync(
  "dotnet",
  [
    "test",
    path.join(apiDir, "DistributeAid.NeedsAssessment.sln"),
    "--collect:XPlat Code Coverage",
    "--settings", path.join(apiDir, "coverlet.runsettings"),
    "--results-directory", resultsDir,
  ],
  { stdio: "inherit" },
);

// coverlet writes <resultsDir>/<guid>/coverage.info
function findCoverageInfo(dir) {
  for (const entry of readdirSync(dir)) {
    const full = path.join(dir, entry);
    if (statSync(full).isDirectory()) {
      const nested = findCoverageInfo(full);
      if (nested) return nested;
    } else if (entry === "coverage.info") {
      return full;
    }
  }
  return null;
}

const raw = findCoverageInfo(resultsDir);
if (!raw) {
  console.error("coverage-api: no coverage.info produced under", resultsDir);
  process.exit(1);
}

execFileSync(
  process.execPath,
  [path.join(repoRoot, "scripts/normalize-lcov.mjs"), raw, outFile, "--base", apiDir],
  { stdio: "inherit" },
);
console.log("coverage-api: wrote", path.relative(repoRoot, outFile));
```

- [ ] **Step 4: Run and verify normalized output**

Run (from repo root): `node scripts/coverage-api.mjs`
Expected: `dotnet test` passes (13 test files); `apps/api/coverage/lcov.info` exists.

Run: `grep -m3 '^SF:' apps/api/coverage/lcov.info`
Expected: paths look like `SF:apps/api/DA.NA.Api/...` (repo-root-relative). If any path is NOT under `apps/api/`, the `--base` is wrong — fix before proceeding.

- [ ] **Step 5: Commit**

```bash
git add apps/api/DA.NA.Tests/DA.NA.Tests.csproj apps/api/coverlet.runsettings scripts/coverage-api.mjs
git commit -m "feat(ci): coverlet lcov coverage for api, normalized to repo root"
```

---

## Task 6: root coverage scripts + measure baseline floors

Add the shared entry-point scripts and replace the placeholder floors with measured numbers.

**Files:**
- Modify: root `package.json` (add `coverage:*` scripts)
- Modify: `coverage.config.json` (real floors)

**Interfaces:**
- Consumes: Task 4 web `test:coverage`, Task 5 `coverage-api.mjs`, Task 1 `normalize-lcov.mjs`, Task 2 `coverage-floor.mjs`.
- Produces: `yarn coverage:web`, `yarn coverage:api`, `yarn coverage` — all leaving normalized lcov under `apps/*/coverage/lcov.info`.

- [ ] **Step 1: Add root coverage scripts**

In root `package.json` `scripts`, add (alongside `test:scripts`):

```json
    "coverage:web": "yarn workspace @zagreus/web test:coverage && node scripts/normalize-lcov.mjs apps/web/coverage/lcov.info apps/web/coverage/lcov.info --base apps/web",
    "coverage:api": "node scripts/coverage-api.mjs",
    "coverage": "yarn coverage:web && yarn coverage:api"
```

(`coverage:web` normalizes in place so web lcov paths are repo-root-relative just like api.)

- [ ] **Step 2: Produce both reports**

Run: `yarn coverage`
Expected: both suites run; `apps/web/coverage/lcov.info` and `apps/api/coverage/lcov.info` exist, both with `SF:apps/...` paths.

- [ ] **Step 3: Measure baseline floors**

Run: `node scripts/coverage-floor.mjs apps/web/coverage/lcov.info 0`
Run: `node scripts/coverage-floor.mjs apps/api/coverage/lcov.info 0`
Expected: each prints `PASS total line coverage <N>% (floor 0%)`. Record both `<N>` values.

- [ ] **Step 4: Set measured floors**

Edit `coverage.config.json`: set `webTotalMin` and `apiTotalMin` to the measured percentages **rounded down by 2 points** (aim high, per the spec — e.g. measured 91.4 → floor 89). Keep `patchMin: 99`.

- [ ] **Step 5: Verify the floors pass against current coverage**

Run: `node scripts/coverage-floor.mjs apps/web/coverage/lcov.info <webTotalMin>`
Run: `node scripts/coverage-floor.mjs apps/api/coverage/lcov.info <apiTotalMin>`
Expected: both `PASS`.

- [ ] **Step 6: Commit**

```bash
git add package.json coverage.config.json
git commit -m "feat(ci): root coverage scripts + measured baseline floors"
```

---

## Task 7: patch gate + report orchestrator (`scripts/coverage-diff.mjs`)

Runs `diff-cover` (patch gate) across both lcov files and the two floor checks, writes the machine-readable `coverage-diff.json` and the human `coverage-report.md`, and sets the process exit code. This is the single command CI and agents both call.

**Files:**
- Create: `scripts/coverage-diff.mjs`
- Modify: root `package.json` (add `coverage:diff` script)

**Prerequisite:** `diff-cover` installed. Locally: `pipx install diff-cover` (or `python3 -m pip install --user diff-cover`). CI installs it in Task 8.

**Interfaces:**
- Consumes: `coverage.config.json` (Task 3/6), both normalized lcov files, `coverage-floor.mjs` (`lineCoveragePct`).
- Produces: `coverage-diff.json` `{ patch: { pct, min, pass }, floors: { web: {pct,floor,pass}, api: {pct,floor,pass} }, pass: boolean }`.
- Produces: `coverage-report.md` (PR-comment body, sans marker).
- Produces CLI: `node scripts/coverage-diff.mjs`; env `DIFF_COMPARE_BRANCH` (default `origin/main`).

- [ ] **Step 1: Write the orchestrator**

Create `scripts/coverage-diff.mjs`:

```javascript
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { lineCoveragePct } from "./coverage-floor.mjs";

const cfg = JSON.parse(readFileSync("coverage.config.json", "utf8"));
const compareBranch = process.env.DIFF_COMPARE_BRANCH || "origin/main";
const webLcov = "apps/web/coverage/lcov.info";
const apiLcov = "apps/api/coverage/lcov.info";

// --- Patch gate via diff-cover (JSON report) ---
const patchJson = "coverage-patch.json";
let patchFailed = false;
try {
  execFileSync(
    "diff-cover",
    [
      webLcov, apiLcov,
      "--compare-branch", compareBranch,
      "--fail-under", String(cfg.patchMin),
      "--format", `json:${patchJson}`,
      "--quiet",
    ],
    { stdio: "inherit" },
  );
} catch {
  patchFailed = true; // non-zero exit == below --fail-under; report still written
}
// diff-cover writes the JSON report even when it fails --fail-under. A MISSING
// report instead means diff-cover crashed (e.g. an unparseable lcov) — surface
// that clearly rather than throwing a raw ENOENT.
if (!existsSync(patchJson)) {
  console.error(
    `coverage-diff: diff-cover produced no ${patchJson} — it likely failed to parse a coverage report. Aborting.`,
  );
  process.exit(2);
}
const patchReport = JSON.parse(readFileSync(patchJson, "utf8"));
const patchPct = patchReport.total_percent_covered ?? 100;

// --- Total floors ---
const floor = (file, min) => {
  const pct = lineCoveragePct(readFileSync(file, "utf8"));
  return { pct, floor: min, pass: pct >= min };
};
const web = floor(webLcov, cfg.webTotalMin);
const api = floor(apiLcov, cfg.apiTotalMin);

const patch = { pct: patchPct, min: cfg.patchMin, pass: !patchFailed };
const pass = patch.pass && web.pass && api.pass;
const result = { patch, floors: { web, api }, pass };
writeFileSync("coverage-diff.json", JSON.stringify(result, null, 2));

// --- Markdown for the PR comment ---
const mark = (ok) => (ok ? "✅" : "❌");
const uncovered = Object.entries(patchReport.src_stats ?? {})
  .flatMap(([f, s]) => (s.violation_lines?.length ? [`- \`${f}\`: ${s.violation_lines.join(", ")}`] : []));
const md = [
  `### Coverage report`,
  ``,
  `${mark(patch.pass)} **Patch coverage** ${patch.pct.toFixed(1)}% (min ${patch.min}%)`,
  `${mark(web.pass)} **Web total** ${web.pct}% (floor ${web.floor}%)`,
  `${mark(api.pass)} **API total** ${api.pct}% (floor ${api.floor}%)`,
  ``,
  uncovered.length ? `#### Uncovered changed lines\n${uncovered.join("\n")}` : `All changed lines covered. 🎉`,
  ``,
].join("\n");
writeFileSync("coverage-report.md", md);

console.log(md);
process.exit(pass ? 0 : 1);
```

- [ ] **Step 2: Add the root script**

In root `package.json` `scripts`, add:

```json
    "coverage:diff": "node scripts/coverage-diff.mjs"
```

- [ ] **Step 3: Install diff-cover and run the full gate locally**

Run: `pipx install diff-cover || python3 -m pip install --user diff-cover`
Run: `yarn coverage` (refresh reports)
Run: `git fetch origin main` (so `origin/main` exists to diff against)
Run: `yarn coverage:diff`
Expected: markdown prints; `coverage-diff.json` + `coverage-report.md` written; exit 0 if patch ≥ 99% and floors pass. (On a branch with no changes vs `origin/main`, diff-cover reports 100% patch coverage — pass.)

- [ ] **Step 4: Verify the JSON shape**

Run: `cat coverage-diff.json`
Expected: keys `patch`, `floors.web`, `floors.api`, `pass` — matching the Interfaces contract.

- [ ] **Step 5: Commit**

```bash
git add scripts/coverage-diff.mjs package.json
git commit -m "feat(ci): coverage:diff — patch gate + floors + PR-comment report"
```

---

## Task 8: GitHub Actions workflow (`.github/workflows/ci.yml`)

Parallel `web` + `api` jobs upload lcov; a `coverage` job runs the same `yarn coverage:diff` and upserts one PR comment.

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: root scripts `coverage:web`, `coverage:api`, `coverage:diff`; artifacts `web-lcov`, `api-lcov`.

- [ ] **Step 1: Write the workflow**

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  pull_request:
  push:
    branches: [main]

concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
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
      - run: yarn workspace @zagreus/web typecheck
      - run: yarn coverage:web
      - uses: actions/upload-artifact@v4
        with:
          name: web-lcov
          path: apps/web/coverage/lcov.info

  api:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 24
      - run: corepack enable
      - run: yarn install --immutable
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - run: yarn coverage:api
      - uses: actions/upload-artifact@v4
        with:
          name: api-lcov
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
      - run: corepack enable
      - run: yarn install --immutable
      - uses: actions/setup-python@v5
        with:
          python-version: "3.12"
      # Install into the setup-python env (on PATH), pinned for reproducibility —
      # avoids pipx's ~/.local/bin not being on PATH mid-job.
      - run: python -m pip install "diff-cover==10.3.0"
      - uses: actions/download-artifact@v4
        with:
          name: web-lcov
          path: apps/web/coverage
      - uses: actions/download-artifact@v4
        with:
          name: api-lcov
          path: apps/api/coverage
      - name: Determine compare branch
        run: |
          if [ -n "${{ github.base_ref }}" ]; then
            git fetch origin "${{ github.base_ref }}" --depth=1
            echo "DIFF_COMPARE_BRANCH=origin/${{ github.base_ref }}" >> "$GITHUB_ENV"
          else
            echo "DIFF_COMPARE_BRANCH=origin/main" >> "$GITHUB_ENV"
          fi
      - name: Run coverage gates
        id: gates
        run: yarn coverage:diff
        continue-on-error: true
      - name: Upsert PR comment
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const marker = '<!-- coverage-report -->';
            const body = marker + '\n' + fs.readFileSync('coverage-report.md', 'utf8');
            const { owner, repo } = context.repo;
            const issue_number = context.issue.number;
            const { data: comments } = await github.rest.issues.listComments({ owner, repo, issue_number });
            const existing = comments.find(c => c.body && c.body.includes(marker));
            if (existing) {
              await github.rest.issues.updateComment({ owner, repo, comment_id: existing.id, body });
            } else {
              await github.rest.issues.createComment({ owner, repo, issue_number, body });
            }
      - name: Fail if gates failed
        if: steps.gates.outcome == 'failure'
        run: |
          echo "Coverage gates failed — see the PR comment."
          exit 1
```

Notes baked in: `continue-on-error` on the gate step lets the comment post even when the gate is red, then the final step re-fails the job. `fetch-depth: 0` + fetching the base ref gives `diff-cover` a real diff range.

- [ ] **Step 2: Validate the workflow syntax**

Run: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml')); print('yaml ok')"`
Expected: `yaml ok`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "feat(ci): GitHub Actions workflow with parallel suites + coverage gate"
```

---

## Task 9: docs + README link

**Files:**
- Create: `docs/ci-and-coverage.md`
- Modify: `README.md` (link it)

- [ ] **Step 1: Write the doc**

Create `docs/ci-and-coverage.md`:

```markdown
# CI & coverage

CI runs on every pull request (and on pushes to `main` to refresh the baseline).
It runs both test suites with coverage and enforces two gates. The same checks run
locally through root `yarn` scripts — no drift between "what you see" and "what CI enforces".

## Gates

- **Patch coverage** — changed lines must be ≥ `patchMin` (default **99%**). This is the
  primary signal: new code ships with tests.
- **Total floors** — each suite's overall line coverage must stay ≥ its floor
  (`webTotalMin`, `apiTotalMin`), seeded from the measured baseline.

All three thresholds live in [`coverage.config.json`](../coverage.config.json). Change them there.

## Run it locally

```bash
yarn coverage        # run both suites with coverage → apps/*/coverage/lcov.info
git fetch origin main
yarn coverage:diff   # patch gate + floors; writes coverage-diff.json + coverage-report.md
```

`coverage-diff.json` is the machine-readable result an agent reads to self-check before
finishing — identical to what the CI gate decides. Exit code is non-zero if any gate fails.

## Adjusting thresholds

Edit `coverage.config.json`. Raising floors as coverage climbs is a manual, deliberate step.
```

- [ ] **Step 2: Link from README**

In `README.md`, under the `## Conventions` section, add a line:

```markdown
CI and coverage gates are documented in [docs/ci-and-coverage.md](docs/ci-and-coverage.md).
```

- [ ] **Step 3: Commit**

```bash
git add docs/ci-and-coverage.md README.md
git commit -m "docs(ci): document coverage gates and local self-check"
```

---

## Task 10: end-to-end verification (negative → positive → comment upsert)

Prove the gates actually gate — not merely that CI is green. Per spec Verification.

**Files:** none committed to `main`; uses throwaway commits on the PR branch, then reverts.

- [ ] **Step 1: Push the branch and open/refresh the PR**

Run: `git push origin feat/ci-coverage-gates`
Expected: PR #12 updates; the `web`, `api`, `coverage` jobs run.

- [ ] **Step 2: Confirm the green path**

Watch the run: `gh run watch $(gh run list --branch feat/ci-coverage-gates --limit 1 --json databaseId -q '.[0].databaseId')`
Expected: all jobs pass; one coverage comment appears on PR #12 with patch + floor lines.

- [ ] **Step 3: Negative test — uncovered new code goes red**

Add an intentionally-uncovered function to `apps/web/src/data/`:

```typescript
// apps/web/src/data/_covtest.ts
export function uncoveredOnPurpose(n: number): number {
  if (n > 0) return n * 2; // no test covers this
  return -1;
}
```

Commit + push:
```bash
git add apps/web/src/data/_covtest.ts
git commit -m "test: temporary uncovered code to prove the patch gate"
git push
```
Expected: `coverage` job **fails**; PR comment updates **in place** (same comment id) and lists `apps/web/src/data/_covtest.ts` uncovered lines. Confirm no second comment was created.

- [ ] **Step 4: Positive test — cover it, gate goes green**

Add a test:

```typescript
// apps/web/src/data/_covtest.test.ts
import { describe, it, expect } from "vitest";
import { uncoveredOnPurpose } from "./_covtest";

describe("uncoveredOnPurpose", () => {
  it("doubles positives", () => expect(uncoveredOnPurpose(3)).toBe(6));
  it("returns -1 otherwise", () => expect(uncoveredOnPurpose(0)).toBe(-1));
});
```

Commit + push:
```bash
git add apps/web/src/data/_covtest.test.ts
git commit -m "test: cover the temporary function — gate should go green"
git push
```
Expected: `coverage` job **passes**; the same comment updates to all-green.

- [ ] **Step 5: Remove the throwaway files**

```bash
git rm apps/web/src/data/_covtest.ts apps/web/src/data/_covtest.test.ts
git commit -m "test: remove temporary coverage-gate probe"
git push
```
Expected: gate still green; PR is clean of probe files.

- [ ] **Step 6: Final state**

Confirm PR #12 shows: green CI, one coverage comment, and a diff of only the intended CI/coverage files (plus the earlier spec change). Ready for review/merge.

---

## Self-Review

**Spec coverage:**
- PR gate (both suites + coverage on every PR) → Tasks 4, 5, 8. ✓
- Local agent self-check, same scripts as CI → Tasks 6, 7 (`coverage:diff` → `coverage-diff.json`). ✓
- Consolidated PR comment (patch % + uncovered lines) → Task 7 (markdown) + Task 8 (upsert). ✓
- Self-contained (no SaaS, only `GITHUB_TOKEN`) → Task 8 uses `github-script` + `GITHUB_TOKEN`, `diff-cover` self-hosted. ✓
- Patch gate + total floor, `patchMin` 99 → Tasks 3, 6, 7. ✓
- Path normalization (the wrinkle) → Task 1, exercised by Tasks 5/6, proven by Task 10. ✓
- lcov from both suites → Tasks 4 (v8), 5 (coverlet). ✓
- Root scripts `coverage:web|api|diff`, `coverage` → Tasks 6, 7. ✓
- Concurrency guard, comment upsert, `push`+`pull_request` triggers → Task 8. ✓
- Baseline measured not guessed → Task 6. ✓
- Verification (negative/positive/upsert) → Task 10. ✓
- Docs linked from README → Task 9. ✓

**Placeholder scan:** floors are `0` in Task 3 by design, replaced with measured values in Task 6 (not a plan placeholder — a deliberate two-phase seed). No `TODO`/`TBD`/"handle edge cases" left.

**Type consistency:** `normalizeLcov(content, {repoRoot, base})` used identically in Tasks 1/5/6. `lineCoveragePct`/`checkFloor` signatures consistent Tasks 2/7. `coverage-diff.json` shape defined in Task 7 Interfaces and asserted in Task 7 Step 4. Script names (`normalize-lcov.mjs`, `coverage-floor.mjs`, `coverage-api.mjs`, `coverage-diff.mjs`) consistent across tasks.
