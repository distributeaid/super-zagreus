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
