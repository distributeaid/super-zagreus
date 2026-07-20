import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { findCoverageInfo } from "../coverage-api.mjs";

let dir;
beforeEach(() => {
  dir = mkdtempSync(path.join(tmpdir(), "covapi-"));
});
afterEach(() => {
  rmSync(dir, { recursive: true, force: true });
});

describe("findCoverageInfo", () => {
  it("finds coverage.info nested in a guid subdirectory", () => {
    const guid = path.join(dir, "3f2a-guid");
    mkdirSync(guid);
    const target = path.join(guid, "coverage.info");
    writeFileSync(target, "TN:\nend_of_record\n");
    expect(findCoverageInfo(dir)).toBe(target);
  });

  it("returns null when no coverage.info exists", () => {
    mkdirSync(path.join(dir, "empty"));
    expect(findCoverageInfo(dir)).toBe(null);
  });

  it("returns null when the results directory does not exist", () => {
    expect(findCoverageInfo(path.join(dir, "does-not-exist"))).toBe(null);
  });
});
