using System.Net;
using System.Net.Http.Json;
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using DA.NA.Tests.Infrastructure;
using Xunit;

namespace DA.NA.Tests.Organisations;

/// <summary>
/// Tests that the org-scoping rule is enforced on GET /api/organisations/{orgId}/projects:
///   - Org users can only request projects for their own organisation (other orgs look like 404)
///   - Org users see their own organisation's projects
/// </summary>
public class ProjectScopingTests : TestBase
{
    [Fact]
    public async Task OrgAdmin_GetProjects_OtherOrg_Returns404()
    {
        // Arrange: two orgs, each with a project; the caller belongs to Org A
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Organisations.AddRange(
                new Organisation { Id = orgAId, Name = "Org A", CreatedAt = DateTime.UtcNow },
                new Organisation { Id = orgBId, Name = "Org B", CreatedAt = DateTime.UtcNow }
            );
            db.Projects.AddRange(
                new Project { Id = Guid.NewGuid(), OrgId = orgAId, Name = "Org A Project", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow },
                new Project { Id = Guid.NewGuid(), OrgId = orgBId, Name = "Org B Project", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow }
            );
            return Task.CompletedTask;
        });

        // Act: org A admin tries to fetch org B's projects
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgAId));
        var response = await client.GetAsync($"/api/organisations/{orgBId}/projects");

        // Assert: invisible to them — looks like it doesn't exist
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrgAdmin_GetProjects_OwnOrg_ReturnsOnlyOwnOrgProjects()
    {
        // Arrange: two orgs, each with a project; the caller belongs to Org A
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Organisations.AddRange(
                new Organisation { Id = orgAId, Name = "Org A", CreatedAt = DateTime.UtcNow },
                new Organisation { Id = orgBId, Name = "Org B", CreatedAt = DateTime.UtcNow }
            );
            db.Projects.AddRange(
                new Project { Id = Guid.NewGuid(), OrgId = orgAId, Name = "Org A Project", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow },
                new Project { Id = Guid.NewGuid(), OrgId = orgBId, Name = "Org B Project", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow }
            );
            return Task.CompletedTask;
        });

        // Act: org A admin requests org A's own projects
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgAId));
        var response = await client.GetAsync($"/api/organisations/{orgAId}/projects");

        // Assert: only Org A's project is returned, proving the 404 above is about
        // scoping and not a broken endpoint
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var projects = await response.Content.ReadFromJsonAsync<List<ProjectSummary>>();
        Assert.NotNull(projects);
        Assert.Single(projects);
        Assert.Equal("Org A Project", projects[0].Name);
        Assert.DoesNotContain(projects, p => p.Name == "Org B Project");
    }

    [Fact]
    public async Task DaAdmin_GetProjects_AnyOrg_ReturnsThatOrgsProjects()
    {
        // Arrange: two orgs, each with a project
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();

        await SeedAsync(db =>
        {
            db.Organisations.AddRange(
                new Organisation { Id = orgAId, Name = "Org A", CreatedAt = DateTime.UtcNow },
                new Organisation { Id = orgBId, Name = "Org B", CreatedAt = DateTime.UtcNow }
            );
            db.Projects.AddRange(
                new Project { Id = Guid.NewGuid(), OrgId = orgAId, Name = "Org A Project", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow },
                new Project { Id = Guid.NewGuid(), OrgId = orgBId, Name = "Org B Project", Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow }
            );
            return Task.CompletedTask;
        });

        // Act: DA admin (null orgId claim) requests Org B's projects
        var client = ClientFor(JwtHelper.ForDaAdmin());
        var response = await client.GetAsync($"/api/organisations/{orgBId}/projects");

        // Assert: DA users are not org-scoped, so they can read any org's projects
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var projects = await response.Content.ReadFromJsonAsync<List<ProjectSummary>>();
        Assert.NotNull(projects);
        Assert.Single(projects);
        Assert.Equal("Org B Project", projects[0].Name);
    }

    // Minimal shape to deserialise the response — only the fields the tests need
    private record ProjectSummary(Guid Id, string Name);
}
