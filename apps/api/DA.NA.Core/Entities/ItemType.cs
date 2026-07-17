namespace DA.NA.Core.Entities;

public class ItemType
{
    public Guid Id { get; set; }
    public string? StrapiId { get; set; }          // reference for Strapi sync
    public string Category { get; set; } = string.Empty;  // Food, Hygiene, Clothing, etc.
    public string Name { get; set; } = string.Empty;      // e.g. "Diapers", "Halal meals"
    public Guid DefaultUnitId { get; set; }
    public Unit DefaultUnit { get; set; } = null!;

    public List<AssessmentItem> AssessmentItems { get; set; } = [];
}
