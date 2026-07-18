import { freshnessStatus, STALE_AFTER_DAYS } from "./freshness";

const NOW = new Date("2026-07-16T00:00:00Z");

describe("freshnessStatus", () => {
  it("treats never-confirmed lists as stale", () => {
    expect(freshnessStatus(null, NOW)).toBe("stale");
  });

  it("treats recently confirmed lists as fresh", () => {
    const recent = new Date("2026-06-01T00:00:00Z").toISOString();
    expect(freshnessStatus(recent, NOW)).toBe("fresh");
  });

  it("treats lists confirmed more than 90 days ago as stale", () => {
    const old = new Date("2026-04-01T00:00:00Z").toISOString(); // > 90 days before NOW
    expect(freshnessStatus(old, NOW)).toBe("stale");
  });

  it("uses a 90-day window", () => {
    expect(STALE_AFTER_DAYS).toBe(90);
  });
});
