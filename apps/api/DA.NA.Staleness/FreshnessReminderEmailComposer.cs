using Microsoft.Extensions.Configuration;

namespace DA.NA.Staleness;

/// <summary>
/// Builds plain-text freshness reminder emails (issue #22).
/// </summary>
public class FreshnessReminderEmailComposer
{
    private readonly string _webAppBaseUrl;

    public FreshnessReminderEmailComposer(IConfiguration configuration)
    {
        _webAppBaseUrl = configuration["WebApp:BaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:3000";
    }

    public EmailMessage ComposePreStaleWarning(
        string toEmail,
        string firstName,
        string orgName,
        string projectName,
        DateTime lastConfirmedAtUtc,
        int ageInDays)
    {
        var daysUntilStale = FreshnessThresholds.StaleAfterDays - ageInDays;
        var lastConfirmed = lastConfirmedAtUtc.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var ctaUrl = $"{_webAppBaseUrl}/needs";

        var subject = daysUntilStale == 1
            ? "Your needs list goes stale tomorrow"
            : $"Your needs list goes stale in {daysUntilStale} days";
        var staleInText = daysUntilStale == 1
            ? "tomorrow"
            : $"in {daysUntilStale} day(s)";
        var body = $"""
            Hi {firstName},

            The needs list for {projectName} ({orgName}) was last confirmed on {lastConfirmed}.
            It will be considered out of date {staleInText} if not updated.

            Please review and confirm your current needs:
            {ctaUrl}

            — Distribute Aid
            """;

        return new EmailMessage(toEmail, subject, body);
    }

    public EmailMessage ComposeStaleNotice(
        string toEmail,
        string firstName,
        string orgName,
        string projectName,
        DateTime lastConfirmedAtUtc,
        int ageInDays)
    {
        var daysSinceStale = ageInDays - FreshnessThresholds.StaleAfterDays;
        var lastConfirmed = lastConfirmedAtUtc.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var ctaUrl = $"{_webAppBaseUrl}/needs";

        var subject = "Your needs list is now out of date";
        var staleForText = daysSinceStale == 0
            ? "as of today"
            : $"for {daysSinceStale} day(s)";
        var body = $"""
            Hi {firstName},

            The needs list for {projectName} ({orgName}) was last confirmed on {lastConfirmed}.
            It has been out of date {staleForText}.

            Please review and confirm your current needs:
            {ctaUrl}

            — Distribute Aid
            """;

        return new EmailMessage(toEmail, subject, body);
    }
}
