// loadDashboard orchestrates apiGet calls; mock apiGet so we test the
// assembly/fallback logic without the server-only fetch layer.
vi.mock("./apiClient", () => ({ apiGet: vi.fn() }));

import { apiGet } from "./apiClient";
import { loadDashboard } from "./dashboard";

const mockApiGet = apiGet as unknown as ReturnType<typeof vi.fn>;

beforeEach(() => mockApiGet.mockReset());

// loadDashboard calls apiGet in a fixed order: /api/me, then the org's projects,
// then the project's current assessment. Queue the responses in that order.

test("returns nulls when the caller has no org", async () => {
  mockApiGet.mockResolvedValueOnce({ orgId: null, orgName: null });

  expect(await loadDashboard()).toEqual({ orgName: null, project: null, lastConfirmedAt: null });
});

test("returns org name but no project when the org has no projects", async () => {
  mockApiGet
    .mockResolvedValueOnce({ orgId: "org-1", orgName: "Aegean Hub" })
    .mockResolvedValueOnce([]); // projects

  expect(await loadDashboard()).toEqual({ orgName: "Aegean Hub", project: null, lastConfirmedAt: null });
});

test("maps the first project and the current assessment's submittedAt", async () => {
  mockApiGet
    .mockResolvedValueOnce({ orgId: "org-1", orgName: "Aegean Hub" })
    .mockResolvedValueOnce([
      { id: "p1", name: "Main", region: "Greece" },
      { id: "p2", name: "Other", region: null },
    ])
    .mockResolvedValueOnce({ submittedAt: "2026-07-01T00:00:00Z" });

  expect(await loadDashboard()).toEqual({
    orgName: "Aegean Hub",
    project: { id: "p1", name: "Main", region: "Greece" },
    lastConfirmedAt: "2026-07-01T00:00:00Z",
  });
});

test("leaves lastConfirmedAt null when there is no current assessment", async () => {
  mockApiGet
    .mockResolvedValueOnce({ orgId: "org-1", orgName: "Aegean Hub" })
    .mockResolvedValueOnce([{ id: "p1", name: "Main", region: "Greece" }])
    .mockResolvedValueOnce(null); // no current assessment (apiGet returns null on 404)

  expect(await loadDashboard()).toMatchObject({ lastConfirmedAt: null, project: { id: "p1" } });
});
