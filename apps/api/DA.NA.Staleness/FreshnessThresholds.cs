namespace DA.NA.Staleness;

/// <summary>
/// Freshness reminder day thresholds. <see cref="StaleAfterDays"/> aligns with
/// apps/web/src/data/freshness.ts (STALE_AFTER_DAYS = 90).
/// </summary>
public static class FreshnessThresholds
{
    public const int PreStaleWarningDays = 76;
    public const int StaleAfterDays = 90;
}
