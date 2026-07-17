namespace DA.NA.Core.Entities;

public class StalenessResponse
{
    public Guid Id { get; set; }
    public Guid AssessmentItemId { get; set; }
    public AssessmentItem AssessmentItem { get; set; } = null!;
    public DateTime NotificationSentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public StalenessResponseType? Response { get; set; }

    // Populated when Response == Updated; links to the new assessment version created
    public Guid? NewAssessmentId { get; set; }
    public NeedsAssessment? NewAssessment { get; set; }
}
