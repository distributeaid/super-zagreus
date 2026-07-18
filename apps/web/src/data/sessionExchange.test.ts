import { exchangeProviderToken } from "./sessionExchange";

function fetchReturning(res: Partial<Response> & { ok: boolean }): typeof fetch {
  return vi.fn(async () => res as Response) as unknown as typeof fetch;
}

test("returns the app token and expiry on a successful exchange", async () => {
  const fetchImpl = fetchReturning({
    ok: true,
    json: async () => ({ token: "app-jwt", expiresAt: "2026-07-18T00:00:00Z" }),
  });

  const result = await exchangeProviderToken("id-tok", "google", "http://api.test", fetchImpl);

  expect(result).toEqual({ apiToken: "app-jwt", apiExpiresAt: "2026-07-18T00:00:00Z" });
});

test("posts the id token and provider to the session endpoint", async () => {
  const fetchImpl = fetchReturning({ ok: true, json: async () => ({ token: "t", expiresAt: "x" }) });

  await exchangeProviderToken("id-tok", "microsoft", "http://api.test", fetchImpl);

  expect(fetchImpl).toHaveBeenCalledWith(
    "http://api.test/api/auth/session",
    expect.objectContaining({
      method: "POST",
      body: JSON.stringify({ idToken: "id-tok", provider: "microsoft" }),
    }),
  );
});

test("flags apiError when the backend rejects the sign-in", async () => {
  const fetchImpl = fetchReturning({ ok: false, status: 401 });

  const result = await exchangeProviderToken("id-tok", "google", "http://api.test", fetchImpl);

  expect(result).toEqual({ apiError: true });
});
