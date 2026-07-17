import "server-only";
import { auth } from "@/auth";

const API_BASE = process.env.API_BASE_URL!;

export async function apiGet<T>(path: string): Promise<T> {
  const session = await auth();
  const token = session?.apiToken;
  if (!token) throw new Error("Not authenticated");
  const res = await fetch(`${API_BASE}${path}`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (res.status === 404) return null as T;
  if (!res.ok) throw new Error(`API ${path} failed: ${res.status}`);
  return (await res.json()) as T;
}
