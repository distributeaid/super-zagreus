import { freshnessStatus, STALE_AFTER_DAYS } from "./freshness";

const NOW = new Date("2026-07-16T00:00:00Z");

test("never-confirmed lists are stale", () => {
  expect(freshnessStatus(null, NOW)).toBe("stale");
});

test("recently confirmed lists are fresh", () => {
  const recent = new Date("2026-06-01T00:00:00Z").toISOString();
  expect(freshnessStatus(recent, NOW)).toBe("fresh");
});

test("lists confirmed more than 90 days ago are stale", () => {
  const old = new Date("2026-04-01T00:00:00Z").toISOString(); // > 90 days before NOW
  expect(freshnessStatus(old, NOW)).toBe("stale");
});

test("the window is 90 days", () => {
  expect(STALE_AFTER_DAYS).toBe(90);
});
