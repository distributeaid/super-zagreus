using System.Net;
using System.Net.Http.Json;
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using DA.NA.Tests.Infrastructure;
using Xunit;

namespace DA.NA.Tests.Organisations;

/// <summary>
/// Tests that the org-scoping rule is enforced on GET /api/organisations:
///   - DA users see all organisations
///   - Org users see only their own organisation
/// </summary>
public class OrganisationScopingTests : TestBase
{
    [Fact]
    public async Task DaAdmin_GetAll_ReturnsAllOrgs()
    {
        // Arrange: two orgs exist in the database
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Organisations.AddRange(
                new Organisation { Id = orgAId, Name = "Org A", CreatedAt = DateTime.UtcNow },
                new Organisation { Id = orgBId, Name = "Org B", CreatedAt = DateTime.UtcNow }
            );
            return Task.CompletedTask;
        });

        // Act: DA admin requests the full list
        var client = ClientFor(JwtHelper.ForDaAdmin());
        var response = await client.GetAsync("/api/organisations");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orgs = await response.Content.ReadFromJsonAsync<List<OrgSummary>>();
        Assert.NotNull(orgs);
        Assert.Equal(2, orgs.Count);
    }

    [Fact]
    public async Task OrgAdmin_GetAll_ReturnsOnlyOwnOrg()
    {
        // Arrange: two orgs; the caller belongs to Org A
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Organisations.AddRange(
                new Organisation { Id = orgAId, Name = "Org A", CreatedAt = DateTime.UtcNow },
                new Organisation { Id = orgBId, Name = "Org B", CreatedAt = DateTime.UtcNow }
            );
            return Task.CompletedTask;
        });

        // Act: org admin whose token encodes orgAId
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgAId));
        var response = await client.GetAsync("/api/organisations");

        // Assert: only Org A is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orgs = await response.Content.ReadFromJsonAsync<List<OrgSummary>>();
        Assert.NotNull(orgs);
        Assert.Single(orgs);
        Assert.Equal(orgAId, orgs[0].Id);
    }

    [Fact]
    public async Task OrgAdmin_GetById_OtherOrg_Returns404()
    {
        // Arrange
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Organisations.AddRange(
                new Organisation { Id = orgAId, Name = "Org A", CreatedAt = DateTime.UtcNow },
                new Organisation { Id = orgBId, Name = "Org B", CreatedAt = DateTime.UtcNow }
            );
            return Task.CompletedTask;
        });

        // Act: org A admin tries to fetch org B
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgAId));
        var response = await client.GetAsync($"/api/organisations/{orgBId}");

        // Assert: invisible to them — looks like it doesn't exist
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_GetAll_Returns401()
    {
        var response = await Client.GetAsync("/api/organisations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Minimal shape to deserialise the response — only the fields the tests need
    private record OrgSummary(Guid Id, string Name);
}
