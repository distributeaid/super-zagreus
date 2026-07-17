using System.Net;
using System.Net.Http.Json;
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using DA.NA.Tests.Infrastructure;
using Xunit;

namespace DA.NA.Tests.Assessments;

/// <summary>
/// Regression coverage for GET /api/projects/{projectId}/assessments/current.
///
/// The endpoint used to return the tracked NeedsAssessment entity graph directly.
/// With .Include(a => a.Items), EF Core's relationship fixup sets each
/// AssessmentItem.Assessment back-reference to the same tracked NeedsAssessment
/// instance, forming a reference cycle. System.Text.Json has no ReferenceHandler
/// configured (see Program.cs), so serializing any assessment with at least one item
/// threw "A possible object cycle was detected", surfacing as an uncaught 500 — even
/// though the query and status code logic were otherwise correct. This only reproduces
/// when the assessment actually has items, which is why the "no submitted assessment"
/// (404) path looked fine in the demo.
/// </summary>
public class AssessmentsControllerTests : TestBase
{
    [Fact]
    public async Task GetCurrent_with_submitted_assessment_and_items_returns_200()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var submittedAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        await SeedAsync(async db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = DateTime.UtcNow });
            db.Projects.Add(new Project
            {
                Id = projectId, OrgId = orgId, Name = "Athens Warehouse",
                Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow
            });

            var assessment = new NeedsAssessment
            {
                Id = assessmentId,
                ProjectId = projectId,
                CreatedBy = userId,
                Status = AssessmentStatus.Submitted,
                CreatedAt = DateTime.UtcNow,
                SubmittedAt = submittedAt,
            };
            db.NeedsAssessments.Add(assessment);

            // Use the fixed seed unit IDs already used elsewhere so we don't depend on
            // SeedData having run (ApiFactory runs in the "Testing" environment, which
            // skips SeedData — see TestBase/ApiFactory).
            db.Units.Add(new Unit { Id = UnitIds.Item, Name = "item", Dimension = UnitDimension.Count, ToBaseFactor = 1m });

            var itemTypeId = Guid.NewGuid();
            db.ItemTypes.Add(new ItemType
            {
                Id = itemTypeId, Category = "Food", Name = "Rice", DefaultUnitId = UnitIds.Item
            });

            db.AssessmentItems.Add(new AssessmentItem
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
                ItemTypeId = itemTypeId,
                Quantity = 10,
                UnitId = UnitIds.Item,
                CreatedAt = DateTime.UtcNow,
            });

            await Task.CompletedTask;
        });

        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId, userId));
        var res = await client.GetAsync($"/api/projects/{projectId}/assessments/current");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<CurrentAssessmentResponse>();
        Assert.NotNull(body);
        Assert.Equal(submittedAt, body!.SubmittedAt);
    }

    private record CurrentAssessmentResponse(Guid Id, AssessmentStatus Status, DateTime? SubmittedAt);
}
