// apiClient pulls the session + Next redirect; mock those out so we can exercise
// apiGet's branch behavior in isolation. (`server-only` is stubbed via vitest.config.)
const redirectMock = vi.fn((url: string) => {
  throw new Error(`REDIRECT:${url}`);
});
vi.mock("next/navigation", () => ({ redirect: (url: string) => redirectMock(url) }));

const authMock = vi.fn();
vi.mock("@/auth", () => ({ auth: () => authMock() }));

// apiClient reads API_BASE_URL at module load; set it before the (hoisted) import.
vi.hoisted(() => {
  process.env.API_BASE_URL = "http://api.test";
});

import { apiGet } from "./apiClient";

function fetchReturning(res: Partial<Response> & { status: number; ok: boolean }): typeof fetch {
  return vi.fn(async () => res as Response) as unknown as typeof fetch;
}

beforeEach(() => {
  redirectMock.mockClear();
  authMock.mockReset();
});

test("redirects to /login when there is no app token", async () => {
  authMock.mockResolvedValue({ apiToken: undefined });

  await expect(apiGet("/api/me")).rejects.toThrow("REDIRECT:/login");
  expect(redirectMock).toHaveBeenCalledWith("/login");
});

test("attaches the bearer token and returns parsed JSON on success", async () => {
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

test("redirects to /login when the API rejects the token (401)", async () => {
  authMock.mockResolvedValue({ apiToken: "stale" });
  vi.stubGlobal("fetch", fetchReturning({ ok: false, status: 401 }));

  await expect(apiGet("/api/me")).rejects.toThrow("REDIRECT:/login");
  expect(redirectMock).toHaveBeenCalledWith("/login");
  vi.unstubAllGlobals();
});

test("resolves to null on a 404", async () => {
  authMock.mockResolvedValue({ apiToken: "app-jwt" });
  vi.stubGlobal("fetch", fetchReturning({ ok: false, status: 404 }));

  await expect(apiGet("/api/projects/x/assessments/current")).resolves.toBeNull();
  vi.unstubAllGlobals();
});

test("throws on other non-2xx responses", async () => {
  authMock.mockResolvedValue({ apiToken: "app-jwt" });
  vi.stubGlobal("fetch", fetchReturning({ ok: false, status: 500 }));

  await expect(apiGet("/api/me")).rejects.toThrow("API /api/me failed: 500");
  vi.unstubAllGlobals();
});
