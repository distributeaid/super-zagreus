# Needs Intake — Edit & Confirm Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the dashboard's "Review & confirm needs" CTA functional — a partner opens `/needs`, reviews their project's needs as an auto-saved living list, and confirms it, resetting the freshness clock.

**Architecture:** One new backend endpoint (`POST .../assessments/working-draft`, get-or-create seeded from the last confirmed list — [ADR-002](../../../apps/api/docs/adr/002-working-draft-endpoint.md)); everything else reuses existing item/submit/catalog endpoints. The frontend `/needs` page has a **View → Edit → Confirm** loop: server components read data, server actions mutate through a server-only API client (`apiPost`/`apiPatch`/`apiDelete`), keeping the app JWT server-side.

**Tech Stack:** .NET 8, EF Core, xUnit; Next.js 16, React 19, TypeScript, Auth.js, Vitest + Testing Library.

**Spec:** [docs/superpowers/specs/2026-07-18-needs-intake-design.md](../specs/2026-07-18-needs-intake-design.md)

## Global Constraints

- **Backend:** .NET 8; PostgreSQL; EF Core migrations via `dotnet ef`; tests are xUnit in `DA.NA.Tests` using the existing `TestBase` + `ApiFactory` (shared in-memory SQLite) + `JwtHelper` pattern. Seeded rows must satisfy FKs (Organisation → Project → NeedsAssessment → AssessmentItem, and ItemType needs a DefaultUnit). Solution builds **warnings-as-errors** (no unused usings, no async-without-await). `NeedsAssessment.Status` is stored as a **string** via `HasConversion<string>()`.
- **Frontend:** Node 20–24; Yarn 4; Next.js `^16.2.2`; React `^19.2.0`; TypeScript. The app JWT stays **server-side** (server components read via `apiGet`; mutations go through server actions calling the server-only API client). No raw hex in components — use DA Tailwind tokens (`da-blue`, `da-lavender`, `da-teal`, `da-green`, `da-sm/md/lg/xl`).
- **Tests:** Vitest + React Testing Library. Every test lives under a `describe` block and uses `it` (not `test`). Every exported function/constant has a docstring. (See [docs/style-guide.md](../../../docs/style-guide.md).)
- **Commits:** Conventional Commits. **License:** AGPL-3.0-only.

---

# Phase A — Backend: working-draft endpoint

## Task A1: `POST .../assessments/working-draft` + one-open-draft guard

**Files:**
- Modify: `apps/api/DA.NA.Api/Controllers/AssessmentsController.cs` (add `WorkingDraft`; project `GetById` to a DTO to avoid the serialization cycle)
- Modify: `apps/api/DA.NA.Core/Data/AppDbContext.cs` (filtered unique index: one open draft per project)
- Create: migration via `dotnet ef` (generated file under `apps/api/DA.NA.Core/Migrations/`)
- Test: `apps/api/DA.NA.Tests/Assessments/WorkingDraftTests.cs`

**Interfaces:**
- Produces: `POST /api/projects/{projectId}/assessments/working-draft` → `200`/`201` with an **assessment-with-items DTO**: `{ id, status, supersedesId, createdAt, submittedAt, items: [{ id, itemTypeId, quantity, unitId, itemType: { name, category }, unit: { name } }] }`. Returns the existing open draft if one exists; otherwise creates a draft seeded (copied items) from the latest **Submitted** assessment, `supersedesId` set (empty items if no prior). Org-scoped (cross-org → `404`). `GetById` returns the **same DTO shape**.

- [ ] **Step 1: Write the failing tests**

Create `apps/api/DA.NA.Tests/Assessments/WorkingDraftTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using DA.NA.Tests.Infrastructure;
using Xunit;

namespace DA.NA.Tests.Assessments;

public class WorkingDraftTests : TestBase
{
    // Seeds an org, a project, one item type + unit, and returns their ids.
    private async Task<(Guid orgId, Guid projectId, Guid itemTypeId, Guid unitId)> SeedCatalogAsync()
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var itemTypeId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = DateTime.UtcNow });
            db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Main", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow });
            db.Units.Add(new Unit { Id = unitId, Name = "item", Dimension = UnitDimension.Count, ToBaseFactor = 1 });
            db.ItemTypes.Add(new ItemType { Id = itemTypeId, Category = "Hygiene", Name = "Soap", DefaultUnitId = unitId });
            return Task.CompletedTask;
        });
        return (orgId, projectId, itemTypeId, unitId);
    }

    private record ItemDto(Guid id, Guid itemTypeId, decimal quantity);
    private record DraftDto(Guid id, string status, Guid? supersedesId, List<ItemDto> items);

    [Fact]
    public async Task Creates_an_empty_draft_when_none_exists_and_no_prior_submitted()
    {
        var (orgId, projectId, _, _) = await SeedCatalogAsync();
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var res = await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var draft = await res.Content.ReadFromJsonAsync<DraftDto>();
        Assert.Equal("Draft", draft!.status);
        Assert.Empty(draft.items);
    }

    [Fact]
    public async Task Seeds_items_from_the_latest_submitted_assessment()
    {
        var (orgId, projectId, itemTypeId, unitId) = await SeedCatalogAsync();
        var submittedId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.NeedsAssessments.Add(new NeedsAssessment
            {
                Id = submittedId, ProjectId = projectId, CreatedBy = Guid.NewGuid(),
                Status = AssessmentStatus.Submitted, CreatedAt = DateTime.UtcNow.AddDays(-1), SubmittedAt = DateTime.UtcNow.AddDays(-1),
                Items = { new AssessmentItem { Id = Guid.NewGuid(), ItemTypeId = itemTypeId, UnitId = unitId, Quantity = 5, CreatedAt = DateTime.UtcNow.AddDays(-1) } }
            });
            return Task.CompletedTask;
        });
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var res = await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null);

        var draft = await res.Content.ReadFromJsonAsync<DraftDto>();
        Assert.Equal("Draft", draft!.status);
        Assert.Equal(submittedId, draft.supersedesId);
        var item = Assert.Single(draft.items);
        Assert.Equal(itemTypeId, item.itemTypeId);
        Assert.Equal(5, item.quantity);
    }

    [Fact]
    public async Task Resumes_the_existing_open_draft_instead_of_creating_a_second()
    {
        var (orgId, projectId, _, _) = await SeedCatalogAsync();
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var first = await (await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null))
            .Content.ReadFromJsonAsync<DraftDto>();
        var second = await (await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null))
            .Content.ReadFromJsonAsync<DraftDto>();

        Assert.Equal(first!.id, second!.id);
    }

    [Fact]
    public async Task Cross_org_access_returns_404()
    {
        var (_, projectId, _, _) = await SeedCatalogAsync();
        var otherOrg = Guid.NewGuid();
        var client = ClientFor(JwtHelper.ForOrgAdmin(otherOrg));

        var res = await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
```

