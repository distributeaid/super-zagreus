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
    // Drop function-record lines. Their name field can contain commas (coverlet
    // emits C# method signatures like `.ctor(System.Guid,System.Decimal)`), which
    // breaks diff-cover's comma-split lcov parser. diff-cover computes patch
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
