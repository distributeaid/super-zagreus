using DA.NA.Staleness;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DA.NA.Tests.Staleness;

public class FreshnessReminderEmailComposerTests
{
    private static FreshnessReminderEmailComposer CreateComposer() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["WebApp:BaseUrl"] = "https://app.example" })
            .Build());

    [Fact]
    public void PreStale_Day76_SubjectAndBodyUse14Days()
    {
        var submitted = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var message = CreateComposer().ComposePreStaleWarning(
            "user@example.org", "Alex", "Aegean Hub", "Greece Ops", submitted, ageInDays: 76);

        Assert.Contains("goes stale in 14 days", message.Subject);
        Assert.Contains("in 14 day(s)", message.Body);
        Assert.Contains("https://app.example/needs", message.Body);
    }

    [Fact]
    public void PreStale_Day89_SubjectSaysTomorrow()
    {
        var submitted = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var message = CreateComposer().ComposePreStaleWarning(
            "user@example.org", "Alex", "Aegean Hub", "Greece Ops", submitted, ageInDays: 89);

        Assert.Contains("goes stale tomorrow", message.Subject);
        Assert.Contains("tomorrow", message.Body);
    }

    [Fact]
    public void Stale_Day90_BodySaysAsOfToday()
    {
        var submitted = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var message = CreateComposer().ComposeStaleNotice(
            "user@example.org", "Alex", "Aegean Hub", "Greece Ops", submitted, ageInDays: 90);

        Assert.Contains("as of today", message.Body);
    }

    [Fact]
    public void Stale_Day95_BodyShowsDaysSinceStale()
    {
        var submitted = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var message = CreateComposer().ComposeStaleNotice(
            "user@example.org", "Alex", "Aegean Hub", "Greece Ops", submitted, ageInDays: 95);

        Assert.Contains("for 5 day(s)", message.Body);
    }
}
