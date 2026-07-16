# Zagreus — MVP Product Requirements Document

**Product:** Zagreus — partner portal for DistributeAid (distributeaid.org)
**Document status:** Draft for review
**Date:** 2026-07-16
**Author:** Corey (with Claude)

---

## 1. Overview

Zagreus is a web application that lets DistributeAid (DA) partner organizations report their humanitarian aid needs to DA. Partners maintain a living list of the items they need, selected from a DA-curated catalog. DA uses this data today for reporting and, in future versions, to power aid sourcing and predictive modeling.

The MVP proves one tight loop: **a partner can submit and maintain their aid needs, and see those needs reported back clearly.** Everything beyond that loop — impact modeling, fulfillment tracking, staff tooling — is deliberately deferred.

## 2. Goals & non-goals

### Goals
- Give DA-invited partners a simple, reliable way to record and maintain their current aid needs.
- Use DA's existing item taxonomy so needs are captured in a structured, DA-usable form from day one.
- Give partners a clean view of their own needs, exportable for their own use.
- Ship a small, well-bounded product that validates the core concept before investing in modeling and staff tooling.

### Non-goals (MVP)
- No aid sourcing, procurement, or predictive-modeling features.
- No impact/value metrics shown to partners.
- No fulfillment or delivery tracking.
- No DA staff-facing admin interface inside the app.
- No partner self-registration.
- No multi-user partner organizations.

## 3. Users & access

**Primary user:** a single authorized person at a partner organization.

- Partner accounts are **provisioned/invited by DA** — there is no public sign-up.
- **One login per partner organization** in the MVP. (Multiple named users per org is a future version.)
- DA staff are not users of the MVP interface. Staff work with the underlying data outside the app for now; a staff-facing view is future work.

## 4. Core concepts & data model

**Partner** — an organization DA works with. Owns one account and one needs list.

**Catalog item** — an entry in the DA-curated item catalog. In the MVP the catalog is a **clean, seeded list** derived from DA's "Needs Reporting Categories" taxonomy. Each item carries only the fields partners need to make a selection:
- SDR Category (e.g., Health, Hygiene, Household, Cleaning, Clothing, Kitchen, Baby, Education)
- Item name (e.g., "Diaper," "Wound Dressing," "Scrub Shirt")
- Variant attributes where they exist (e.g., Gender, Style/size)
- Default unit of measurement

> The taxonomy's modeling/reference columns (USD value, weight, volume, impact factors, needs-met math) are **intentionally excluded** from the MVP catalog. They belong to DA's internal modeling and will power future impact reporting.

**Need** — a single line item on a partner's needs list:
- Reference to a catalog item (required)
- Quantity, expressed in the item's default unit (required)
- Location note — optional, free text
- Urgency / needed-by — optional, lightweight (e.g., a free-text or simple flag)
- Standard record metadata (created/updated timestamps)

**Needs list** — the partner's standing, living collection of needs. It is edited in place over time rather than resubmitted each period; the list always reflects the partner's current needs.

**Catalog-addition request** — when a partner needs an item not in the catalog, they submit a free-text request. This is captured for DA to review and, if appropriate, add to the catalog later. It does not create a structured need until DA acts on it.

## 5. Functional requirements

### 5.1 Authentication & accounts
- DA provisions a partner account and invites the partner user (e.g., via emailed invitation).
- The partner user can log in securely and log out.
- The partner user can perform a basic account recovery (e.g., password reset), scoped to a single user per org.
- All data a user sees is scoped to their own partner organization; no partner can see another partner's needs.

### 5.2 Catalog (seeded)
- The app is seeded with DA's curated catalog (SDR Category → Item → variant → default unit).
- Partners can browse the catalog by category and search by item name.
- The catalog is read-only to partners; only DA controls its contents. In a future version the catalog will be managed in **Strapi** and served to Zagreus; the MVP may use a simple seed import as a stand-in.

### 5.3 Intake — managing the needs list
- View the current needs list, grouped/sortable by category.
- Add a need: select a catalog item (via browse or search), enter a quantity in the item's default unit, optionally add a location note and urgency/needed-by.
- Edit an existing need (quantity, optional fields).
- Remove a need.
- Request an item not in the catalog via a free-text form; the request is stored for DA review.
- Changes are saved to the standing list and immediately reflected in the partner's view.

### 5.4 Reporting — viewing needs
- A clean summary of the partner's own current needs, grouped by SDR Category, with per-category and overall quantity totals.
- Export the current needs list to **CSV**.
- (PDF export is a candidate for a later version unless prioritized into MVP.)

## 6. Key user stories

- As a partner, I can log in with the account DA gave me and see my organization's current needs list.
- As a partner, I can add a need by finding the right item in DA's catalog and entering how many I need.
- As a partner, I can note where something is needed and how urgently, without being forced to.
- As a partner, when the item I need isn't listed, I can request it so DA can add it.
- As a partner, I can keep my list up to date as my needs change.
- As a partner, I can see a clean summary of my needs by category and export it to share internally.

## 7. Success criteria

- DA can provision a partner, and that partner can independently log in and record a complete needs list without DA hand-holding.
- Needs are captured against the structured catalog (not free text) for the large majority of items.
- Partners return to update their list over time, indicating the standing-list model works.
- DA can retrieve partner needs data in a structured, usable form.

*(Specific target numbers to be set by DA before launch.)*

## 8. For later / future versions

Captured here so scope stays clean but the direction is on record:

- **Impact & value reporting** — surface derived metrics from DA's modeling data (estimated USD value, total weight/volume, people served) in the partner's reporting view.
- **Needs vs. fulfillment tracking** — show what DA has sourced/delivered against each need (fulfilled vs. outstanding). Depends on staff tooling to enter fulfillment data.
- **DA staff-facing view** — an in-app admin interface for DA to see and manage submissions across all partners.
- **Multi-user partner organizations** — multiple named logins per partner org, sharing one needs list.
- **Strapi-managed catalog** — move catalog management to Strapi as the source of truth, served to Zagreus.
- **Structured location/sites** — replace the free-text location note with structured sites/locations per partner.
- **Structured urgency/timeframes** — replace the lightweight urgency field with structured needed-by dates and urgency levels.
- **Beneficiary-count framing** — allow partners to express needs in people/days served, with DA deriving item quantities.
- **Partner self-registration** — onboarding without manual DA provisioning.
- **PDF export** and richer report formats.
- **Sourcing & predictive modeling integration** — feed needs data into DA's sourcing and forecasting efforts.

## 9. Open questions

- What is the exact set of fields DA wants in the seeded MVP catalog, and who curates the clean list from the taxonomy sheet?
- What are the concrete launch success targets (number of partners onboarded, needs recorded, etc.)?
- Any data-retention, privacy, or hosting requirements DA needs reflected before launch?
- Is CSV sufficient for the MVP export, or is PDF needed at launch?
