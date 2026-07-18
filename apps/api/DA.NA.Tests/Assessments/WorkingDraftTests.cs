using System.Net;
using System.Net.Http.Json;
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using DA.NA.Tests.Infrastructure;
using Xunit;

namespace DA.NA.Tests.Assessments;

public class WorkingDraftTests : TestBase
{
    // Seeds an org, a project, one item type + unit, and returns their ids.
    private async Task<(Guid orgId, Guid projectId, Guid itemTypeId, Guid unitId)> SeedCatalogAsync()
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var itemTypeId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = DateTime.UtcNow });
            db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Main", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow });
            db.Units.Add(new Unit { Id = unitId, Name = "item", Dimension = UnitDimension.Count, ToBaseFactor = 1 });
            db.ItemTypes.Add(new ItemType { Id = itemTypeId, Category = "Hygiene", Name = "Soap", DefaultUnitId = unitId });
            return Task.CompletedTask;
        });
        return (orgId, projectId, itemTypeId, unitId);
    }

    private record ItemDto(Guid id, Guid itemTypeId, decimal quantity);
    private record DraftDto(Guid id, string status, Guid? supersedesId, List<ItemDto> items);

    [Fact]
    public async Task Creates_an_empty_draft_when_none_exists_and_no_prior_submitted()
    {
        var (orgId, projectId, _, _) = await SeedCatalogAsync();
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var res = await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var draft = await res.Content.ReadFromJsonAsync<DraftDto>();
        Assert.Equal("Draft", draft!.status);
        Assert.Empty(draft.items);
    }

    [Fact]
    public async Task Seeds_items_from_the_latest_submitted_assessment()
    {
        var (orgId, projectId, itemTypeId, unitId) = await SeedCatalogAsync();
        var submittedId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.NeedsAssessments.Add(new NeedsAssessment
            {
                Id = submittedId, ProjectId = projectId, CreatedBy = Guid.NewGuid(),
                Status = AssessmentStatus.Submitted, CreatedAt = DateTime.UtcNow.AddDays(-1), SubmittedAt = DateTime.UtcNow.AddDays(-1),
                Items = { new AssessmentItem { Id = Guid.NewGuid(), ItemTypeId = itemTypeId, UnitId = unitId, Quantity = 5, CreatedAt = DateTime.UtcNow.AddDays(-1) } }
            });
            return Task.CompletedTask;
        });
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var res = await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null);

        var draft = await res.Content.ReadFromJsonAsync<DraftDto>();
        Assert.Equal("Draft", draft!.status);
        Assert.Equal(submittedId, draft.supersedesId);
        var item = Assert.Single(draft.items);
        Assert.Equal(itemTypeId, item.itemTypeId);
        Assert.Equal(5, item.quantity);
    }

    [Fact]
    public async Task Resumes_the_existing_open_draft_instead_of_creating_a_second()
    {
        var (orgId, projectId, _, _) = await SeedCatalogAsync();
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        var first = await (await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null))
            .Content.ReadFromJsonAsync<DraftDto>();
        var second = await (await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null))
            .Content.ReadFromJsonAsync<DraftDto>();

        Assert.Equal(first!.id, second!.id);
    }

    [Fact]
    public async Task Cross_org_access_returns_404()
    {
        var (_, projectId, _, _) = await SeedCatalogAsync();
        var otherOrg = Guid.NewGuid();
        var client = ClientFor(JwtHelper.ForOrgAdmin(otherOrg));

        var res = await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
