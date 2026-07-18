import "server-only";
import { redirect } from "next/navigation";
import { auth } from "@/auth";

const API_BASE = process.env.API_BASE_URL!;

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