> If `UnitDimension.Count` is not a member, open `apps/api/DA.NA.Core/Entities/Enums.cs` and use the first defined `UnitDimension` value instead — the test only needs a valid unit.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test apps/api/DistributeAid.NeedsAssessment.sln --filter WorkingDraftTests`
Expected: FAIL — the route returns `404`/`405` (endpoint missing).

- [ ] **Step 3: Add the shared DTO projection and the `WorkingDraft` action; project `GetById`**

In `apps/api/DA.NA.Api/Controllers/AssessmentsController.cs`, add a private projection helper and the action. Add these members inside the class:
```csharp
// Shared "assessment with items" response shape — projected (not the tracked entity) to
// avoid the Items[].Assessment serialization cycle. Used by GetById and WorkingDraft.
private static object ToDto(NeedsAssessment a) => new
{
    a.Id,
    Status = a.Status.ToString(),
    a.SupersedesId,
    a.CreatedAt,
    a.SubmittedAt,
    Items = a.Items.Select(i => new
    {
        i.Id, i.ItemTypeId, i.Quantity, i.UnitId,
        ItemType = new { i.ItemType.Name, i.ItemType.Category },
        Unit = new { i.Unit.Name }
    })
};

/// <summary>
/// Return the project's single editable working draft: the existing open draft if one
/// exists, otherwise a new draft seeded with a copy of the latest submitted assessment's items.
/// </summary>
[HttpPost("api/projects/{projectId:guid}/assessments/working-draft")]
public async Task<IActionResult> WorkingDraft(Guid projectId)
{
    var projectOrgId = await ProjectOrgIdAsync(projectId);
    if (projectOrgId is null) return NotFound("Project not found");

    var callerOrgId = User.OrgId();
    if (callerOrgId.HasValue && callerOrgId.Value != projectOrgId.Value)
        return NotFound("Project not found");

    var existing = await _db.NeedsAssessments
        .Include(a => a.Items).ThenInclude(i => i.ItemType)
        .Include(a => a.Items).ThenInclude(i => i.Unit)
        .Where(a => a.ProjectId == projectId && a.Status == AssessmentStatus.Draft)
        .OrderByDescending(a => a.CreatedAt)
        .FirstOrDefaultAsync();
    if (existing is not null) return Ok(ToDto(existing));

    var current = await _db.NeedsAssessments
        .Include(a => a.Items)
        .Where(a => a.ProjectId == projectId && a.Status == AssessmentStatus.Submitted)
        .OrderByDescending(a => a.SubmittedAt)
        .FirstOrDefaultAsync();

    var callerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var draft = new NeedsAssessment
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        CreatedBy = callerId,
        Status = AssessmentStatus.Draft,
        SupersedesId = current?.Id,
        CreatedAt = DateTime.UtcNow,
        Items = current is null ? new List<AssessmentItem>() : current.Items.Select(i => new AssessmentItem
        {
            Id = Guid.NewGuid(),
            ItemTypeId = i.ItemTypeId,
            Quantity = i.Quantity,
            UnitId = i.UnitId,
            Notes = i.Notes,
            CreatedAt = DateTime.UtcNow
        }).ToList()
    };
    _db.NeedsAssessments.Add(draft);

    try
    {
        await _db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
        // Lost a race to create the single open draft — return the winner's draft.
        _db.Entry(draft).State = EntityState.Detached;
        var winner = await _db.NeedsAssessments
            .Include(a => a.Items).ThenInclude(i => i.ItemType)
            .Include(a => a.Items).ThenInclude(i => i.Unit)
            .Where(a => a.ProjectId == projectId && a.Status == AssessmentStatus.Draft)
            .OrderByDescending(a => a.CreatedAt)
            .FirstAsync();
        return Ok(ToDto(winner));
    }

    await _db.Entry(draft).Collection(a => a.Items).Query()
        .Include(i => i.ItemType).Include(i => i.Unit).LoadAsync();
    return CreatedAtAction(nameof(GetById), new { id = draft.Id }, ToDto(draft));
}
```

Replace the body of the existing `GetById` action's final `return Ok(assessment);` with `return Ok(ToDto(assessment));` so it no longer returns the tracked entity (avoids the cycle). Leave its `.Include(...)` chain as-is.

- [ ] **Step 4: Add the one-open-draft filtered unique index**

In `apps/api/DA.NA.Core/Data/AppDbContext.cs`, inside `OnModelCreating`, alongside the other `HasIndex` lines, add:
```csharp
// At most one open (Draft) assessment per project. Status is stored as a string.
modelBuilder.Entity<NeedsAssessment>()
    .HasIndex(a => a.ProjectId)
    .HasFilter("\"Status\" = 'Draft'")
    .IsUnique();
```

- [ ] **Step 5: Generate the migration**

Run:
```bash
dotnet ef migrations add AddOneOpenDraftPerProjectIndex \
  --project apps/api/DA.NA.Core --startup-project apps/api/DA.NA.Api
```
Expected: a new migration is created under `apps/api/DA.NA.Core/Migrations/` creating a unique index on `NeedsAssessments (ProjectId)` filtered to `"Status" = 'Draft'`. (Tests build the schema from the model via `EnsureCreatedAsync`, so they pick up the index without applying migrations.)

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test apps/api/DistributeAid.NeedsAssessment.sln --filter WorkingDraftTests`
Expected: all 4 pass. Then run the full suite to confirm no regression (the `GetById` projection change):
`dotnet test apps/api/DistributeAid.NeedsAssessment.sln` — all green.

- [ ] **Step 7: Commit**

```bash
git add apps/api
git commit -m "feat: add working-draft endpoint with one-open-draft guard"
```

---

# Phase B — Frontend: data & mutation layer

## Task B1: API client mutations (`apiPost`/`apiPatch`/`apiDelete`)

**Files:**
- Modify: `apps/web/src/data/apiClient.ts`
- Test: `apps/web/src/data/apiClient.mutations.test.ts`

**Interfaces:**
- Consumes: existing `apiGet` pattern (Bearer from the Auth.js session; `401 → /login`).
- Produces: `apiPost<T>(path, body?): Promise<T>`, `apiPatch<T>(path, body?): Promise<T>`, `apiDelete(path): Promise<void>`. Same auth/redirect handling as `apiGet`; JSON body when provided; `apiDelete` returns nothing on `204`.

- [ ] **Step 1: Write the failing tests**

