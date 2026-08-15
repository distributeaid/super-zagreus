import { describe, it, expect } from "vitest";
import { buildReport } from "../coverage-diff.mjs";

const cfg = { patchMin: 99, webTotalMin: 41, apiTotalMin: 28 };
// LF 10 / LH 9 = 90% line coverage — above both floors.
const lcov90 = "SF:apps/web/src/a.ts\nLF:10\nLH:9\nend_of_record\n";
// LF 10 / LH 2 = 20% — below the api floor (28).
const lcov20 = "SF:apps/api/x.cs\nLF:10\nLH:2\nend_of_record\n";

describe("buildReport", () => {
  it("passes when the patch gate and both floors pass", () => {
    const patchReport = { total_percent_covered: 100, src_stats: {} };
    const { result, markdown } = buildReport({
      patchReport, webLcov: lcov90, apiLcov: lcov90, cfg, patchFailed: false,
    });
    expect(result.pass).toBe(true);
    expect(result.patch).toEqual({ pct: 100, min: 99, pass: true });
    expect(result.floors.web.pass).toBe(true);
    expect(result.floors.api.pass).toBe(true);
    expect(markdown).toContain("✅ **Patch coverage** 100.0% (min 99%)");
    expect(markdown).toContain("All changed lines covered");
  });

  it("fails overall and lists uncovered lines when the patch gate failed", () => {
    const patchReport = {
      total_percent_covered: 50,
      src_stats: { "apps/web/src/a.ts": { violation_lines: [3, 4] } },
    };
    const { result, markdown } = buildReport({
      patchReport, webLcov: lcov90, apiLcov: lcov90, cfg, patchFailed: true,
    });
    expect(result.patch.pass).toBe(false);
    expect(result.pass).toBe(false);
    expect(markdown).toContain("❌ **Patch coverage** 50.0%");
    expect(markdown).toContain("#### Uncovered changed lines");
    expect(markdown).toContain("`apps/web/src/a.ts`: 3, 4");
  });

  it("fails overall when a total floor is below its threshold", () => {
    const patchReport = { total_percent_covered: 100, src_stats: {} };
    const { result } = buildReport({
      patchReport, webLcov: lcov90, apiLcov: lcov20, cfg, patchFailed: false,
    });
    expect(result.patch.pass).toBe(true);
    expect(result.floors.api.pass).toBe(false); // 20% < 28
    expect(result.pass).toBe(false);
  });

  it("treats a missing total_percent_covered as 100% (no coverable changed lines)", () => {
    const patchReport = { src_stats: {} };
    const { result } = buildReport({
      patchReport, webLcov: lcov90, apiLcov: lcov90, cfg, patchFailed: false,
    });
    expect(result.patch.pct).toBe(100);
    expect(result.pass).toBe(true);
  });
});
