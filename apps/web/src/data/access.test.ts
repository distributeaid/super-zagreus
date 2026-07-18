import { sessionAccess } from "./access";

const NOW = new Date("2026-07-17T12:00:00Z");

describe("sessionAccess", () => {
  it("treats no session as needing re-authentication", () => {
    expect(sessionAccess(null, NOW)).toBe("reauth");
    expect(sessionAccess(undefined, NOW)).toBe("reauth");
  });

  it("denies a session the backend rejected (apiError)", () => {
    expect(sessionAccess({ apiError: true }, NOW)).toBe("denied");
  });

  it("lets apiError win even if a stale token is somehow present", () => {
    expect(sessionAccess({ apiToken: "t", apiError: true }, NOW)).toBe("denied");
  });

  it("allows a token with no expiry information", () => {
    expect(sessionAccess({ apiToken: "t" }, NOW)).toBe("allow");
  });

  it("allows a token that has not yet expired", () => {
    const future = new Date("2026-07-17T20:00:00Z").toISOString();
    expect(sessionAccess({ apiToken: "t", apiExpiresAt: future }, NOW)).toBe("allow");
  });

  it("requires re-authentication for a present-but-expired token", () => {
    const past = new Date("2026-07-17T04:00:00Z").toISOString();
    expect(sessionAccess({ apiToken: "t", apiExpiresAt: past }, NOW)).toBe("reauth");
  });

  it("requires re-authentication for a session with neither token nor error", () => {
    expect(sessionAccess({}, NOW)).toBe("reauth");
  });
});
