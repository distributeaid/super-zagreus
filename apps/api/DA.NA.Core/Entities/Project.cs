namespace DA.NA.Core.Entities;

public class Project
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public Organisation Org { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Region { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSubmittedAt { get; set; }   // staleness clock for project-level data

    public List<NeedsAssessment> Assessments { get; set; } = [];
}
