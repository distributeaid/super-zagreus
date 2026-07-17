namespace DA.NA.Core.Entities;

/// <summary>
/// Represents a relationship between two organisations — typically a hub and a frontline org it supports.
/// Any org can be a parent, a child, or both. Hub visibility is derived from this table:
/// a user belonging to a parent org can see the needs of all its child orgs.
/// </summary>
public class OrgAssociation
{
    public Guid ParentOrgId { get; set; }
    public Organisation ParentOrg { get; set; } = null!;

    public Guid ChildOrgId { get; set; }
    public Organisation ChildOrg { get; set; } = null!;
}