Create `apps/web/src/data/apiClient.mutations.test.ts`:
```ts
import { apiPost, apiPatch, apiDelete } from "./apiClient";

const { redirectMock, authMock } = vi.hoisted(() => {
  process.env.API_BASE_URL = "http://api.test";
  return {
    redirectMock: vi.fn((url: string) => {
      throw new Error(`REDIRECT:${url}`);
    }),
    authMock: vi.fn(),
  };
});
vi.mock("next/navigation", () => ({ redirect: redirectMock }));
vi.mock("@/auth", () => ({ auth: () => authMock() }));

function fetchReturning(res: Partial<Response> & { status: number; ok: boolean }): typeof fetch {
  return vi.fn(async () => res as Response) as unknown as typeof fetch;
}

describe("apiClient mutations", () => {
  beforeEach(() => {
    redirectMock.mockClear();
    authMock.mockReset();
  });

  it("apiPost sends a bearer token and JSON body and returns parsed JSON", async () => {
    authMock.mockResolvedValue({ apiToken: "app-jwt" });
    const fetchImpl = fetchReturning({ ok: true, status: 201, json: async () => ({ id: "d1" }) });
    vi.stubGlobal("fetch", fetchImpl);

    const body = await apiPost<{ id: string }>("/api/x", { a: 1 });

    expect(body).toEqual({ id: "d1" });
    expect(fetchImpl).toHaveBeenCalledWith(
      "http://api.test/api/x",
      expect.objectContaining({
        method: "POST",
        headers: { authorization: "Bearer app-jwt", "content-type": "application/json" },
        body: JSON.stringify({ a: 1 }),
      }),
    );
    vi.unstubAllGlobals();
  });

  it("apiPatch uses the PATCH method", async () => {
    authMock.mockResolvedValue({ apiToken: "app-jwt" });
    const fetchImpl = fetchReturning({ ok: true, status: 200, json: async () => ({ ok: true }) });
    vi.stubGlobal("fetch", fetchImpl);

    await apiPatch("/api/x/1", { q: 2 });

    expect(fetchImpl).toHaveBeenCalledWith("http://api.test/api/x/1", expect.objectContaining({ method: "PATCH" }));
    vi.unstubAllGlobals();
  });

  it("apiDelete resolves without a body on 204", async () => {
    authMock.mockResolvedValue({ apiToken: "app-jwt" });
    vi.stubGlobal("fetch", fetchReturning({ ok: true, status: 204 }));

    await expect(apiDelete("/api/x/1")).resolves.toBeUndefined();
    vi.unstubAllGlobals();
  });

  it("redirects to /login when the API rejects the token (401)", async () => {
    authMock.mockResolvedValue({ apiToken: "stale" });
    vi.stubGlobal("fetch", fetchReturning({ ok: false, status: 401 }));

    await expect(apiPost("/api/x", {})).rejects.toThrow("REDIRECT:/login");
    vi.unstubAllGlobals();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `yarn workspace @zagreus/web test src/data/apiClient.mutations.test.ts`
Expected: FAIL — `apiPost`/`apiPatch`/`apiDelete` are not exported.

- [ ] **Step 3: Implement the mutation helpers**

In `apps/web/src/data/apiClient.ts`, add below `apiGet` a shared writer and the three exports:
```ts
async function apiSend<T>(method: "POST" | "PATCH" | "DELETE", path: string, body?: unknown): Promise<T> {
  const session = await auth();
  const token = session?.apiToken;
  if (!token) redirect("/login");
  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers: body === undefined
      ? { authorization: `Bearer ${token}` }
      : { authorization: `Bearer ${token}`, "content-type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    cache: "no-store",
  });
  if (res.status === 401) redirect("/login");
  if (!res.ok) throw new Error(`API ${method} ${path} failed: ${res.status}`);
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

/** Server-only POST to the backend API (Bearer attached; `401 → /login`). */
export async function apiPost<T>(path: string, body?: unknown): Promise<T> {
  return apiSend<T>("POST", path, body);
}

/** Server-only PATCH to the backend API (Bearer attached; `401 → /login`). */
export async function apiPatch<T>(path: string, body?: unknown): Promise<T> {
  return apiSend<T>("PATCH", path, body);
}

/** Server-only DELETE to the backend API (Bearer attached; `401 → /login`). */
export async function apiDelete(path: string): Promise<void> {
  await apiSend<void>("DELETE", path);
}
```

- [ ] **Step 4: Run to verify pass**

Run: `yarn workspace @zagreus/web test src/data/apiClient.mutations.test.ts`
Expected: all 4 pass.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/data/apiClient.ts apps/web/src/data/apiClient.mutations.test.ts
git commit -m "feat: add apiPost/apiPatch/apiDelete server-side mutation helpers"
```

## Task B2: Catalog + needs pure helpers

**Files:**
- Create: `apps/web/src/data/catalog.ts`, `apps/web/src/data/catalog.test.ts`
- Create: `apps/web/src/data/needs.ts`, `apps/web/src/data/needs.test.ts`

**Interfaces:**
- Produces:
  - `CatalogItem = { id, name, category, defaultUnit: { id, name } }`; `flattenCatalog(categories)`; `searchCatalog(items, query)`.
  - `NeedItem = { id, itemTypeId, name, category, quantity, unit }`; `toNeedItems(apiItems)`; `groupByCategory(items)`; `selectNeedsMode(assessments)`.
- Consumed by Tasks B3–C-series (components and the page).

- [ ] **Step 1: Write the failing catalog test**

Create `apps/web/src/data/catalog.test.ts`:
```ts
import { flattenCatalog, searchCatalog } from "./catalog";

const CATEGORIES = [
  { category: "Hygiene", items: [
    { id: "soap", name: "Soap", defaultUnit: { id: "u1", name: "item" } },
    { id: "towel", name: "Towel", defaultUnit: { id: "u1", name: "item" } },
  ] },
  { category: "Food", items: [
    { id: "rice", name: "Rice", defaultUnit: { id: "u2", name: "kg" } },
  ] },
];

describe("flattenCatalog", () => {
  it("flattens categories into items carrying their category", () => {
    const flat = flattenCatalog(CATEGORIES);
    expect(flat).toHaveLength(3);
    expect(flat.find((i) => i.id === "rice")).toMatchObject({ name: "Rice", category: "Food", defaultUnit: { id: "u2", name: "kg" } });
  });
});

describe("searchCatalog", () => {
  it("filters by case-insensitive name substring", () => {
    const flat = flattenCatalog(CATEGORIES);
    expect(searchCatalog(flat, "so").map((i) => i.id)).toEqual(["soap"]);
  });

  it("returns everything for an empty query", () => {
    const flat = flattenCatalog(CATEGORIES);
    expect(searchCatalog(flat, "")).toHaveLength(3);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `yarn workspace @zagreus/web test src/data/catalog.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `catalog.ts`**

