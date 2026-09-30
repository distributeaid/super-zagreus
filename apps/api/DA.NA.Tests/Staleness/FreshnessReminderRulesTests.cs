using DA.NA.Staleness;
using Xunit;

namespace DA.NA.Tests.Staleness;

public class FreshnessReminderRulesTests
{
    private static readonly DateTime Submitted = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Decide_Before76Days_ReturnsNone()
    {
        var age = FreshnessReminderRules.AgeInDays(Submitted, Submitted.AddDays(75));
        var kind = FreshnessReminderRules.Decide(age, preStaleEmailSentAt: null, staleEmailSentAt: null);
        Assert.Equal(FreshnessReminderKind.None, kind);
    }

    [Fact]
    public void Decide_At76Days_ReturnsPreStaleWarning()
    {
        var age = FreshnessReminderRules.AgeInDays(Submitted, Submitted.AddDays(76));
        var kind = FreshnessReminderRules.Decide(age, preStaleEmailSentAt: null, staleEmailSentAt: null);
        Assert.Equal(FreshnessReminderKind.PreStaleWarning, kind);
    }

    [Fact]
    public void Decide_At89Days_ReturnsPreStaleWarning()
    {
        var age = FreshnessReminderRules.AgeInDays(Submitted, Submitted.AddDays(89));
        var kind = FreshnessReminderRules.Decide(age, preStaleEmailSentAt: null, staleEmailSentAt: null);
        Assert.Equal(FreshnessReminderKind.PreStaleWarning, kind);
    }

    [Fact]
    public void Decide_At90Days_ReturnsStaleNotice()
    {
        var age = FreshnessReminderRules.AgeInDays(Submitted, Submitted.AddDays(90));
        var kind = FreshnessReminderRules.Decide(age, preStaleEmailSentAt: null, staleEmailSentAt: null);
        Assert.Equal(FreshnessReminderKind.StaleNotice, kind);
    }

    [Fact]
    public void Decide_CatchUp_BothThresholdsMissed_ReturnsStaleOnly()
    {
        var age = FreshnessReminderRules.AgeInDays(Submitted, Submitted.AddDays(95));
        var kind = FreshnessReminderRules.Decide(age, preStaleEmailSentAt: null, staleEmailSentAt: null);
        Assert.Equal(FreshnessReminderKind.StaleNotice, kind);
    }

    [Fact]
    public void Decide_PreStaleAlreadySent_ReturnsNone()
    {
        var age = FreshnessReminderRules.AgeInDays(Submitted, Submitted.AddDays(80));
        var kind = FreshnessReminderRules.Decide(age, preStaleEmailSentAt: Submitted.AddDays(76), staleEmailSentAt: null);
        Assert.Equal(FreshnessReminderKind.None, kind);
    }

    [Fact]
    public void Decide_StaleAlreadySent_ReturnsNone()
    {
        var age = FreshnessReminderRules.AgeInDays(Submitted, Submitted.AddDays(100));
        var kind = FreshnessReminderRules.Decide(age, preStaleEmailSentAt: null, staleEmailSentAt: Submitted.AddDays(90));
        Assert.Equal(FreshnessReminderKind.None, kind);
    }
}
