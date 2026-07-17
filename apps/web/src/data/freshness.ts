export const STALE_AFTER_DAYS = 90;

export function freshnessStatus(
  lastConfirmedAt: string | null,
  now: Date = new Date(),
): "fresh" | "stale" {
  if (!lastConfirmedAt) return "stale";
  const confirmed = new Date(lastConfirmedAt).getTime();
  const ageDays = (now.getTime() - confirmed) / (1000 * 60 * 60 * 24);
  return ageDays > STALE_AFTER_DAYS ? "stale" : "fresh";
}
