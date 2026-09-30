using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DA.NA.Staleness;

/// <summary>
/// Finds active projects due for freshness reminders and sends emails to org users.
/// </summary>
public class FreshnessReminderService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly FreshnessReminderEmailComposer _composer;
    private readonly ILogger<FreshnessReminderService> _logger;
    private readonly TimeProvider _time;

    public FreshnessReminderService(
        AppDbContext db,
        IEmailSender emailSender,
        FreshnessReminderEmailComposer composer,
        ILogger<FreshnessReminderService> logger,
        TimeProvider? time = null)
    {
        _db = db;
        _emailSender = emailSender;
        _composer = composer;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task ProcessDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = _time.GetUtcNow().UtcDateTime;

        var activeProjects = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Active)
            .Select(p => new { p.Id, p.Name, p.OrgId, OrgName = p.Org!.Name })
            .ToListAsync(cancellationToken);

        if (activeProjects.Count == 0)
        {
            _logger.LogDebug("Freshness reminder job: no active projects.");
            return;
        }

        var projectIds = activeProjects.Select(p => p.Id).ToList();
        var submittedAssessments = await _db.NeedsAssessments
            .AsNoTracking()
            .Where(a => projectIds.Contains(a.ProjectId)
                && a.Status == AssessmentStatus.Submitted
                && a.SubmittedAt != null)
            .ToListAsync(cancellationToken);

        var currentByProject = submittedAssessments
            .GroupBy(a => a.ProjectId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.SubmittedAt).First());

        var candidates = activeProjects
            .Where(p => currentByProject.ContainsKey(p.Id))
            .Select(p =>
            {
                var a = currentByProject[p.Id];
                return new ProjectReminderCandidate(
                    p.Id, p.Name, p.OrgId, p.OrgName,
                    new AssessmentReminderCandidate(
                        a.Id, a.SubmittedAt!.Value, a.PreStaleEmailSentAt, a.StaleEmailSentAt));
            })
            .ToList();

        if (candidates.Count == 0)
        {
            _logger.LogDebug("Freshness reminder job: no confirmed needs lists.");
            return;
        }

        _logger.LogInformation("Freshness reminder job: checking {Count} project(s).", candidates.Count);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessProjectAsync(candidate, utcNow, cancellationToken);
        }
    }

    private async Task ProcessProjectAsync(
        ProjectReminderCandidate candidate,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var assessment = candidate.CurrentAssessment!;
        var ageInDays = FreshnessReminderRules.AgeInDays(assessment.SubmittedAt, utcNow);
        var kind = FreshnessReminderRules.Decide(
            ageInDays,
            assessment.PreStaleEmailSentAt,
            assessment.StaleEmailSentAt);

        if (kind == FreshnessReminderKind.None)
            return;

        var recipients = await _db.Users
            .AsNoTracking()
            .Where(u => u.OrgId == candidate.OrgId
                && (u.Role == UserRole.OrgMember || u.Role == UserRole.OrgAdmin))
            .Select(u => new Recipient(u.Email, u.FirstName))
            .ToListAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Freshness reminder skipped for project {ProjectId}: no OrgMember/OrgAdmin recipients.",
                candidate.ProjectId);
            return;
        }

        foreach (var recipient in recipients)
        {
            var message = kind switch
            {
                FreshnessReminderKind.PreStaleWarning => _composer.ComposePreStaleWarning(
                    recipient.Email, recipient.FirstName, candidate.OrgName, candidate.ProjectName,
                    assessment.SubmittedAt, ageInDays),
                FreshnessReminderKind.StaleNotice => _composer.ComposeStaleNotice(
                    recipient.Email, recipient.FirstName, candidate.OrgName, candidate.ProjectName,
                    assessment.SubmittedAt, ageInDays),
                _ => throw new InvalidOperationException($"Unexpected reminder kind: {kind}"),
            };

            await _emailSender.SendAsync(message, cancellationToken);
        }

        var tracked = await _db.NeedsAssessments
            .FirstAsync(a => a.Id == assessment.AssessmentId, cancellationToken);

        var sentAt = utcNow;
        if (kind == FreshnessReminderKind.PreStaleWarning)
            tracked.PreStaleEmailSentAt = sentAt;
        else
            tracked.StaleEmailSentAt = sentAt;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Freshness reminder {Kind} sent for project {ProjectId} to {RecipientCount} recipient(s).",
            kind, candidate.ProjectId, recipients.Count);
    }

    private sealed record Recipient(string Email, string FirstName);

    internal sealed record AssessmentReminderCandidate(
        Guid AssessmentId,
        DateTime SubmittedAt,
        DateTime? PreStaleEmailSentAt,
        DateTime? StaleEmailSentAt);

    internal sealed record ProjectReminderCandidate(
        Guid ProjectId,
        string ProjectName,
        Guid OrgId,
        string OrgName,
        AssessmentReminderCandidate CurrentAssessment);
}