Create `apps/web/src/data/catalog.ts`:
```ts
/** A single catalog item, flattened out of its category grouping. */
export type CatalogItem = {
  id: string;
  name: string;
  category: string;
  defaultUnit: { id: string; name: string };
};

type RawCategory = {
  category: string;
  items: { id: string; name: string; defaultUnit: { id: string; name: string } }[];
};

/** Flatten the grouped `GET /api/categories` response into a searchable item list. */
export function flattenCatalog(categories: RawCategory[]): CatalogItem[] {
  return categories.flatMap((c) => c.items.map((i) => ({ ...i, category: c.category })));
}

/** Filter catalog items by a case-insensitive substring of the item name. */
export function searchCatalog(items: CatalogItem[], query: string): CatalogItem[] {
  const q = query.trim().toLowerCase();
  if (!q) return items;
  return items.filter((i) => i.name.toLowerCase().includes(q));
}
```

- [ ] **Step 4: Run to verify pass**

Run: `yarn workspace @zagreus/web test src/data/catalog.test.ts` — all pass.

- [ ] **Step 5: Write the failing needs test**

Create `apps/web/src/data/needs.test.ts`:
```ts
import { toNeedItems, groupByCategory, selectNeedsMode } from "./needs";

const API_ITEMS = [
  { id: "i1", itemTypeId: "soap", quantity: 3, unit: { name: "item" }, itemType: { name: "Soap", category: "Hygiene" } },
  { id: "i2", itemTypeId: "rice", quantity: 5, unit: { name: "kg" }, itemType: { name: "Rice", category: "Food" } },
];

describe("toNeedItems", () => {
  it("maps API assessment items into flat NeedItems", () => {
    expect(toNeedItems(API_ITEMS)[0]).toEqual({ id: "i1", itemTypeId: "soap", name: "Soap", category: "Hygiene", quantity: 3, unit: "item" });
  });
});

describe("groupByCategory", () => {
  it("groups needs by category, alphabetically", () => {
    const groups = groupByCategory(toNeedItems(API_ITEMS));
    expect(groups.map((g) => g.category)).toEqual(["Food", "Hygiene"]);
  });
});

describe("selectNeedsMode", () => {
  it("picks edit mode with the open draft's id when a draft exists", () => {
    expect(selectNeedsMode([{ id: "d1", status: "Draft" }, { id: "s1", status: "Submitted" }])).toEqual({ mode: "edit", draftId: "d1" });
  });

  it("picks view mode when there is no open draft", () => {
    expect(selectNeedsMode([{ id: "s1", status: "Submitted" }])).toEqual({ mode: "view" });
  });
});
```

- [ ] **Step 6: Run to verify it fails**

Run: `yarn workspace @zagreus/web test src/data/needs.test.ts` — FAIL, module not found.

- [ ] **Step 7: Implement `needs.ts`**

Create `apps/web/src/data/needs.ts`:
```ts
/** A need as rendered in the UI, flattened from an API assessment item. */
export type NeedItem = {
  id: string;
  itemTypeId: string;
  name: string;
  category: string;
  quantity: number;
  unit: string;
};

type ApiItem = {
  id: string;
  itemTypeId: string;
  quantity: number;
  unit: { name: string };
  itemType: { name: string; category: string };
};

/** Map API assessment items into flat {@link NeedItem}s for display/editing. */
export function toNeedItems(items: ApiItem[]): NeedItem[] {
  return items.map((i) => ({
    id: i.id,
    itemTypeId: i.itemTypeId,
    name: i.itemType.name,
    category: i.itemType.category,
    quantity: i.quantity,
    unit: i.unit.name,
  }));
}

/** Group needs by category, categories in alphabetical order. */
export function groupByCategory(items: NeedItem[]): { category: string; items: NeedItem[] }[] {
  const byCategory = new Map<string, NeedItem[]>();
  for (const item of items) {
    const list = byCategory.get(item.category) ?? [];
    list.push(item);
    byCategory.set(item.category, list);
  }
  return [...byCategory.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([category, list]) => ({ category, items: list }));
}

/** Decide whether the needs page shows the editor (an open draft exists) or the read-only view. */
export function selectNeedsMode(
  assessments: { id: string; status: string }[],
): { mode: "edit"; draftId: string } | { mode: "view" } {
  const draft = assessments.find((a) => a.status === "Draft");
  return draft ? { mode: "edit", draftId: draft.id } : { mode: "view" };
}
```

- [ ] **Step 8: Run to verify pass, then commit**

Run: `yarn workspace @zagreus/web test src/data/needs.test.ts src/data/catalog.test.ts` — all pass.
```bash
git add apps/web/src/data/catalog.ts apps/web/src/data/catalog.test.ts apps/web/src/data/needs.ts apps/web/src/data/needs.test.ts
git commit -m "feat: add catalog and needs pure helpers"
```

---

# Phase C — Frontend: components, actions, page

## Task C1: `CurrentNeeds` read-only view

**Files:**
- Create: `apps/web/src/components/needs/CurrentNeeds.tsx`, `apps/web/src/components/needs/CurrentNeeds.test.tsx`

**Interfaces:**
- Consumes: `NeedItem`, `groupByCategory` (Task B2); `Button` (`@/components/ui/Button`).
- Produces: `CurrentNeeds({ items, lastConfirmedAt, onReview })` — read-only grouped list + "Last confirmed …" (or empty state) + a "Review & update needs" button that calls `onReview` (a server action passed by the page).

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/needs/CurrentNeeds.test.tsx`:
```tsx
import { render, screen } from "@testing-library/react";
import { CurrentNeeds } from "./CurrentNeeds";

const noop = async () => {};

