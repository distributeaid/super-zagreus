/** A needs list is considered stale once it hasn't been confirmed for this many days. */
export const STALE_AFTER_DAYS = 90;

/**
 * Classify a needs list's freshness from its last-confirmed timestamp.
 *
 * @param lastConfirmedAt ISO timestamp of the current submitted assessment, or `null`
 *                        if it has never been confirmed.
 * @param now Reference time (injectable for tests; defaults to now).
 * @returns `"stale"` when never confirmed or older than {@link STALE_AFTER_DAYS},
 *          otherwise `"fresh"`.
 */
export function freshnessStatus(
  lastConfirmedAt: string | null,
  now: Date = new Date(),
): "fresh" | "stale" {
  if (!lastConfirmedAt) return "stale";
  const confirmed = new Date(lastConfirmedAt).getTime();
  const ageDays = (now.getTime() - confirmed) / (1000 * 60 * 60 * 24);
  return ageDays > STALE_AFTER_DAYS ? "stale" : "fresh";
}
