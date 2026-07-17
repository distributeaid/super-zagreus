using System.Net;
using System.Net.Http.Json;
using DA.NA.Core.Entities;
using DA.NA.Tests.Infrastructure;
using Xunit;

namespace DA.NA.Tests.Auth;

public class MeEndpointTests : TestBase
{
    [Fact]
    public async Task Returns_caller_identity_with_org_name()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = DateTime.UtcNow });
            db.Users.Add(new User
            {
                Id = userId, Email = "hub@example.org", Username = "hub@example.org",
                OrgId = orgId, Role = UserRole.OrgAdmin, CreatedAt = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });

        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId, userId));
        var res = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var me = await res.Content.ReadFromJsonAsync<Me>();
        Assert.Equal("hub@example.org", me!.email);
        Assert.Equal("Aegean Hub", me.orgName);
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected()
    {
        var res = await Client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private record Me(Guid id, string email, string role, Guid? orgId, string? orgName);
}
