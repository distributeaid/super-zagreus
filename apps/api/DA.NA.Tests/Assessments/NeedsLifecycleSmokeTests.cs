using System.Net;
using System.Net.Http.Json;
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using DA.NA.Tests.Infrastructure;
using Xunit;

namespace DA.NA.Tests.Assessments;

/// <summary>
/// End-to-end smoke test of the needs-intake lifecycle over the real HTTP pipeline:
/// working-draft → add item → edit quantity → submit → read current → next draft supersedes.
///
/// Why this exists: every unit-test layer mocks its neighbour (frontend actions mock the API
/// client; component tests mock the actions; per-endpoint tests cover single calls), so bugs
/// that live in the seams — e.g. an endpoint whose DB write succeeds but whose RESPONSE fails
/// to serialize (tracked-entity cycle → 500) — stayed invisible until manual E2E. This test
/// drives the whole advertised flow through the real serializer and asserts every response.
/// </summary>
public class NeedsLifecycleSmokeTests : TestBase
{
    private record ItemDto(Guid Id, Guid ItemTypeId, decimal Quantity);
    private record DraftDto(Guid Id, string Status, Guid? SupersedesId, List<ItemDto> Items);
    private record SubmitDto(Guid Id, string Status, DateTime? SubmittedAt);
    private record CurrentDto(Guid Id, DateTime? SubmittedAt, int ItemCount);
    private record ListRow(Guid Id, string Status);

    [Fact]
    public async Task Full_lifecycle_draft_edit_confirm_and_supersede()
    {
        // Arrange: a hub with a project and one catalog item.
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var itemTypeId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = DateTime.UtcNow });
            db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Main", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow });
            db.Units.Add(new Unit { Id = UnitIds.Item, Name = "item", Dimension = UnitDimension.Count, ToBaseFactor = 1m });
            db.ItemTypes.Add(new ItemType { Id = itemTypeId, Category = "Hygiene", Name = "Soap", DefaultUnitId = UnitIds.Item });
            return Task.CompletedTask;
        });
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));

        // 1. Open the working draft — new hub, so it's created empty.
        var draftRes = await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null);
        Assert.Equal(HttpStatusCode.Created, draftRes.StatusCode);
        var draft = await draftRes.Content.ReadFromJsonAsync<DraftDto>();
        Assert.Equal("Draft", draft!.Status);
        Assert.Empty(draft.Items);

        // 2. Add an item (auto-save).
        var addRes = await client.PostAsJsonAsync($"/api/assessments/{draft.Id}/items",
            new { itemTypeId, unitId = UnitIds.Item, quantity = 1 });
        Assert.Equal(HttpStatusCode.Created, addRes.StatusCode);
        var added = await addRes.Content.ReadFromJsonAsync<ItemDto>();

        // 3. Edit its quantity (auto-save).
        var patchRes = await client.PatchAsJsonAsync($"/api/assessments/{draft.Id}/items/{added!.Id}",
            new { quantity = 5 });
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        // 4. Confirm ("confirm my current needs").
        var submitRes = await client.PostAsync($"/api/assessments/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);
        var submitted = await submitRes.Content.ReadFromJsonAsync<SubmitDto>();
        Assert.Equal("Submitted", submitted!.Status);
        Assert.NotNull(submitted.SubmittedAt);

        // 5. The confirmed list is now "current" and drives the freshness clock.
        var currentRes = await client.GetAsync($"/api/projects/{projectId}/assessments/current");
        Assert.Equal(HttpStatusCode.OK, currentRes.StatusCode);
        var current = await currentRes.Content.ReadFromJsonAsync<CurrentDto>();
        Assert.Equal(draft.Id, current!.Id);
        Assert.Equal(1, current.ItemCount);

        // 6. The list endpoint reports string statuses (what selectNeedsMode consumes).
        var listRes = await client.GetAsync($"/api/projects/{projectId}/assessments");
        var list = await listRes.Content.ReadFromJsonAsync<List<ListRow>>();
        Assert.Equal("Submitted", Assert.Single(list!).Status);

        // 7. Re-opening the working draft starts the next cycle: a NEW draft, seeded from the
        //    confirmed list, superseding it.
        var nextRes = await client.PostAsync($"/api/projects/{projectId}/assessments/working-draft", null);
        Assert.Equal(HttpStatusCode.Created, nextRes.StatusCode);
        var next = await nextRes.Content.ReadFromJsonAsync<DraftDto>();
        Assert.NotEqual(draft.Id, next!.Id);
        Assert.Equal(draft.Id, next.SupersedesId);
        var seeded = Assert.Single(next.Items);
        Assert.Equal(itemTypeId, seeded.ItemTypeId);
        Assert.Equal(5, seeded.Quantity); // carries the edited quantity forward
    }
}
