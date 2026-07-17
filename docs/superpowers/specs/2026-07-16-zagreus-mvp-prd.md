# Zagreus — MVP Product Requirements Document

**Product:** Zagreus — partner portal for DistributeAid (distributeaid.org)
**Document status:** Draft for review — v0.2 (reconciled with existing `zagreus-fe` / `zagreus-be` prototypes)
**Date:** 2026-07-16
**Author:** Corey (with Claude)
**Companion:** see the technical design spec, `2026-07-16-zagreus-mvp-technical-design.md`

---

## 1. Overview

Zagreus is a web application that lets DistributeAid (DA) partner organizations report their humanitarian aid needs to DA. Partners maintain their current needs — selected from a DA-curated catalog — and confirm them so DA always knows what each partner needs and how fresh that information is. DA uses this data for reporting today and, in future versions, to power aid sourcing and predictive modeling.

The MVP proves one tight loop: **a partner can record and confirm their aid needs, and see those needs reported back clearly.** Everything beyond that loop — impact modeling, fulfillment tracking, staff tooling — is deliberately deferred.

This version reconciles the original product scope with two existing prototypes: a Next.js frontend (`zagreus-fe`) and a .NET 8 / ASP.NET Core / PostgreSQL backend (`zagreus-be`) that already implements a data model and API. Both are being **consolidated into the single `super-zagreus` monorepo** (frontend in `apps/web`, backend in `apps/api`); see the technical design spec for that decision. Where the two prototypes diverged from the original scope, the reconciled decisions are recorded in Section 10.

## 2. Goals & non-goals

### Goals
- Give DA-invited partners a simple, reliable way to record and confirm their current aid needs.
- Use DA's existing item taxonomy so needs are captured in a structured, DA-usable form from day one.
- Give partners a clean view of their own needs, exportable for their own use.
- Track the freshness of each partner's needs so DA knows when data is stale.
- Ship a small, well-bounded product that validates the core concept before investing in modeling and staff tooling.

### Non-goals (MVP)
- No aid sourcing, procurement, or predictive-modeling features.
- No impact/value metrics shown to partners.
- No fulfillment or delivery tracking.
- No DA staff-facing admin interface inside the app (DA uses provisioning scripts + the API for now).
- No in-app user management or self-service password reset (handled by DA via scripts for now).
- No modeling of the frontline groups a hub serves.

## 3. Users & access

**Target partner:** a **hub** — a mid-tier organization that aggregates and distributes aid onward to local frontline groups. Hubs are the MVP's target partners because they have the administrative capacity to report reliably. (Frontline groups are not modeled in the MVP.)

**Primary user:** a person at a hub organization who records and confirms the hub's needs.

- Partner accounts are **provisioned by DA** — there is no public sign-up.
- The backend already supports multiple users per organization and DA/organization roles, but the **MVP frontend is login-only**: it does not expose in-app user management. DA provisions the hub's user(s).
- To keep DA's manual overhead low, the MVP includes **admin/ops scripts** (in the API app, `apps/api`) to (a) provision a hub organization and its first admin user, and (b) reset/recreate a user's credentials. Self-service reset is future work.
- Organization users only ever see data belonging to their own organization.
- DA staff are not users of the MVP interface; a staff-facing view is future work.

## 4. Core concepts & data model

**Organization (hub)** — a partner DA works with. Owns its users, projects, and needs data.

**Project** — a unit of work under a hub, carrying a **region**. Needs are recorded per project. The backend supports multiple projects per hub; the **MVP UI exposes a single default project per hub** to keep the first experience simple. Multiple-project UI is future work.

**Catalog item (item type)** — an entry in the DA-curated catalog, seeded as reference data in the backend. Each item carries the fields partners need to make a selection:
- Category (e.g., Health, Hygiene, Household, Cleaning, Clothing, Kitchen, Baby, Education)
- Item name and variant attributes where they exist (e.g., Gender, Style/size)
- A **default unit of measurement**

> An outstanding **task** (not a design decision): DA reconciles the "Needs Reporting Categories" taxonomy sheet against the backend's seeded categories/item types so the two match. The taxonomy's modeling/reference columns (USD value, weight, volume, impact factors) remain internal to DA and are excluded from the MVP catalog; they will power future impact reporting.

**Need (item)** — a single line item of need:
- Reference to a catalog item (required)
- Quantity (required), entered in the item's **locked default unit** for the MVP (the partner does not choose the unit)

**Needs list** — the partner's current set of needs for their project. The partner edits it like a **living list**, then **confirms** it ("confirm my current needs"). Confirming snapshots the current state for history and **resets the freshness/staleness clock**. Between confirmations the partner can freely edit. This gives a living-list experience while retaining dated history that DA can trend over time.

**Missing-item request** — when a partner needs an item not in the catalog, they submit a **dedicated, structured request** captured separately for DA to review and, if appropriate, add to the catalog. It does not create a need until DA acts on it.

## 5. Functional requirements

