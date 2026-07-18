import "server-only";
import { redirect } from "next/navigation";
import { auth } from "@/auth";

const API_BASE = process.env.API_BASE_URL!;

/**
 * Server-only GET helper for the backend API. Reads the app JWT from the Auth.js
 * session and attaches it as a Bearer token.
 *
 * Auth handling (the `/dashboard` proxy guard normally prevents these, but this is
 * the defensive fallback): a missing token or a `401` response redirects to `/login`
 * to re-authenticate. A `404` resolves to `null` (treated as "no data yet").
 *
 * @typeParam T The expected JSON response shape.
 * @param path API path beginning with `/` (e.g. `/api/me`).
 * @returns The parsed JSON as `T`, or `null` for a `404`.
 * @throws Error on any non-2xx response other than `401`/`404`.
 */
export async function apiGet<T>(path: string): Promise<T> {
  const session = await auth();
  const token = session?.apiToken;
  // The middleware already guards these routes; if a request still arrives without
  // a token, send the caller back to sign in rather than throwing a raw error.
  if (!token) redirect("/login");
  const res = await fetch(`${API_BASE}${path}`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  // Token rejected (expired or revoked since the middleware check) → re-authenticate.
  if (res.status === 401) redirect("/login");
  if (res.status === 404) return null as T;
  if (!res.ok) throw new Error(`API ${path} failed: ${res.status}`);
  return (await res.json()) as T;
}
