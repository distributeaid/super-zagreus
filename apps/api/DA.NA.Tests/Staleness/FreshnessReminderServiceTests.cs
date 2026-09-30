using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using DA.NA.Staleness;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DA.NA.Tests.Staleness;

public class FreshnessReminderServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private CollectingEmailSender _emailSender = null!;
    private AppDbContext _db = null!;
    private FreshnessReminderService _service = null!;
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _emailSender = new CollectingEmailSender();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["WebApp:BaseUrl"] = "http://localhost:3000" })
            .Build();
        var composer = new FreshnessReminderEmailComposer(config);
        _service = new FreshnessReminderService(
            _db,
            _emailSender,
            composer,
            NullLogger<FreshnessReminderService>.Instance,
            new FixedTimeProvider(Now));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Day77_SendsPreStale_ToOrgUsers_AndSetsFlag()
    {
        var (orgId, projectId, assessmentId) = await SeedProjectWithAssessmentAsync(
            submittedAt: Now.AddDays(-77));

        await _service.ProcessDueRemindersAsync();

        var assessment = await _db.NeedsAssessments.FindAsync(assessmentId);
        Assert.NotNull(assessment!.PreStaleEmailSentAt);
        Assert.Null(assessment.StaleEmailSentAt);
        Assert.Equal(2, _emailSender.Messages.Count);
        Assert.All(_emailSender.Messages, m => Assert.Contains("goes stale in 13 days", m.Subject));
    }

    [Fact]
    public async Task Day76_SendsPreStale_OnExactBoundary()
    {
        await SeedProjectWithAssessmentAsync(submittedAt: Now.AddDays(-76));

        await _service.ProcessDueRemindersAsync();

        Assert.Equal(2, _emailSender.Messages.Count);
        Assert.All(_emailSender.Messages, m => Assert.Contains("goes stale in 14 days", m.Subject));
    }

    [Fact]
    public async Task Day90_SendsStale_OnExactBoundary()
    {
        await SeedProjectWithAssessmentAsync(submittedAt: Now.AddDays(-90));

        await _service.ProcessDueRemindersAsync();

        Assert.Equal(2, _emailSender.Messages.Count);
        Assert.All(_emailSender.Messages, m => Assert.Contains("as of today", m.Body));
    }

    [Fact]
    public async Task SecondRun_DoesNotSendDuplicateEmails()
    {
        await SeedProjectWithAssessmentAsync(submittedAt: Now.AddDays(-77));

        await _service.ProcessDueRemindersAsync();
        await _service.ProcessDueRemindersAsync();

        Assert.Equal(2, _emailSender.Messages.Count);
    }

    [Fact]
    public async Task Day95_CatchUp_SendsStaleOnly_AndSetsStaleFlag()
    {
        var (_, _, assessmentId) = await SeedProjectWithAssessmentAsync(
            submittedAt: Now.AddDays(-95));

        await _service.ProcessDueRemindersAsync();

        var assessment = await _db.NeedsAssessments.FindAsync(assessmentId);
        Assert.Null(assessment!.PreStaleEmailSentAt);
        Assert.NotNull(assessment.StaleEmailSentAt);
        Assert.Equal(2, _emailSender.Messages.Count);
        Assert.All(_emailSender.Messages, m => Assert.Contains("out of date", m.Subject));
    }

    [Fact]
    public async Task InactiveProject_IsSkipped()
    {
        await SeedProjectWithAssessmentAsync(submittedAt: Now.AddDays(-77), status: ProjectStatus.Inactive);

        await _service.ProcessDueRemindersAsync();

        Assert.Empty(_emailSender.Messages);
    }

    [Fact]
    public async Task NeverConfirmed_IsSkipped()
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _db.Organisations.Add(new Organisation { Id = orgId, Name = "Org", CreatedAt = Now });
        _db.Projects.Add(new Project
        {
            Id = projectId, OrgId = orgId, Name = "Project", Status = ProjectStatus.Active, CreatedAt = Now,
        });
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(), OrgId = orgId, Username = "member", FirstName = "Jane", LastName = "Doe",
            Email = "jane@example.org", PasswordHash = "x", Role = UserRole.OrgMember, CreatedAt = Now,
        });
        await _db.SaveChangesAsync();

        await _service.ProcessDueRemindersAsync();

        Assert.Empty(_emailSender.Messages);
    }

    private async Task<(Guid OrgId, Guid ProjectId, Guid AssessmentId)> SeedProjectWithAssessmentAsync(
        DateTime submittedAt,
        ProjectStatus status = ProjectStatus.Active)
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();

        _db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = Now });
        _db.Projects.Add(new Project
        {
            Id = projectId, OrgId = orgId, Name = "Greece Ops", Status = status, CreatedAt = Now,
        });
        _db.Users.AddRange(
            new User
            {
                Id = Guid.NewGuid(), OrgId = orgId, Username = "admin", FirstName = "Alex", LastName = "Admin",
                Email = "admin@example.org", PasswordHash = "x", Role = UserRole.OrgAdmin, CreatedAt = Now,
            },
            new User
            {
                Id = Guid.NewGuid(), OrgId = orgId, Username = "member", FirstName = "Jane", LastName = "Member",
                Email = "jane@example.org", PasswordHash = "x", Role = UserRole.OrgMember, CreatedAt = Now,
            });
        _db.NeedsAssessments.Add(new NeedsAssessment
        {
            Id = assessmentId,
            ProjectId = projectId,
            CreatedBy = Guid.NewGuid(),
            Status = AssessmentStatus.Submitted,
            SubmittedAt = submittedAt,
            CreatedAt = submittedAt,
        });
        await _db.SaveChangesAsync();
        return (orgId, projectId, assessmentId);
    }

    private sealed class CollectingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
