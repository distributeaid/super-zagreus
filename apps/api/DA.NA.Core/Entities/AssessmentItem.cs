namespace DA.NA.Core.Entities;

public class AssessmentItem
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public NeedsAssessment Assessment { get; set; } = null!;
    public Guid ItemTypeId { get; set; }
    public ItemType ItemType { get; set; } = null!;
    public decimal Quantity { get; set; }
    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public string? Notes { get; set; }             // replaces forced sub-question detail
    public DateTime CreatedAt { get; set; }

    public List<StalenessResponse> StalenessResponses { get; set; } = [];
}
