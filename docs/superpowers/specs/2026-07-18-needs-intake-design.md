# Needs Intake — Edit & Confirm (design)

**Status:** Approved (brainstorming)
**Date:** 2026-07-18
**Depends on:** the shipped auth + dashboard slice; [ADR-002](../../../apps/api/docs/adr/002-working-draft-endpoint.md)

## Goal

Make the dashboard's "Review & confirm needs" call-to-action functional: a partner opens
their project's needs list, reviews and edits it as a **living list** (auto-saved), and
**confirms** it — snapshotting history and resetting the 90-day freshness clock. This closes
the core loop the dashboard already advertises (PRD §5.3).

## Scope

**In scope**

- One small backend addition: a **project "working draft" endpoint** (ADR-002).
- A `/needs` page with a **View → Edit → Confirm** loop, wired to existing item endpoints.
- Search-only catalog picker; needs recorded against the catalog with the item's **locked
  default unit** (partner enters quantity only).
- Tests on both sides, following the repo [style guide](../../../docs/style-guide.md).

**Out of scope (deferred, already in PRD §8)**

- Browse-by-category catalog picker — [issue #8](https://github.com/distributeaid/super-zagreus/issues/8).
- CSV export and reporting totals (PRD §5.4).
- Missing-item request (needs a new backend entity + endpoint).
- Multiple projects per hub in the UI.

## User flow & routes

`/needs` — guarded by extending the proxy matcher to
`["/dashboard/:path*", "/needs/:path*"]`. Single page, two states:

1. **View (read-only current)** — the last confirmed needs, grouped by category, with a
   "Last confirmed …" line. A new hub (no submitted assessment) sees an empty state. A
   prominent **"Review & update needs"** button enters Edit. **The dashboard CTA links here**
   (lands in View), so visiting `/needs` never creates a draft by itself.
2. **Edit (working draft)** — "Review & update needs" calls the working-draft endpoint
   (get-or-create, seeded from current), and the list becomes editable: add, adjust quantity,
   remove — each change **auto-saves**. A **"Confirm current needs"** button submits.
3. **Confirm → View** — submit flips the draft to the new current; the page returns to
   read-only View with a "Confirmed just now" message. The dashboard's freshness badge reads
   "Up to date" on next load.

The page chooses its state on load: if an **open draft already exists** for the project,
show Edit (resume); otherwise show View. This keeps draft creation deliberate.

## Backend — working-draft endpoint

Per [ADR-002](../../../apps/api/docs/adr/002-working-draft-endpoint.md):

`POST /api/projects/{projectId}/assessments/working-draft`

- Returns the project's **open draft** (with items, including item type and unit) if one
  exists; otherwise **atomically creates** a draft seeded with a copy of the latest
  **Submitted** assessment's items (item type, quantity, unit, notes), linked via
  `SupersedesId`. Empty when there is no prior submitted assessment.
- Org-scoped like the rest of `AssessmentsController`; enforces **at most one open draft per
  project**; the get-or-create is concurrency-guarded so two racing requests cannot both
  create a draft.
- Response shape matches `GetById` (items with `ItemType` and `Unit`).

Everything else uses the **existing** endpoints unchanged: `POST/PATCH/DELETE
.../assessments/{id}/items` (auto-save), `POST .../assessments/{id}/submit` (confirm),
`GET .../assessments/current` (View), `GET /api/categories` (catalog).

**Tests (xUnit, `TestBase` + `ApiFactory` + `JwtHelper`):** creates-when-none;
seeds-items-from-current; resumes-existing-draft (no duplicate); empty-when-no-prior;
cross-org access returns 404. Seed rows satisfy FKs (Organisation → Project → Assessment).

## Frontend — data & mutation architecture

- **Reads** stay server-side via `apiGet` in server components (current needs, open-draft
  lookup, catalog).
- **Mutations** extend the server-only API client with `apiPost` / `apiPatch` / `apiDelete`
  (same Bearer attach + `401 → /login` handling as `apiGet`), called from **server actions**:
  `enterEdit` (working-draft), `addNeed`, `updateQuantity`, `removeNeed`, `confirmNeeds`.
  Client components invoke the actions and the route revalidates. The app JWT never leaves
  the server — consistent with the existing proxy pattern.
- **Auto-save** = each edit calls its action immediately, with optimistic UI and an inline
  error fallback on failure (never silently drop an edit).

## Components

- **NeedsPage** (server) — loads current + any open draft; renders View or Edit.
- **CurrentNeeds** (server) — read-only grouped summary + "Last confirmed …" / empty state +
  "Review & update needs" button.
- **NeedsEditor** (client) — the editable draft: category groups, add-need control, confirm.
- **AddNeed** (client) — **search-only typeahead** over the flattened catalog; selecting an
  item adds it with its `defaultUnit` and quantity defaulting to 1.
- **NeedRow** (client) — item name + unit, quantity input (auto-save on change), remove.
- **ConfirmButton** (client) — disabled when the list is empty (mirrors the API's ≥1-item
  rule).

A small pure **catalog helper** flattens the grouped `GET /api/categories` response into a
searchable list (`{ id, name, category, defaultUnit }`) and filters by name.

## Error handling

- Auto-save failure → keep the edit on screen, show an inline error + retry.
- `submit` with zero items → blocked in the UI and rejected by the API.
- Any `401` → existing `/login` redirect (via the extended API client).
- Catalog / draft load failure → the existing dashboard-style error boundary
  (`error.tsx` sibling for `/needs`).

## Testing

- **Backend:** the working-draft xUnit tests above.
- **Frontend (Vitest, `describe`/`it`, docstrings):** the catalog flatten+search helper;
  `NeedRow`; `AddNeed`; and the View-vs-Edit selection logic. Server-action core logic is
  extracted into testable functions where practical (as with `sessionExchange`), so the
  mutation behavior is unit-covered without booting Next.

## References

- [ADR-002 — working-draft endpoint](../../../apps/api/docs/adr/002-working-draft-endpoint.md)
- PRD §4, §5.2–5.4; technical design spec
- Existing endpoints in `apps/api/DA.NA.Api/Controllers/AssessmentsController.cs`,
  `ReferenceDataController.cs`
- Deferred: [issue #8](https://github.com/distributeaid/super-zagreus/issues/8)