### 5.1 Authentication & accounts
- DA provisions a hub organization and its user(s) via admin scripts; the partner receives credentials.
- The partner can log in securely and log out.
- All data a user sees is scoped to their own organization; no partner can see another partner's data.
- Password reset for the MVP is DA-assisted via an admin script (no self-service reset in the UI).

### 5.2 Catalog
- The app's catalog is the backend's seeded categories / item types.
- Partners can browse the catalog by category and search by item name.
- The catalog is read-only to partners; only DA controls its contents.

### 5.3 Intake — recording needs
- View the current needs for the hub's project, grouped/sortable by category.
- Add a need: select a catalog item (browse or search), enter a quantity in the item's locked default unit, optionally add a location note, urgency/needed-by, and item notes.
- Edit or remove a need.
- **Confirm current needs**: submit the current set, which snapshots it for history and resets the freshness clock.
- Request an item not in the catalog via a dedicated structured form; the request is stored for DA review.

### 5.4 Reporting — viewing needs
- A clean summary of the hub's current needs, grouped by category, with per-category and overall quantity totals.
- Show when the needs were **last confirmed** (freshness).
- Export the current needs to **CSV** (generated client-side for the MVP).
- (PDF export is a candidate for a later version unless prioritized into MVP.)

### 5.5 Design & UX
- The UI must follow the **DA Design Guidelines**: Primary DA Blue `#051E5D`; secondary palette `#DFCDE8`, `#98BEC6`, `#5AC597`; **Roboto** for body/UI type with **Permanent Marker** as a sparing accent; the 8/16/32/64px spacing scale; and the 12/8/4-column responsive grid. Details in the technical spec.

## 6. Key user stories

- As a hub user, I can log in with the account DA gave me and see my organization's current needs.
- As a hub user, I can add a need by finding the right item in DA's catalog and entering how many I need.
- As a hub user, I can note where something is needed and how urgently, without being forced to.
- As a hub user, when the item I need isn't listed, I can request it so DA can add it.
- As a hub user, I can keep my needs up to date and confirm them so DA knows they're current.
- As a hub user, I can see a clean summary of my needs by category, see when I last confirmed them, and export to CSV.

## 7. Success criteria

- DA can provision a hub (via script), and that hub can independently log in and record and confirm a complete set of needs without DA hand-holding.
- Needs are captured against the structured catalog (not free text) for the large majority of items.
- Hubs return to update and re-confirm their needs over time, and DA can see freshness.
- DA can retrieve partner needs data in a structured, usable form.

*(Specific target numbers to be set by DA before launch.)*

## 8. For later / future versions

- **Impact & value reporting** — derived metrics from DA's modeling data (USD value, weight/volume, people served) in reporting. (`DA.NA.Analytics`)
- **Needs vs. fulfillment tracking** — what DA has sourced/delivered against each need.
- **DA staff-facing view** — an in-app admin interface across all partners.
- **In-app user management** — a hub admin invites/manages its own users (backend already supports this).
- **Self-service password reset** — email-based (DA's stack uses Resend).
- **Multiple projects per hub in the UI** — expose the backend's multi-project support.
- **Unit flexibility** — unit override per item and, importantly, letting partners work in their **own local units** rather than DA's normalized defaults (adds UX and data-normalization complexity).
- **Frontline-group modeling** — attribute a hub's needs to the specific frontline groups it serves.
- **Structured urgency/timeframes** — replace the lightweight urgency field with structured needed-by dates and levels.
- **PDF export** and richer report formats.
- **Sourcing & predictive modeling integration** — feed needs data into DA's sourcing and forecasting.

## 9. Open questions

- Concrete launch success targets (hubs onboarded, needs recorded/confirmed).
- Data-retention, privacy, and hosting requirements DA needs reflected before launch.
- Is CSV sufficient for the MVP export, or is PDF needed at launch?
- Final mapping of the taxonomy sheet to the backend's seeded categories/units.

## 10. Reconciliation log (PRD ↔ existing prototypes)

The MVP scope was reconciled against the `zagreus-be` prototype's existing data model. Decisions:

1. **Needs time model** — *Hybrid.* Living-list UX over the backend's submitted assessment; "confirm my current needs" snapshots history and resets the staleness clock. (Original PRD: pure standing list. Backend: draft→submit assessment + current assessment.)
2. **Structure** — *Adopt org → project(region), one project per hub in the MVP UI.* Multi-project supported underneath; frontline groups deferred. (Original PRD: one flat list, free-text location, multi-site deferred.)
3. **Accounts & auth** — *Login-only frontend against the existing JWT API; DA provisions via admin scripts; no in-app user management or self-service reset in MVP.* (Original PRD: one login per org, basic reset. Backend: multi-user orgs + roles, no reset yet.)
4. **Units** — *Locked default unit per item for MVP;* unit override and partner-local units are future work. (Backend: explicit unit per item from a fixed list.)
5. **Catalog & missing items** — *Catalog = backend seeded reference data; dedicated structured missing-item request.* (Original PRD: seed a clean catalog + free-text request flag.)
