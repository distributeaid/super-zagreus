// Pure authorization decision for a request against the caller's session.
// Kept free of server-only / next imports so it runs in the edge middleware
// (via auth.ts's `authorized` callback) and is unit-testable in isolation.

export type AccessDecision =
  | "allow" // has a valid, unexpired app token
  | "denied" // signed in, but the backend rejected the account (not provisioned)
  | "reauth"; // not signed in, or the app token is missing/expired

export type SessionAccessInput =
  | {
      apiToken?: string;
      apiError?: boolean;
      apiExpiresAt?: string;
    }
  | null
  | undefined;

/**
 * Decide what a request with the given session may do, used by the route guard.
 *
 * @param session The Auth.js session (or `null`/`undefined` when not signed in).
 * @param now Reference time for the expiry check (injectable for tests).
 * @returns `"denied"` if the backend rejected the account (`apiError`), `"allow"` for a
 *          valid unexpired token, otherwise `"reauth"` (not signed in, or token missing/expired).
 */
export function sessionAccess(
  session: SessionAccessInput,
  now: Date = new Date(),
): AccessDecision {
  if (session?.apiError) return "denied";
  if (session?.apiToken && !isExpired(session.apiExpiresAt, now)) return "allow";
  return "reauth";
}

function isExpired(expiresAt: string | undefined, now: Date): boolean {
  if (!expiresAt) return false; // no expiry info → don't infer expiry here
  return new Date(expiresAt).getTime() <= now.getTime();
}
