# Review Requirements Design

**Date:** 2026-08-15
**Status:** Approved, pending implementation

## Purpose

super-zagreus has no `.github/` directory, no CODEOWNERS, and no branch
protection: `GET /repos/distributeaid/super-zagreus/rulesets` returns an empty
list. Anyone with write access can push directly to `main`, and the CI work in
flight (PRs #23 and #24) has nothing making it binding.

This design mirrors the review requirements of
[`distributeaid/next-website-v2`](https://github.com/distributeaid/next-website-v2)
so that changes to `main` arrive through a reviewed pull request with green
checks.

## What next-website-v2 does

Readable from the repository:

| Artifact | Content |
| --- | --- |
| `.github/CODEOWNERS` | `* @distributeaid/reviews-website` |
| `.github/workflows/checks.yml` | On push; job `checks` runs `lint`, `check:format`, `check:types`; job `test` runs `test` |
| `.github/workflows/pr-title-check.yml` | On `pull_request_target`; validates PR title and requires a linked issue, via `distributeaid/centralised-github-actions/.github/actions/pr-title-check-linking@main` |
| `.github/pull_request_template.md` | What changed / How visible / How to test |
| `.github/ISSUE_TEMPLATE/` | Bug report, new feature, change request, plus `config.yml` |

The branch protection ruleset itself is **not** readable from this environment
(see [Constraints](#constraints)), so the ruleset below is reconstructed from
the repository's evident intent rather than copied verbatim.

## Constraints

Three constraints shape the design. They are environmental, not preferences.

**Rulesets cannot be applied from here.** Required approvals, required status
checks, and push restrictions are GitHub *settings*, not repository files. No
MCP tool in this session exposes the rulesets API, and direct calls to
`api.github.com` are blocked by the session proxy. The ruleset is therefore
delivered as importable JSON plus written instructions, and a human with admin
rights applies it.

**Required status checks match by job name, and the workflow must already exist
on the base branch.** `ci.yml` currently lives only on `ci/pipeline` (PR #23);
it is not on `main`. Requiring `web`, `api`, or `coverage` before those PRs
merge would leave every pull request waiting on a check that never reports,
freezing `main`. This forces the phased rollout below.

**Teams cannot be created from here.** CODEOWNERS referencing a non-existent
team fails silently — no review is requested, and the rule cannot be satisfied.
`@distributeaid/reviews-zagreus` must exist before the ruleset is enforced.

## Decisions

| Decision | Choice | Reasoning |
| --- | --- | --- |
| Scope | Full mirror of the website | Chosen deliberately over a CI-only gate |
| Reviewer team | New `@distributeaid/reviews-zagreus` | `reviews-website` is scoped to the website repo; `contributors` is too broad |
| Required checks | Existing CI jobs only (`web`, `api`, `coverage`) | No lint/format tooling exists in this repo yet; adding it is tracked separately |
| PR title check | Mirrored, bots exempt | Dependabot PRs have no linked issue and would be permanently blocked |
| Bypass | Repo admins, plus Dependabot auto-merge | Keeps a solo author from deadlocking; keeps dependency bumps off the review queue |

### Known consequence of the full mirror

CODEOWNERS review is unsatisfiable when the only member of
`reviews-zagreus` is also the pull request author — GitHub does not count an
author's own approval. Until a second member is added, every merge depends on
admin bypass. Admin bypass is the escape hatch, not the intended path; adding a
second reviewer is what makes the requirement real.

## Rollout

**Phase 1 — now.** Land the repository files and apply a ruleset with no
required status checks: pull request required, one approval, CODEOWNERS review,
dismiss stale approvals on push, conversation resolution required, force-push
and deletion blocked, admin bypass. Review requirements take effect immediately
and nothing can deadlock on a missing check.

**Phase 2 — after PRs #23 and #24 merge.** Add `web`, `api`, and `coverage` to
the ruleset's required status checks. This is an edit to the existing ruleset,
not a new one.

**Phase 3 — after the lint/format issue is resolved.** Add the lint and format
job names to the required checks.

## Artifacts

### Committed to the repository

- **`.github/CODEOWNERS`** — `* @distributeaid/reviews-zagreus`
- **`.github/workflows/pr-title-check.yml`** — the website's workflow, with the
  job guarded by `if: ${{ !endsWith(github.actor, '[bot]') }}` so Dependabot and
  other bots are exempt.
- **`.github/pull_request_template.md`** — the website's template, with "How
  will this change be visible?" adapted for a web + API monorepo.
- **`.github/workflows/dependabot-auto-merge.yml`** — enables auto-merge on
  Dependabot pull requests so they land on green CI without consuming review
  cycles.
- **`.github/rulesets/main.json`** — the ruleset in GitHub's import format, at
  its phase 1 state: `required_status_checks` is present but empty. Phase 2
  fills it in, so the file stays the checked-in record of what is configured.
- **`docs/branch-protection.md`** — what to click, in what order, and how to
  verify it took effect.

### Applied by hand

These cannot be done from this session and are prerequisites, not follow-ups:

1. Create `@distributeaid/reviews-zagreus` and add at least one member who is
   not the usual PR author.
2. Import `.github/rulesets/main.json` on `main`.
3. Enable **Allow auto-merge** in repository settings — the Dependabot
   auto-merge workflow silently does nothing without it.

### Tracked separately

A GitHub issue for adding eslint, prettier, `dotnet format
--verify-no-changes`, and root-level `lint` / `check:format` / `check:types`
scripts. This is what closes the remaining gap against the website's `checks`
job, and it is real work: the repository has no lint or format configuration of
any kind today, and the first run will touch a large share of the tree.

## Out of scope

- **The website's issue templates.** Work in this repository originates from
  specs in `docs/superpowers/specs/`, not issue forms.
- **The website's deploy workflows.** Deployment is not a review requirement.
- **Changes to PRs #23 and #24.** Their CI jobs are consumed by name in phase 2;
  the PRs themselves are untouched.

## Verification

The design is correctly implemented when:

1. A pull request from a non-admin cannot merge to `main` without an approving
   review from a `reviews-zagreus` member.
2. A direct push to `main` from a non-admin is rejected.
3. A pull request with a malformed title or no linked issue fails the PR title
   check — and a Dependabot pull request does not.
4. After phase 2, a pull request with a failing `web`, `api`, or `coverage` job
   cannot merge.
