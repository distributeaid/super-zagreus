import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { lineCoveragePct } from "./coverage-floor.mjs";

const mark = (ok) => (ok ? "✅" : "❌");

/**
 * Evaluate the two coverage gates and render the PR-comment body. Pure: takes the
 * parsed diff-cover JSON report, both lcov contents, the config, and whether
 * diff-cover's patch gate failed — returns the machine-readable result object and
 * the markdown. This is the gate logic; `main()` only does the I/O around it.
 *
 * @param {object} args
 * @param {object} args.patchReport - parsed diff-cover JSON report
 * @param {string} args.webLcov - web lcov file contents
 * @param {string} args.apiLcov - api lcov file contents
 * @param {{patchMin:number, webTotalMin:number, apiTotalMin:number}} args.cfg
 * @param {boolean} args.patchFailed - true if diff-cover exited non-zero on --fail-under
 * @returns {{ result: object, markdown: string }}
 */
export function buildReport({ patchReport, webLcov, apiLcov, cfg, patchFailed }) {
  // A report without total_percent_covered means diff-cover found no coverable
  // changed lines — nothing to gate, so treat the patch as fully covered.
  const patchPct = patchReport.total_percent_covered ?? 100;

  const floor = (lcov, min) => {
    const pct = lineCoveragePct(lcov);
    return { pct, floor: min, pass: pct >= min };
  };
  const web = floor(webLcov, cfg.webTotalMin);
  const api = floor(apiLcov, cfg.apiTotalMin);

  const patch = { pct: patchPct, min: cfg.patchMin, pass: !patchFailed };
  const pass = patch.pass && web.pass && api.pass;
  const result = { patch, floors: { web, api }, pass };

  const uncovered = Object.entries(patchReport.src_stats ?? {}).flatMap(([f, s]) =>
    s.violation_lines?.length ? [`- \`${f}\`: ${s.violation_lines.join(", ")}`] : [],
  );
  const markdown = [
    `### Coverage report`,
    ``,
    `${mark(patch.pass)} **Patch coverage** ${patch.pct.toFixed(1)}% (min ${patch.min}%)`,
    `${mark(web.pass)} **Web total** ${web.pct}% (floor ${web.floor}%)`,
    `${mark(api.pass)} **API total** ${api.pct}% (floor ${api.floor}%)`,
    ``,
    uncovered.length
      ? `#### Uncovered changed lines\n${uncovered.join("\n")}`
      : `All changed lines covered. 🎉`,
    ``,
  ].join("\n");

  return { result, markdown };
}

function main() {
  const cfg = JSON.parse(readFileSync("coverage.config.json", "utf8"));
  const compareBranch = process.env.DIFF_COMPARE_BRANCH || "origin/main";
  const webLcovFile = "apps/web/coverage/lcov.info";
  const apiLcovFile = "apps/api/coverage/lcov.info";
  const patchJson = "coverage-patch.json";

  // --- Patch gate via diff-cover (JSON report) ---
  let patchFailed = false;
  try {
    execFileSync(
      "diff-cover",
      [
        webLcovFile, apiLcovFile,
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
  const { result, markdown } = buildReport({
    patchReport,
    webLcov: readFileSync(webLcovFile, "utf8"),
    apiLcov: readFileSync(apiLcovFile, "utf8"),
    cfg,
    patchFailed,
  });

  writeFileSync("coverage-diff.json", JSON.stringify(result, null, 2));
  writeFileSync("coverage-report.md", markdown);
  console.log(markdown);
  process.exit(result.pass ? 0 : 1);
}

// Run as CLI only when invoked directly (not when imported by tests).
if (import.meta.url === `file://${process.argv[1]}`) {
  main();
}
