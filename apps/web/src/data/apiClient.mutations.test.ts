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

  it("throws when the API returns a non-ok, non-401 response", async () => {
    authMock.mockResolvedValue({ apiToken: "app-jwt" });
    vi.stubGlobal("fetch", fetchReturning({ ok: false, status: 500 }));

    await expect(apiPost("/api/x", {})).rejects.toThrow("API POST /api/x failed: 500");
    vi.unstubAllGlobals();
  });

  it("apiDelete sends no content-type header when there is no body", async () => {
    authMock.mockResolvedValue({ apiToken: "app-jwt" });
    const fetchImpl = fetchReturning({ ok: true, status: 204 });
    vi.stubGlobal("fetch", fetchImpl);

    await apiDelete("/api/x/1");

    expect(fetchImpl).toHaveBeenCalledWith(
      "http://api.test/api/x/1",
      expect.objectContaining({ method: "DELETE", headers: { authorization: "Bearer app-jwt" } }),
    );
    vi.unstubAllGlobals();
  });
});
