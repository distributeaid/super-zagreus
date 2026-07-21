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

  it("strips FN/FNDA records whose names may contain commas, keeps FNF/FNH", () => {
    const input = [
      "SF:/repo/apps/api/X.cs",
      "FN:316,System.Void X::.ctor(System.Guid,System.Decimal)",
      "FNDA:1,System.Void X::.ctor(System.Guid,System.Decimal)",
      "FNF:1",
      "FNH:1",
      "DA:316,1",
      "LF:1",
      "LH:1",
      "end_of_record",
      "",
    ].join("\n");
    const out = normalizeLcov(input, { repoRoot, base: "/repo/apps/api" });
    expect(out).not.toContain("FN:");
    expect(out).not.toContain("FNDA:");
    expect(out).toContain("FNF:1");
    expect(out).toContain("FNH:1");
    expect(out).toContain("DA:316,1");
    expect(out).toContain("SF:apps/api/X.cs");
  });
});
