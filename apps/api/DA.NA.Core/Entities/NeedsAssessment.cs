namespace DA.NA.Core.Entities;

public class NeedsAssessment
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid CreatedBy { get; set; }            // user id (no FK for prototype simplicity)

    public AssessmentStatus Status { get; set; } = AssessmentStatus.Draft;

    // Points to the previous assessment this one replaces (append-only versioning)
    public Guid? SupersedesId { get; set; }
    public NeedsAssessment? Supersedes { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }     // staleness clock starts here

    // Freshness email reminders (issue #22) — one send per type per assessment cycle
    public DateTime? PreStaleEmailSentAt { get; set; }
    public DateTime? StaleEmailSentAt { get; set; }

    public List<AssessmentItem> Items { get; set; } = [];
}
