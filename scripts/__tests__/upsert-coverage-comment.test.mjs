import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { mkdtempSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { createRequire } from "node:module";

// The module under test is CommonJS (github-script runs in a CJS context).
const require = createRequire(import.meta.url);
const { upsertCoverageComment, readReportBody, MARKER } = require(
  "../../.github/scripts/upsert-coverage-comment.cjs",
);

const context = { repo: { owner: "distributeaid", repo: "super-zagreus" }, issue: { number: 12 } };

// Minimal Octokit stand-in: paginate returns the seeded comments; update/create
// record their calls so the test can assert which path ran.
function makeGithub(existingComments) {
  const calls = { list: [], update: [], create: [] };
  const github = {
    calls,
    paginate: async (fn, params) => {
      calls.list.push({ fn, params });
      return existingComments;
    },
    rest: {
      issues: {
        listComments: "listComments",
        updateComment: async (args) => calls.update.push(args),
        createComment: async (args) => calls.create.push(args),
      },
    },
  };
  return github;
}

describe("readReportBody", () => {
  let dir;
  beforeEach(() => {
    dir = mkdtempSync(path.join(tmpdir(), "upsert-"));
  });
  afterEach(() => {
    rmSync(dir, { recursive: true, force: true });
  });

  it("prefixes the marker to the report file when it exists", () => {
    const file = path.join(dir, "coverage-report.md");
    writeFileSync(file, "### Coverage report\n\n✅ all good\n");
    const body = readReportBody(file);
    expect(body.startsWith(MARKER + "\n")).toBe(true);
    expect(body).toContain("✅ all good");
  });

  it("falls back to a visible error when the report file is missing", () => {
    const body = readReportBody(path.join(dir, "nope.md"));
    expect(body.startsWith(MARKER + "\n")).toBe(true);
    expect(body).toContain("errored before producing a report");
  });
});

describe("upsertCoverageComment", () => {
  let reportPath;
  let dir;
  beforeEach(() => {
    dir = mkdtempSync(path.join(tmpdir(), "upsert-"));
    reportPath = path.join(dir, "coverage-report.md");
    writeFileSync(reportPath, "### Coverage report\n\n✅ patch 100%\n");
  });
  afterEach(() => {
    rmSync(dir, { recursive: true, force: true });
  });

  it("creates a new comment when no marker comment exists", async () => {
    const github = makeGithub([
      { id: 1, body: "unrelated review comment" },
      { id: 2, body: "another one" },
    ]);
    const outcome = await upsertCoverageComment({ github, context, reportPath });
    expect(outcome).toBe("created");
    expect(github.calls.create).toHaveLength(1);
    expect(github.calls.update).toHaveLength(0);
    expect(github.calls.create[0].issue_number).toBe(12);
    expect(github.calls.create[0].body).toContain(MARKER);
    expect(github.calls.create[0].body).toContain("patch 100%");
  });

  it("updates the existing marker comment in place (no duplicate)", async () => {
    const github = makeGithub([
      { id: 7, body: "unrelated" },
      { id: 42, body: MARKER + "\n### Coverage report\n\n(old)" },
    ]);
    const outcome = await upsertCoverageComment({ github, context, reportPath });
    expect(outcome).toBe("updated");
    expect(github.calls.update).toHaveLength(1);
    expect(github.calls.create).toHaveLength(0);
    expect(github.calls.update[0].comment_id).toBe(42);
    expect(github.calls.update[0].body).toContain("patch 100%");
  });

  it("requests all comments with a large page size (pagination)", async () => {
    const github = makeGithub([]);
    await upsertCoverageComment({ github, context, reportPath });
    expect(github.calls.list).toHaveLength(1);
    expect(github.calls.list[0].params.per_page).toBe(100);
    expect(github.calls.list[0].params).toMatchObject({
      owner: "distributeaid",
      repo: "super-zagreus",
      issue_number: 12,
    });
  });
});
