import { encode } from "next-auth/jwt";
import { inject } from "vitest";
import { API_TOKEN, AUTH_SECRET, PROJECT_NAME, SESSION_COOKIE } from "./fixtures";

const baseUrl = inject("baseUrl");
const HOUR_MS = 60 * 60 * 1000;

/**
 * Request `/dashboard` from the running app without following redirects.
 *
 * @param cookie Optional `Cookie` header value.
 * @returns The raw response, so redirects can be inspected.
 */
function getDashboard(cookie?: string): Promise<Response> {
  return fetch(`${baseUrl}/dashboard`, {
    redirect: "manual",
    headers: cookie ? { cookie } : {},
  });
}

/**
 * Assert the response is a redirect and return its target.
 *
 * @param res The response to check.
 * @returns The absolute redirect target.
 */
function redirectTarget(res: Response): URL {
  expect(res.status).toBeGreaterThanOrEqual(300);
  expect(res.status).toBeLessThan(400);
  const location = res.headers.get("location");
  expect(location).toBeTruthy();
  return new URL(location!, baseUrl);
}

/**
 * Forge an Auth.js session cookie carrying the given app-token claims, signed with the
 * test server's secret, so each guard branch can be exercised without an OAuth provider.
 *
 * @param claims Session claims copied onto the session by the `session` callback.
 * @returns A `Cookie` header value.
 */
async function sessionCookie(claims: { apiToken?: string; apiError?: boolean; apiExpiresAt?: string }): Promise<string> {
  const value = await encode({
    token: { sub: "integration-user", ...claims },
    secret: AUTH_SECRET,
    salt: SESSION_COOKIE,
  });
  return `${SESSION_COOKIE}=${value}`;
}

describe("/dashboard proxy guard", () => {
  it("redirects to /login with a callbackUrl when there is no session", async () => {
    const target = redirectTarget(await getDashboard());

    expect(target.pathname).toBe("/login");
    // Only the proxy guard adds callbackUrl; the apiGet fallback redirects to bare /login.
    const callbackUrl = target.searchParams.get("callbackUrl");
    expect(callbackUrl).toBeTruthy();
    expect(new URL(callbackUrl!).pathname).toBe("/dashboard");
  });

  it("redirects to /access-denied when the backend rejected the account", async () => {
    const target = redirectTarget(await getDashboard(await sessionCookie({ apiError: true })));

    expect(target.pathname).toBe("/access-denied");
  });

  it("redirects to /login with a callbackUrl when the app token has expired", async () => {
    const cookie = await sessionCookie({
      apiToken: API_TOKEN,
      apiExpiresAt: new Date(Date.now() - HOUR_MS).toISOString(),
    });
    const target = redirectTarget(await getDashboard(cookie));

    expect(target.pathname).toBe("/login");
    expect(target.searchParams.get("callbackUrl")).toBeTruthy();
  });

  it("renders the dashboard for a valid, unexpired app token", async () => {
    const cookie = await sessionCookie({
      apiToken: API_TOKEN,
      apiExpiresAt: new Date(Date.now() + HOUR_MS).toISOString(),
    });
    const res = await getDashboard(cookie);

    expect(res.status).toBe(200);
    expect(await res.text()).toContain(PROJECT_NAME);
  });
});
