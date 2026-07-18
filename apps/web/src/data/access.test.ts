import { sessionAccess } from "./access";

const NOW = new Date("2026-07-17T12:00:00Z");

test("no session is treated as needing re-authentication", () => {
  expect(sessionAccess(null, NOW)).toBe("reauth");
  expect(sessionAccess(undefined, NOW)).toBe("reauth");
});

test("a session the backend rejected (apiError) is denied", () => {
  expect(sessionAccess({ apiError: true }, NOW)).toBe("denied");
});

test("apiError wins even if a stale token is somehow present", () => {
  expect(sessionAccess({ apiToken: "t", apiError: true }, NOW)).toBe("denied");
});

test("a token with no expiry information is allowed", () => {
  expect(sessionAccess({ apiToken: "t" }, NOW)).toBe("allow");
});

test("a token that has not yet expired is allowed", () => {
  const future = new Date("2026-07-17T20:00:00Z").toISOString();
  expect(sessionAccess({ apiToken: "t", apiExpiresAt: future }, NOW)).toBe("allow");
});

test("a present-but-expired token needs re-authentication", () => {
  const past = new Date("2026-07-17T04:00:00Z").toISOString();
  expect(sessionAccess({ apiToken: "t", apiExpiresAt: past }, NOW)).toBe("reauth");
});

test("a signed-in session with neither token nor error needs re-authentication", () => {
  expect(sessionAccess({}, NOW)).toBe("reauth");
});
