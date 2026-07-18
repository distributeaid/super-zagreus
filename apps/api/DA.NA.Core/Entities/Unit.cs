namespace DA.NA.Core.Entities;

public class Unit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;      // e.g. "kg", "lb", "item"
    public UnitDimension Dimension { get; set; }
    public decimal ToBaseFactor { get; set; }              // multiply by this to get grams / ml / 1

    public List<ItemType> ItemTypes { get; set; } = [];
    public List<AssessmentItem> AssessmentItems { get; set; } = [];
}
