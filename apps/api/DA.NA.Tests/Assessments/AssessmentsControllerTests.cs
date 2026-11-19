using System.Net;
using System.Net.Http.Json;
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using DA.NA.Tests.Infrastructure;
using Xunit;

namespace DA.NA.Tests.Assessments;

/// <summary>
/// Regression coverage for GET /api/projects/{projectId}/assessments/current and
/// GET /api/assessments/{id}.
///
/// The endpoints used to return the tracked NeedsAssessment entity graph directly.
/// With .Include(a => a.Items), EF Core's relationship fixup sets each
/// AssessmentItem.Assessment back-reference to the same tracked NeedsAssessment
/// instance, forming a reference cycle. System.Text.Json has no ReferenceHandler
/// configured (see Program.cs), so serializing any assessment with at least one item
/// threw "A possible object cycle was detected", surfacing as an uncaught 500 — even
/// though the query and status code logic were otherwise correct. This only reproduces
/// when the assessment actually has items, which is why the "no submitted assessment"
/// (404) path looked fine in the demo. GetById now returns a projected DTO to avoid the
/// same cycle, and its org-scoping / not-found behavior is also covered here.
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

    [Fact]
    public async Task GetByProject_returns_status_as_a_string()
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = DateTime.UtcNow });
            db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Main", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow });
            db.NeedsAssessments.Add(new NeedsAssessment { Id = Guid.NewGuid(), ProjectId = projectId, CreatedBy = Guid.NewGuid(), Status = AssessmentStatus.Draft, CreatedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });

        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));
        var res = await client.GetAsync($"/api/projects/{projectId}/assessments");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<AssessmentListRow>>();
        var row = Assert.Single(list!);
        Assert.Equal("Draft", row.Status); // must be the STRING "Draft", not 0 — selectNeedsMode depends on it
    }

    private record AssessmentListRow(Guid Id, string Status);

    [Fact]
    public async Task GetById_returns_the_assessment_with_its_items()
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var itemTypeId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = DateTime.UtcNow });
            db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Main", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow });
            db.Units.Add(new Unit { Id = UnitIds.Item, Name = "item", Dimension = UnitDimension.Count, ToBaseFactor = 1m });
            db.ItemTypes.Add(new ItemType { Id = itemTypeId, Category = "Food", Name = "Rice", DefaultUnitId = UnitIds.Item });
            db.NeedsAssessments.Add(new NeedsAssessment
            {
                Id = assessmentId, ProjectId = projectId, CreatedBy = Guid.NewGuid(),
                Status = AssessmentStatus.Draft, CreatedAt = DateTime.UtcNow,
                Items = { new AssessmentItem { Id = Guid.NewGuid(), ItemTypeId = itemTypeId, UnitId = UnitIds.Item, Quantity = 10, CreatedAt = DateTime.UtcNow } }
            });
            return Task.CompletedTask;
        });

        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));
        var res = await client.GetAsync($"/api/assessments/{assessmentId}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode); // 500 here would mean the serialization cycle regressed
        var body = await res.Content.ReadFromJsonAsync<AssessmentByIdResponse>();
        Assert.NotNull(body);
        Assert.Equal("Draft", body!.Status);
        var item = Assert.Single(body.Items);
        Assert.Equal("Rice", item.ItemType.Name);
        Assert.Equal("Food", item.ItemType.Category);
        Assert.Equal("item", item.Unit.Name);
        Assert.Equal(10, item.Quantity);
    }

    [Fact]
    public async Task GetById_from_another_org_returns_404()
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Org A", CreatedAt = DateTime.UtcNow });
            db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Main", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow });
            db.NeedsAssessments.Add(new NeedsAssessment { Id = assessmentId, ProjectId = projectId, CreatedBy = Guid.NewGuid(), Status = AssessmentStatus.Draft, CreatedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });

        var client = ClientFor(JwtHelper.ForOrgAdmin(Guid.NewGuid())); // a different org
        var res = await client.GetAsync($"/api/assessments/{assessmentId}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetById_unknown_id_returns_404()
    {
        var client = ClientFor(JwtHelper.ForOrgAdmin(Guid.NewGuid()));
        var res = await client.GetAsync($"/api/assessments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── Item mutation + submit endpoints: they return the created/updated entity, which used
    //    to serialize the AssessmentItem.Assessment back-reference into a cycle (500). These pin
    //    that they now return a projected 2xx body — the seam the frontend-action unit tests
    //    (which mock apiPost/apiPatch) never exercised, and which surfaced in the manual E2E. ──

    // Seeds an org, project, unit, item type, and a Draft assessment; returns their ids.
    private async Task<(Guid orgId, Guid projectId, Guid draftId, Guid itemTypeId)> SeedDraftAsync()
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var itemTypeId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = DateTime.UtcNow });
            db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Main", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow });
            db.Units.Add(new Unit { Id = UnitIds.Item, Name = "item", Dimension = UnitDimension.Count, ToBaseFactor = 1m });
            db.ItemTypes.Add(new ItemType { Id = itemTypeId, Category = "Hygiene", Name = "Soap", DefaultUnitId = UnitIds.Item });
            db.NeedsAssessments.Add(new NeedsAssessment { Id = draftId, ProjectId = projectId, CreatedBy = Guid.NewGuid(), Status = AssessmentStatus.Draft, CreatedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        return (orgId, projectId, draftId, itemTypeId);
    }

    [Fact]
    public async Task AddItem_returns_the_created_item_without_a_serialization_cycle()
    {
        var (orgId, _, draftId, itemTypeId) = await SeedDraftAsync();
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var res = await client.PostAsJsonAsync($"/api/assessments/{draftId}/items",
            new { itemTypeId, unitId = UnitIds.Item, quantity = 3 });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode); // 500 here means the cycle regressed
        var item = await res.Content.ReadFromJsonAsync<ItemResponse>();
        Assert.Equal("Soap", item!.ItemType.Name);
        Assert.Equal("item", item.Unit.Name);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public async Task UpdateItem_returns_the_updated_item_without_a_cycle()
    {
        var (orgId, _, draftId, itemTypeId) = await SeedDraftAsync();
        var itemId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.AssessmentItems.Add(new AssessmentItem { Id = itemId, AssessmentId = draftId, ItemTypeId = itemTypeId, UnitId = UnitIds.Item, Quantity = 1, CreatedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var res = await client.PatchAsJsonAsync($"/api/assessments/{draftId}/items/{itemId}", new { quantity = 9 });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode); // 500 here means the cycle regressed
        var item = await res.Content.ReadFromJsonAsync<ItemResponse>();
        Assert.Equal(9, item!.Quantity);
    }

    [Fact]
    public async Task UpdateNotes_returns_the_assessment_without_a_cycle()
    {
        var (orgId, _, draftId, _) = await SeedDraftAsync();
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var res = await client.PatchAsJsonAsync($"/api/assessments/{draftId}", new { notes = "winter priorities" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode); // 500 here means the cycle regressed
        var body = await res.Content.ReadFromJsonAsync<NotesResponse>();
        Assert.Equal("winter priorities", body!.Notes);
    }

    private record NotesResponse(Guid Id, string Status, string? Notes);

    [Fact]
    public async Task Submit_confirms_the_draft_without_a_cycle()
    {
        var (orgId, _, draftId, itemTypeId) = await SeedDraftAsync();
        await SeedAsync(db =>
        {
            db.AssessmentItems.Add(new AssessmentItem { Id = Guid.NewGuid(), AssessmentId = draftId, ItemTypeId = itemTypeId, UnitId = UnitIds.Item, Quantity = 2, CreatedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var res = await client.PostAsync($"/api/assessments/{draftId}/submit", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode); // 500 here means the cycle regressed
        var body = await res.Content.ReadFromJsonAsync<SubmitResponse>();
        Assert.Equal("Submitted", body!.Status);
        Assert.NotNull(body.SubmittedAt);
    }

    private record AssessmentByIdResponse(Guid Id, string Status, List<ItemResponse> Items);
    private record ItemResponse(Guid Id, decimal Quantity, ItemTypeResponse ItemType, UnitResponse Unit);
    private record ItemTypeResponse(string Name, string Category);
    private record UnitResponse(string Name);
    private record SubmitResponse(Guid Id, string Status, DateTime? SubmittedAt);
}
