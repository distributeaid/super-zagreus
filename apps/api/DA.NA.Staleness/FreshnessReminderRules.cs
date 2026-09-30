namespace DA.NA.Staleness;

/// <summary>
/// Pure decision logic for which freshness reminder to send, if any.
/// </summary>
public static class FreshnessReminderRules
{
    public static int AgeInDays(DateTime submittedAtUtc, DateTime utcNow) =>
        (int)(utcNow - submittedAtUtc).TotalDays;

    /// <summary>
    /// Returns the reminder type to send. Stale takes priority over pre-stale (catch-up rule).
    /// </summary>
    public static FreshnessReminderKind Decide(
        int ageInDays,
        DateTime? preStaleEmailSentAt,
        DateTime? staleEmailSentAt)
    {
        if (ageInDays >= FreshnessThresholds.StaleAfterDays)
        {
            if (staleEmailSentAt is null)
                return FreshnessReminderKind.StaleNotice;
            return FreshnessReminderKind.None;
        }

        if (ageInDays >= FreshnessThresholds.PreStaleWarningDays && preStaleEmailSentAt is null)
            return FreshnessReminderKind.PreStaleWarning;

        return FreshnessReminderKind.None;
    }
}
