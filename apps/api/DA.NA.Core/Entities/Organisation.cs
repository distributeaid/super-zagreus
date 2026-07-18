namespace DA.NA.Core.Entities;

public class Organisation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SalesforceId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastSubmittedAt { get; set; }   // staleness clock for org-level data

    public List<Project> Projects { get; set; } = [];
    public List<User> Users { get; set; } = [];

    // Orgs this org is a parent of (e.g. hub → frontline)
    public List<OrgAssociation> ChildAssociations { get; set; } = [];
    // Orgs this org is a child of
    public List<OrgAssociation> ParentAssociations { get; set; } = [];
}