describe("CurrentNeeds", () => {
  it("groups items by category and shows the last-confirmed date", () => {
    render(<CurrentNeeds lastConfirmedAt="2026-07-01T00:00:00Z" onReview={noop} items={[
      { id: "i1", itemTypeId: "soap", name: "Soap", category: "Hygiene", quantity: 3, unit: "item" },
    ]} />);
    expect(screen.getByText("Hygiene")).toBeInTheDocument();
    expect(screen.getByText("Soap")).toBeInTheDocument();
    expect(screen.getByText(/Last confirmed/)).toBeInTheDocument();
  });

  it("shows an empty state when there are no needs yet", () => {
    render(<CurrentNeeds lastConfirmedAt={null} onReview={noop} items={[]} />);
    expect(screen.getByText(/no needs recorded yet/i)).toBeInTheDocument();
  });

  it("renders the review CTA", () => {
    render(<CurrentNeeds lastConfirmedAt={null} onReview={noop} items={[]} />);
    expect(screen.getByRole("button", { name: /review & update needs/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `yarn workspace @zagreus/web test src/components/needs/CurrentNeeds.test.tsx` — FAIL, cannot resolve `./CurrentNeeds`.

- [ ] **Step 3: Implement `CurrentNeeds.tsx`**

Create `apps/web/src/components/needs/CurrentNeeds.tsx`:
```tsx
import { groupByCategory, type NeedItem } from "@/data/needs";
import { Button } from "@/components/ui/Button";

/** Read-only view of the last confirmed needs, grouped by category, with a CTA to edit. */
export function CurrentNeeds({
  items,
  lastConfirmedAt,
  onReview,
}: {
  items: NeedItem[];
  lastConfirmedAt: string | null;
  onReview: () => void;
}) {
  const groups = groupByCategory(items);
  return (
    <main className="container py-da-xl">
      <div className="mb-da-lg flex items-center justify-between">
        <h1 className="font-marker text-3xl text-da-blue">Your needs</h1>
        <form action={onReview}>
          <Button>Review &amp; update needs</Button>
        </form>
      </div>
      <p className="mb-da-lg text-sm">
        {lastConfirmedAt ? `Last confirmed ${new Date(lastConfirmedAt).toLocaleDateString()}` : "Not yet confirmed"}
      </p>
      {items.length === 0 ? (
        <p>No needs recorded yet.</p>
      ) : (
        groups.map((g) => (
          <section key={g.category} className="mb-da-md">
            <h2 className="text-xl font-medium text-da-blue">{g.category}</h2>
            <ul>
              {g.items.map((i) => (
                <li key={i.id} className="flex justify-between border-b border-da-teal py-da-sm">
                  <span>{i.name}</span>
                  <span>{i.quantity} {i.unit}</span>
                </li>
              ))}
            </ul>
          </section>
        ))
      )}
    </main>
  );
}
```

- [ ] **Step 4: Run to verify pass, then commit**

Run: `yarn workspace @zagreus/web test src/components/needs/CurrentNeeds.test.tsx` — all pass.
```bash
git add apps/web/src/components/needs/CurrentNeeds.tsx apps/web/src/components/needs/CurrentNeeds.test.tsx
git commit -m "feat: add CurrentNeeds read-only view"
```

## Task C2: `NeedRow` + `ConfirmButton`

**Files:**
- Create: `apps/web/src/components/needs/NeedRow.tsx`, `apps/web/src/components/needs/NeedRow.test.tsx`
- Create: `apps/web/src/components/needs/ConfirmButton.tsx`, `apps/web/src/components/needs/ConfirmButton.test.tsx`

**Interfaces:**
- Produces:
  - `NeedRow({ need, onQuantityChange, onRemove })` (client) — item name + unit, a number input that calls `onQuantityChange(newQty)` on change, and a remove button that calls `onRemove()`. `need: NeedItem`.
  - `ConfirmButton({ disabled, onConfirm })` (client) — "Confirm current needs"; disabled when `disabled` is true.

- [ ] **Step 1: Write the failing tests**

Create `apps/web/src/components/needs/NeedRow.test.tsx`:
```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NeedRow } from "./NeedRow";

const NEED = { id: "i1", itemTypeId: "soap", name: "Soap", category: "Hygiene", quantity: 3, unit: "item" };

describe("NeedRow", () => {
  it("shows the item name and unit", () => {
    render(<NeedRow need={NEED} onQuantityChange={() => {}} onRemove={() => {}} />);
    expect(screen.getByText("Soap")).toBeInTheDocument();
    expect(screen.getByText("item")).toBeInTheDocument();
  });

  it("reports a new quantity when the input changes", async () => {
    const onQuantityChange = vi.fn();
    render(<NeedRow need={NEED} onQuantityChange={onQuantityChange} onRemove={() => {}} />);
    const input = screen.getByLabelText(/quantity/i);
    await userEvent.clear(input);
    await userEvent.type(input, "8");
    expect(onQuantityChange).toHaveBeenCalledWith(8);
  });

  it("reports removal", async () => {
    const onRemove = vi.fn();
    render(<NeedRow need={NEED} onQuantityChange={() => {}} onRemove={onRemove} />);
    await userEvent.click(screen.getByRole("button", { name: /remove/i }));
    expect(onRemove).toHaveBeenCalled();
  });
});
```

Create `apps/web/src/components/needs/ConfirmButton.test.tsx`:
```tsx
import { render, screen } from "@testing-library/react";
import { ConfirmButton } from "./ConfirmButton";

describe("ConfirmButton", () => {
  it("is enabled when there are needs", () => {
    render(<ConfirmButton disabled={false} onConfirm={() => {}} />);
    expect(screen.getByRole("button", { name: /confirm current needs/i })).toBeEnabled();
  });

  it("is disabled when the list is empty", () => {
    render(<ConfirmButton disabled={true} onConfirm={() => {}} />);
    expect(screen.getByRole("button", { name: /confirm current needs/i })).toBeDisabled();
  });
});
```

- [ ] **Step 2: Run to verify they fail**

Run: `yarn workspace @zagreus/web test src/components/needs/NeedRow.test.tsx src/components/needs/ConfirmButton.test.tsx`
Expected: FAIL — modules not found. (`@testing-library/user-event` is already a dev dependency from the auth slice.)

- [ ] **Step 3: Implement the components**

Create `apps/web/src/components/needs/NeedRow.tsx`:
```tsx
"use client";

import type { NeedItem } from "@/data/needs";

/** One editable need: name + unit, a quantity input (fires onQuantityChange), and a remove button. */
export function NeedRow({
  need,
  onQuantityChange,
  onRemove,
}: {
  need: NeedItem;
  onQuantityChange: (quantity: number) => void;
  onRemove: () => void;
}) {
  return (
    <li className="flex items-center gap-da-md border-b border-da-teal py-da-sm">
      <span className="flex-1">{need.name}</span>
      <label className="flex items-center gap-da-sm">
        <span className="sr-only">Quantity for {need.name}</span>
        <input
          type="number"
          min={0}
          aria-label={`Quantity for ${need.name}`}
          defaultValue={need.quantity}
          onChange={(e) => {
            const value = Number(e.target.value);
            if (!Number.isNaN(value)) onQuantityChange(value);
          }}
          className="w-20 rounded border border-da-teal px-da-sm py-da-sm"
        />
        <span>{need.unit}</span>
      </label>
      <button type="button" onClick={onRemove} className="text-da-blue underline">
        Remove
      </button>
    </li>
  );
}
```

Create `apps/web/src/components/needs/ConfirmButton.tsx`:
```tsx
"use client";

import { Button } from "@/components/ui/Button";

/** Confirms the current needs (submits the draft). Disabled when the list is empty. */
export function ConfirmButton({ disabled, onConfirm }: { disabled: boolean; onConfirm: () => void }) {
  return (
    <Button onClick={onConfirm} disabled={disabled}>
      Confirm current needs
    </Button>
  );
}
```

- [ ] **Step 4: Run to verify pass, then commit**

Run: `yarn workspace @zagreus/web test src/components/needs/NeedRow.test.tsx src/components/needs/ConfirmButton.test.tsx` — all pass.
```bash
git add apps/web/src/components/needs/NeedRow.tsx apps/web/src/components/needs/NeedRow.test.tsx apps/web/src/components/needs/ConfirmButton.tsx apps/web/src/components/needs/ConfirmButton.test.tsx
git commit -m "feat: add NeedRow and ConfirmButton components"
```

## Task C3: `AddNeed` typeahead

**Files:**
- Create: `apps/web/src/components/needs/AddNeed.tsx`, `apps/web/src/components/needs/AddNeed.test.tsx`

**Interfaces:**
- Consumes: `CatalogItem`, `searchCatalog` (Task B2).
- Produces: `AddNeed({ catalog, onAdd })` (client) — a search box; typing filters `catalog` by name; clicking a result calls `onAdd(item)` (`item: CatalogItem`) and clears the search.

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/needs/AddNeed.test.tsx`:
```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AddNeed } from "./AddNeed";

const CATALOG = [
  { id: "soap", name: "Soap", category: "Hygiene", defaultUnit: { id: "u1", name: "item" } },
  { id: "rice", name: "Rice", category: "Food", defaultUnit: { id: "u2", name: "kg" } },
];

describe("AddNeed", () => {
  it("filters the catalog as the user types and adds the chosen item", async () => {
    const onAdd = vi.fn();
    render(<AddNeed catalog={CATALOG} onAdd={onAdd} />);

    await userEvent.type(screen.getByRole("searchbox", { name: /add a need/i }), "ric");
    expect(screen.queryByRole("button", { name: "Soap" })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Rice" }));

    expect(onAdd).toHaveBeenCalledWith(CATALOG[1]);
  });

  it("shows no results list until the user types", () => {
    render(<AddNeed catalog={CATALOG} onAdd={() => {}} />);
    expect(screen.queryByRole("button", { name: "Rice" })).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `yarn workspace @zagreus/web test src/components/needs/AddNeed.test.tsx` — FAIL, module not found.

- [ ] **Step 3: Implement `AddNeed.tsx`**

Create `apps/web/src/components/needs/AddNeed.tsx`:
```tsx
"use client";

import { useState } from "react";
import { searchCatalog, type CatalogItem } from "@/data/catalog";

/** Search-only catalog picker: type to filter by name, click a result to add it. */
export function AddNeed({ catalog, onAdd }: { catalog: CatalogItem[]; onAdd: (item: CatalogItem) => void }) {
  const [query, setQuery] = useState("");
  const results = query.trim() ? searchCatalog(catalog, query).slice(0, 10) : [];

  return (
    <div className="mb-da-lg">
      <label className="flex flex-col gap-da-sm">
        <span className="font-medium text-da-blue">Add a need</span>
        <input
          type="search"
          aria-label="Add a need"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search the catalog…"
          className="rounded border border-da-teal px-da-md py-da-sm"
        />
      </label>
      {results.length > 0 && (
        <ul className="mt-da-sm rounded border border-da-teal">
          {results.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => {
                  onAdd(item);
                  setQuery("");
                }}
                className="flex w-full justify-between px-da-md py-da-sm text-left hover:bg-da-lavender"
              >
                <span>{item.name}</span>
                <span className="text-da-blue">{item.category}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run to verify pass, then commit**

Run: `yarn workspace @zagreus/web test src/components/needs/AddNeed.test.tsx` — all pass.
```bash
git add apps/web/src/components/needs/AddNeed.tsx apps/web/src/components/needs/AddNeed.test.tsx
git commit -m "feat: add AddNeed search-only catalog picker"
```

## Task C4: Needs server actions

**Files:**
- Create: `apps/web/src/data/needsActions.ts`

**Interfaces:**
- Consumes: `apiPost`/`apiPatch`/`apiDelete` (Task B1); `revalidatePath` from `next/cache`.
- Produces server actions (all `"use server"`): `enterEdit(projectId)`, `addNeed(draftId, itemTypeId, unitId)`, `updateQuantity(draftId, itemId, quantity)`, `removeNeed(draftId, itemId)`, `confirmNeeds(draftId)`. Each mutates via the API then `revalidatePath("/needs")`. These are thin wrappers over the (already unit-tested) API-client helpers; correctness of the HTTP layer is covered by Task B1, and the components that call them are covered by their own tests.

- [ ] **Step 1: Create the server actions**

Create `apps/web/src/data/needsActions.ts`:
```ts
"use server";

import { revalidatePath } from "next/cache";
import { apiPost, apiPatch, apiDelete } from "./apiClient";

/** Get-or-create the project's working draft, then re-render `/needs` in edit mode. */
export async function enterEdit(projectId: string): Promise<void> {
  await apiPost(`/api/projects/${projectId}/assessments/working-draft`);
  revalidatePath("/needs");
}

/** Add a catalog item to the draft at its default unit, quantity 1. */
export async function addNeed(draftId: string, itemTypeId: string, unitId: string): Promise<void> {
  await apiPost(`/api/assessments/${draftId}/items`, { itemTypeId, unitId, quantity: 1 });
  revalidatePath("/needs");
}

/** Update a need's quantity (auto-save). */
export async function updateQuantity(draftId: string, itemId: string, quantity: number): Promise<void> {
  await apiPatch(`/api/assessments/${draftId}/items/${itemId}`, { quantity });
  revalidatePath("/needs");
}

/** Remove a need from the draft (auto-save). */
export async function removeNeed(draftId: string, itemId: string): Promise<void> {
  await apiDelete(`/api/assessments/${draftId}/items/${itemId}`);
  revalidatePath("/needs");
}

/** Confirm the current needs — submit the draft; freshness resets on the backend. */
export async function confirmNeeds(draftId: string): Promise<void> {
  await apiPost(`/api/assessments/${draftId}/submit`);
  revalidatePath("/needs");
}
```

- [ ] **Step 2: Typecheck and commit**

Run: `yarn workspace @zagreus/web typecheck`
Expected: exit 0.
```bash
git add apps/web/src/data/needsActions.ts
git commit -m "feat: add needs server actions (enter-edit, add, update, remove, confirm)"
```

## Task C5: `NeedsEditor`, `/needs` page, error boundary, route guard

**Files:**
- Create: `apps/web/src/components/needs/NeedsEditor.tsx`
- Create: `apps/web/src/app/needs/page.tsx`, `apps/web/src/app/needs/error.tsx`
- Modify: `apps/web/src/proxy.ts` (matcher)
- Modify: `apps/web/src/data/dashboard.ts` (export `getCurrentProject` used by the page)

**Interfaces:**
- Consumes: all Task B/C pieces; `apiGet` (server); server actions (Task C4).
- Produces: the `/needs` route, guarded, choosing View vs Edit via `selectNeedsMode`.

- [ ] **Step 1: Add `getCurrentProject` to the data layer**

In `apps/web/src/data/dashboard.ts`, add (and export) a helper the page reuses:
```ts
/** Resolve the caller's single project (org's first project), or null if none. */
export async function getCurrentProject(): Promise<{ id: string; name: string; region: string | null; orgName: string | null } | null> {
  const me = await apiGet<{ orgId: string | null; orgName: string | null }>("/api/me");
  if (!me?.orgId) return null;
  const projects = await apiGet<{ id: string; name: string; region: string | null }[]>(`/api/organisations/${me.orgId}/projects`);
  const project = projects?.[0];
  return project ? { ...project, orgName: me.orgName } : null;
}
```

- [ ] **Step 2: Write the failing `NeedsEditor` test**

Create `apps/web/src/components/needs/NeedsEditor.test.tsx`:
```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NeedsEditor } from "./NeedsEditor";

const actions = vi.hoisted(() => ({
  addNeed: vi.fn(),
  updateQuantity: vi.fn(),
  removeNeed: vi.fn(),
  confirmNeeds: vi.fn(),
}));
vi.mock("@/data/needsActions", () => actions);

const CATALOG = [{ id: "soap", name: "Soap", category: "Hygiene", defaultUnit: { id: "u1", name: "item" } }];
const ITEMS = [{ id: "i1", itemTypeId: "soap", name: "Soap", category: "Hygiene", quantity: 3, unit: "item" }];

describe("NeedsEditor", () => {
  beforeEach(() => {
    for (const fn of Object.values(actions)) fn.mockReset();
  });

  it("renders needs grouped by category", () => {
    render(<NeedsEditor draftId="d1" items={ITEMS} catalog={CATALOG} />);
    expect(screen.getByText("Hygiene")).toBeInTheDocument();
    expect(screen.getByText("Soap")).toBeInTheDocument();
  });

  it("removing a row calls removeNeed with the draft and item ids", async () => {
    render(<NeedsEditor draftId="d1" items={ITEMS} catalog={CATALOG} />);
    await userEvent.click(screen.getByRole("button", { name: /remove/i }));
    expect(actions.removeNeed).toHaveBeenCalledWith("d1", "i1");
  });

  it("disables confirm when there are no needs", () => {
    render(<NeedsEditor draftId="d1" items={[]} catalog={CATALOG} />);
    expect(screen.getByRole("button", { name: /confirm current needs/i })).toBeDisabled();
  });
});
```

- [ ] **Step 3: Run to verify it fails**

Run: `yarn workspace @zagreus/web test src/components/needs/NeedsEditor.test.tsx`
Expected: FAIL — cannot resolve `./NeedsEditor`.

- [ ] **Step 4: Implement `NeedsEditor` (client)**

Create `apps/web/src/components/needs/NeedsEditor.tsx`:
```tsx
"use client";

import { groupByCategory, type NeedItem } from "@/data/needs";
import type { CatalogItem } from "@/data/catalog";
import { AddNeed } from "./AddNeed";
import { NeedRow } from "./NeedRow";
import { ConfirmButton } from "./ConfirmButton";
import { addNeed, updateQuantity, removeNeed, confirmNeeds } from "@/data/needsActions";

/** The editable working draft: add/adjust/remove needs (auto-saved) and confirm. */
export function NeedsEditor({
  draftId,
  items,
  catalog,
}: {
  draftId: string;
  items: NeedItem[];
  catalog: CatalogItem[];
}) {
  const groups = groupByCategory(items);
  return (
    <main className="container py-da-xl">
      <h1 className="mb-da-lg font-marker text-3xl text-da-blue">Update your needs</h1>
      <AddNeed catalog={catalog} onAdd={(item) => addNeed(draftId, item.id, item.defaultUnit.id)} />
      {groups.map((g) => (
        <section key={g.category} className="mb-da-md">
          <h2 className="text-xl font-medium text-da-blue">{g.category}</h2>
          <ul>
            {g.items.map((need) => (
              <NeedRow
                key={need.id}
                need={need}
                onQuantityChange={(q) => updateQuantity(draftId, need.id, q)}
                onRemove={() => removeNeed(draftId, need.id)}
              />
            ))}
          </ul>
        </section>
      ))}
      <div className="mt-da-lg">
        <ConfirmButton disabled={items.length === 0} onConfirm={() => confirmNeeds(draftId)} />
      </div>
    </main>
  );
}
```

- [ ] **Step 5: Run to verify the `NeedsEditor` test passes**

Run: `yarn workspace @zagreus/web test src/components/needs/NeedsEditor.test.tsx`
Expected: all 3 pass.

- [ ] **Step 6: Implement the page (server)**

Create `apps/web/src/app/needs/page.tsx`:
```tsx
import { redirect } from "next/navigation";
import { apiGet } from "@/data/apiClient";
import { getCurrentProject } from "@/data/dashboard";
import { flattenCatalog } from "@/data/catalog";
import { toNeedItems, selectNeedsMode } from "@/data/needs";
import { enterEdit } from "@/data/needsActions";
import { CurrentNeeds } from "@/components/needs/CurrentNeeds";
import { NeedsEditor } from "@/components/needs/NeedsEditor";

type Assessment = {
  id: string;
  status: string;
  submittedAt: string | null;
  items: { id: string; itemTypeId: string; quantity: number; unit: { name: string }; itemType: { name: string; category: string } }[];
};

/** Needs page: read-only View by default, Edit when an open draft exists, Confirm returns to View. */
export default async function NeedsPage() {
  const project = await getCurrentProject();
  if (!project) {
    return <main className="container py-da-xl"><p>No project has been set up for your organization yet.</p></main>;
  }

  const rawCategories = await apiGet<Parameters<typeof flattenCatalog>[0]>("/api/categories");
  const catalog = flattenCatalog(rawCategories ?? []);

  const assessments = await apiGet<{ id: string; status: string }[]>(`/api/projects/${project.id}/assessments`);
  const mode = selectNeedsMode(assessments ?? []);

  if (mode.mode === "edit") {
    const draft = await apiGet<Assessment>(`/api/assessments/${mode.draftId}`);
    return <NeedsEditor draftId={mode.draftId} items={toNeedItems(draft!.items)} catalog={catalog} />;
  }

  // View: read-only current needs (with items), or an empty state for a new hub.
  const current = await apiGet<{ id: string; submittedAt: string | null } | null>(`/api/projects/${project.id}/assessments/current`);
  const items = current ? toNeedItems((await apiGet<Assessment>(`/api/assessments/${current.id}`))!.items) : [];

  async function onReview() {
    "use server";
    await enterEdit(project!.id);
  }

  return <CurrentNeeds items={items} lastConfirmedAt={current?.submittedAt ?? null} onReview={onReview} />;
}
```

Create `apps/web/src/app/needs/error.tsx`:
```tsx
"use client";

import { Button } from "@/components/ui/Button";

/** Error boundary for the needs route — offers a retry / re-sign-in on an unexpected failure. */
export default function NeedsError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <main className="container flex min-h-screen flex-col items-center justify-center gap-da-lg text-center">
      <h1 className="font-marker text-3xl text-da-blue">Something went wrong loading your needs</h1>
      <p>Your session may have expired. Try again, or sign in again.</p>
      <div className="flex gap-da-md">
        <Button onClick={reset} variant="secondary">Try again</Button>
        <a href="/login"><Button>Sign in</Button></a>
      </div>
    </main>
  );
}
```

- [ ] **Step 7: Guard the route**

In `apps/web/src/proxy.ts`, extend the matcher:
```ts
export const config = { matcher: ["/dashboard/:path*", "/needs/:path*"] };
```

- [ ] **Step 8: Verify build + guard**

```bash
yarn workspace @zagreus/web typecheck
yarn workspace @zagreus/web build
```
Expected: typecheck exit 0; build succeeds and lists `/needs` as a route.

Then confirm the guard runs (the route requires a session). With the dev server running (`yarn workspace @zagreus/web dev`):
```bash
curl -s -o /dev/null -D - http://localhost:3000/needs | grep -iE '^HTTP|^location:'
```
Expected: `307` redirect to `/login?callbackUrl=…` (the proxy added the param — proves `/needs` is guarded).

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/needs/NeedsEditor.tsx apps/web/src/components/needs/NeedsEditor.test.tsx apps/web/src/app/needs apps/web/src/proxy.ts apps/web/src/data/dashboard.ts
git commit -m "feat: add /needs page with View/Edit/Confirm flow and route guard"
```

## Task C6: Wire the dashboard CTA + end-to-end verification

**Files:**
- Modify: `apps/web/src/components/ProjectCard.tsx` (link the CTA to `/needs`)

**Interfaces:**
- Consumes: the `/needs` route (Task C5).
- Produces: the dashboard's "Review & confirm needs" button navigates to `/needs` (landing in View).

- [ ] **Step 1: Link the CTA**

In `apps/web/src/components/ProjectCard.tsx`, wrap the existing button in a link. Change the CTA block:
```tsx
      <div className="mt-da-md">
        <a href="/needs">
          <Button>Review &amp; confirm needs</Button>
        </a>
      </div>
```

- [ ] **Step 2: Run all frontend tests + typecheck + build (verification gate)**

```bash
yarn workspace @zagreus/web test
yarn workspace @zagreus/web typecheck
yarn workspace @zagreus/web build
```
Expected: all tests pass; typecheck exit 0; build succeeds.

- [ ] **Step 3: Manual end-to-end**

With Postgres running, the API running (`cd apps/api/DA.NA.Api && dotnet run`), and the web dev server running, signed in as a provisioned user:
1. From the dashboard, click **Review & confirm needs** → lands on `/needs` in **View** (empty state for a fresh hub).
2. Click **Review & update needs** → **Edit**: search the catalog, add an item (appears under its category at its default unit), change a quantity, remove an item — each persists (refresh keeps them).
3. Click **Confirm current needs** → returns to **View** showing the confirmed list and "Last confirmed …".
4. Return to the dashboard → freshness badge reads **Up to date**.
Expected: the full loop works; a second visit to `/needs` resumes an open draft if you didn't confirm.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ProjectCard.tsx
git commit -m "feat: link dashboard CTA to the needs page"
```

---

## Self-Review

**Spec coverage:**
- Working-draft endpoint (get-or-create, seeded, org-scoped, one-open-draft) → Task A1 (matches ADR-002).
- Server-side mutation client (`apiPost/apiPatch/apiDelete`) → Task B1.
- Catalog flatten+search, needs mapping/grouping, View-vs-Edit selection → Task B2.
- Components with behavior/branching all tested: `CurrentNeeds` (C1), `NeedRow` + `ConfirmButton` (C2), `AddNeed` (C3), `NeedsEditor` (C5) → matches the spec's "every component + branch" testing principle. Server component (`page.tsx`) and server actions (`needsActions.ts`) covered via extracted pure logic (B2) + the API-client tests (B1), per the spec.
- View → Edit → Confirm flow, dashboard CTA lands in View → Tasks C5, C6.
- Auto-save (each edit calls its action) → C4/C5; empty-list confirm blocked (client `disabled` + API `submit` rule) → C2/A1.
- Route guard for `/needs` → C5 (matcher) with a curl verification.

**Deferred (out of slice, per spec §Scope):** browse-by-category ([#8](https://github.com/distributeaid/super-zagreus/issues/8)), CSV/reporting totals, missing-item request, multi-project UI.

**Placeholder scan:** none — every code step has full contents; every command states expected output.

**Type/name consistency:** `CatalogItem`/`flattenCatalog`/`searchCatalog` (B2) are used by `AddNeed` (C3) and the page (C5). `NeedItem`/`toNeedItems`/`groupByCategory`/`selectNeedsMode` (B2) are used by `CurrentNeeds`, `NeedsEditor`, and the page. Server actions `enterEdit`/`addNeed`/`updateQuantity`/`removeNeed`/`confirmNeeds` (C4) are called by the page and `NeedsEditor` with matching signatures. The backend DTO shape (A1) — `items[].itemType.{name,category}`, `items[].unit.name`, `items[].quantity`, `items[].id`, `items[].itemTypeId` — matches the `ApiItem` type consumed by `toNeedItems` (B2) and the page's `Assessment` type (C5).
