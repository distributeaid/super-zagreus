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
