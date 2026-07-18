import { apiGet } from "./apiClient";

// apiClient pulls the session + Next redirect, and reads API_BASE_URL at module load.
// vi.hoisted runs before the (hoisted) imports, so the mocks and env are ready in time.
// `server-only` is stubbed via vitest.config.
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

describe("apiGet", () => {
  beforeEach(() => {
    redirectMock.mockClear();
    authMock.mockReset();
  });

  it("redirects to /login when there is no app token", async () => {
    authMock.mockResolvedValue({ apiToken: undefined });

    await expect(apiGet("/api/me")).rejects.toThrow("REDIRECT:/login");
    expect(redirectMock).toHaveBeenCalledWith("/login");
  });

  it("attaches the bearer token and returns parsed JSON on success", async () => {
    authMock.mockResolvedValue({ apiToken: "app-jwt" });
    const fetchImpl = fetchReturning({ ok: true, status: 200, json: async () => ({ email: "hub@example.org" }) });
    vi.stubGlobal("fetch", fetchImpl);

    const body = await apiGet<{ email: string }>("/api/me");

    expect(body).toEqual({ email: "hub@example.org" });
    expect(fetchImpl).toHaveBeenCalledWith(
      "http://api.test/api/me",
      expect.objectContaining({ headers: { authorization: "Bearer app-jwt" }, cache: "no-store" }),
    );
    vi.unstubAllGlobals();
  });

  it("redirects to /login when the API rejects the token (401)", async () => {
    authMock.mockResolvedValue({ apiToken: "stale" });
    vi.stubGlobal("fetch", fetchReturning({ ok: false, status: 401 }));

    await expect(apiGet("/api/me")).rejects.toThrow("REDIRECT:/login");
    expect(redirectMock).toHaveBeenCalledWith("/login");
    vi.unstubAllGlobals();
  });

  it("resolves to null on a 404", async () => {
    authMock.mockResolvedValue({ apiToken: "app-jwt" });
    vi.stubGlobal("fetch", fetchReturning({ ok: false, status: 404 }));

    await expect(apiGet("/api/projects/x/assessments/current")).resolves.toBeNull();
    vi.unstubAllGlobals();
  });

  it("throws on other non-2xx responses", async () => {
    authMock.mockResolvedValue({ apiToken: "app-jwt" });
    vi.stubGlobal("fetch", fetchReturning({ ok: false, status: 500 }));

    await expect(apiGet("/api/me")).rejects.toThrow("API /api/me failed: 500");
    vi.unstubAllGlobals();
  });
});
