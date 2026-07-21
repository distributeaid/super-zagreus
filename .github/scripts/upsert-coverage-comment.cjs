// Post (or update in place) the single coverage-report comment on a PR.
// Loaded by the "Upsert PR comment" step in .github/workflows/ci.yml via
// actions/github-script, which supplies the `github` (Octokit) and `context`
// objects. Kept in its own module so the logic can be unit-tested.

const fs = require("fs");

const MARKER = "<!-- coverage-report -->";

/**
 * Build the comment body: the hidden marker followed by the coverage report.
 * If the report file is missing (the gate crashed before writing it), fall back
 * to a visible error notice so the failure isn't silent.
 * @param {string} [reportPath]
 * @returns {string}
 */
function readReportBody(reportPath = "coverage-report.md") {
  const report = fs.existsSync(reportPath)
    ? fs.readFileSync(reportPath, "utf8")
    : "### Coverage report\n\n⚠️ The coverage gate errored before producing a report — see the job logs.";
  return MARKER + "\n" + report;
}

/**
 * Find the existing marker comment on the PR and update it, or create one if
 * none exists — so re-runs update a single comment in place (no spam).
 * @param {object} args
 * @param {object} args.github  - Octokit client (from actions/github-script)
 * @param {object} args.context - workflow context (from actions/github-script)
 * @param {string} [args.reportPath]
 * @returns {Promise<"created"|"updated">}
 */
async function upsertCoverageComment({ github, context, reportPath = "coverage-report.md" }) {
  const body = readReportBody(reportPath);
  const { owner, repo } = context.repo;
  const issue_number = context.issue.number;

  // Paginate: a busy PR can have >30 comments, and the default page size would
  // miss our existing marker comment and post a duplicate.
  const comments = await github.paginate(github.rest.issues.listComments, {
    owner,
    repo,
    issue_number,
    per_page: 100,
  });
  const existing = comments.find((c) => c.body && c.body.includes(MARKER));

  if (existing) {
    await github.rest.issues.updateComment({ owner, repo, comment_id: existing.id, body });
    return "updated";
  }
  await github.rest.issues.createComment({ owner, repo, issue_number, body });
  return "created";
}

module.exports = { upsertCoverageComment, readReportBody, MARKER };
