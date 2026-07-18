/**
 * Result of exchanging a provider ID token for the app's own session token.
 * Either the app JWT (with its expiry), or a flag that the backend rejected
 * the sign-in (unverifiable token, or an email that isn't provisioned).
 */
export type ProviderTokenExchange =
  | { apiToken: string; apiExpiresAt: string }
  | { apiError: true };

/**
 * Exchange a verified Google/Microsoft ID token for the app's session JWT by
 * calling `POST {apiBase}/api/auth/session`.
 *
 * @param idToken   The OIDC ID token issued by the provider during sign-in.
 * @param provider  Which provider issued the token (`"google"` or `"microsoft"`).
 * @param apiBase   Base URL of the backend API (e.g. `http://localhost:54764`).
 * @param fetchImpl Injectable `fetch` (defaults to global `fetch`; overridden in tests).
 * @returns `{ apiToken, apiExpiresAt }` on success, or `{ apiError: true }` when the
 *          backend responds non-2xx (invalid token or unauthorized email).
 */
export async function exchangeProviderToken(
  idToken: string,
  provider: "google" | "microsoft",
  apiBase: string,
  fetchImpl: typeof fetch = fetch,
): Promise<ProviderTokenExchange> {
  const res = await fetchImpl(`${apiBase}/api/auth/session`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ idToken, provider }),
  });
  if (!res.ok) return { apiError: true };
  const data = (await res.json()) as { token: string; expiresAt: string };
  return { apiToken: data.token, apiExpiresAt: data.expiresAt };
}
