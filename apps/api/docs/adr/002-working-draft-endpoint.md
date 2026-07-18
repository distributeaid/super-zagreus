# ADR-002: A project "working draft" endpoint for the needs-editing flow

**Status:** Accepted  
**Date:** July 2026  
**Authors:** Corey Hindersinn, Claude (Anthropic)  
**Project:** DistributeAid Needs Assessment

---

## Context

With authentication and the dashboard shipped, the next slice is **needs intake**: the
dashboard's "Review & confirm needs" call-to-action must open an editable needs list that
the partner edits and then confirms.

The backend already models this as an assessment lifecycle:

- A **draft** assessment is editable — items are added, edited, and removed against it, and
  each change is persisted immediately (this is the product's "auto-save").
- **Submit** ("confirm my current needs") flips the draft to **Submitted**, which is
  **immutable**, and resets the freshness clock. The latest submitted assessment is "current".
- To change a confirmed list, the partner starts a **new** draft.

The PRD (§4, §5.3) calls for a **living list**: when a returning hub opens the editor to
update, it should start **pre-filled from their last confirmed needs** so they tweak deltas,
not rebuild from scratch. We accepted that requirement in brainstorming for this slice.

The existing endpoint `POST /api/projects/{projectId}/assessments` creates an **empty**
draft — it records an optional `SupersedesId` link but does **not** copy the prior list's
items. Delivering the living-list experience on top of the current endpoints would push the
orchestration entirely into the frontend: find any open draft; if none, create one and copy
the current assessment's items with one add-item call each. That approach has three problems:

- **Non-atomic seeding.** A failure partway through the per-item copy leaves a half-seeded
  draft that looks like a real (but wrong) list.
- **Chatty.** N+2 round-trips to open a list of N items.
- **Duplicate-draft races.** Two opens (two tabs, a double-click, a returning session)
  can each create a draft, leaving orphans and ambiguity about which is "the" list.

We considered where to put the get-or-create-and-seed logic: the frontend, or the backend.

---

## Decision

Add a backend **"working draft" endpoint** that returns the project's single editable draft:

- If an **open draft already exists** for the project, return it (with its items) — the
  partner resumes where they left off.
- Otherwise, **create one atomically**, seeded with a copy of the latest **Submitted**
  assessment's items (item type, quantity, unit, notes) and linked via `SupersedesId`. If
  there is no prior submitted assessment, the new draft is empty.

The endpoint has **get-or-create semantics** and enforces the invariant **"at most one open
draft per project."** The frontend fetches the editable list in a single call and then uses
the existing item endpoints (`POST`/`PATCH`/`DELETE .../items`) and `submit` unchanged.

Proposed shape (final route/verb settled in the implementation plan):
`POST /api/projects/{projectId}/assessments/working-draft` → `200`/`201` with the draft + items.

Submitted assessments remain **immutable**; this decision only concerns how the *editable*
draft is obtained and seeded.

---

## Reasoning

### Atomic seeding

Copying the prior list's items server-side, in one transaction with the draft's creation,
removes the half-seeded-draft failure mode entirely. The client never observes a partially
built list.

### One source of truth for "the editable list"

Get-or-create + seed lives in one place, behind one call. The frontend does not orchestrate
lifecycle logic, and the "at most one open draft per project" invariant is enforced where the
data lives rather than hoped for across clients.

### Prevents duplicate/orphan drafts

Because the server returns the existing open draft instead of blindly creating, concurrent
opens converge on the same draft. This avoids orphaned drafts and the "which draft is current"
ambiguity that a create-first frontend flow invites.

### Preserves the existing model

Submitted assessments stay immutable, `SupersedesId` keeps the history chain, and org-scoping
and validation reuse the patterns already in `AssessmentsController`. The living-list UX is a
thin layer over the existing draft/submit machinery, not a change to it.

---

## Alternatives considered

### Seed-on-create flag

Extend `POST .../assessments` to copy the superseded list's items when `SupersedesId` is
provided. Smaller change, but less encapsulated: the frontend still has to find-open-draft-
else-create and still owns the duplicate-draft guard. The invariant ends up split between
client and server.

### Pure frontend copy

No backend change: the frontend creates a draft, reads the current items, and POSTs each one.
Keeps the slice 100% frontend but reintroduces all three problems above (non-atomic, chatty,
race-prone). Rejected as fragile for a core flow.

---

## Consequences

### Positive

- Robust living-list experience: resume-or-seed in a single, atomic call.
- Simpler frontend with no lifecycle orchestration or seeding edge cases.
- The "one open draft per project" invariant is enforced server-side.
- Submitted-assessment immutability and the `SupersedesId` history chain are unchanged.

### Negative / risks

- The endpoint's get-or-create semantics need explicit concurrency handling so two racing
  requests cannot both create a draft (e.g. a guard/uniqueness check inside the transaction).
- Seeding must copy item fields faithfully; a future field on `AssessmentItem` must be added
  to the copy or it will be silently dropped from seeded drafts.
- "Seed from current" defines *current* as the latest **Submitted** assessment, consistent
  with `GET .../assessments/current`; that coupling should stay in sync if the definition ever
  changes.

---

## Review cadence

Revisit if we expose **multiple projects per hub** in the UI, add **partial confirmation /
per-item freshness**, or support **multiple concurrent editors per hub** (which would push
this toward explicit draft locking rather than a single shared working draft).
