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
