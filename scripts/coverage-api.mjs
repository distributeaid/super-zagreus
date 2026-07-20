import { execFileSync } from "node:child_process";
import { existsSync, readdirSync, statSync, rmSync } from "node:fs";
import path from "node:path";

/**
 * Recursively find coverlet's emitted `coverage.info` under a results directory
 * (coverlet writes it to `<resultsDir>/<guid>/coverage.info`). Returns the full
 * path, or null if the directory is missing or contains no such file.
 */
export function findCoverageInfo(dir) {
  if (!existsSync(dir)) return null;
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

function main() {
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
}

// Run as CLI only when invoked directly (not when imported by tests).
if (import.meta.url === `file://${process.argv[1]}`) {
  main();
}
