using System.IdentityModel.Tokens.Jwt;
using DA.NA.Api.Auth;
using DA.NA.Core.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DA.NA.Tests.Auth;

public class JwtTokenFactoryTests
{
    private static IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-that-is-at-least-32-characters",
            ["Jwt:Issuer"] = "da-needs-assessment",
            ["Jwt:Audience"] = "da-needs-assessment",
            ["Jwt:ExpiryHours"] = "8",
        }).Build();

    [Fact]
    public void Create_embeds_user_role_and_org_in_the_token()
    {
        var user = new User { Id = Guid.NewGuid(), Role = UserRole.OrgAdmin, OrgId = Guid.NewGuid() };
        var factory = new JwtTokenFactory(Config());

        var (token, expiresAt) = factory.Create(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var values = jwt.Claims.Select(c => c.Value).ToList();

        Assert.Contains(user.Id.ToString(), values);      // sub
        Assert.Contains("OrgAdmin", values);              // role
        Assert.Contains(user.OrgId.ToString(), values);   // orgId
        Assert.Equal("da-needs-assessment", jwt.Issuer);
        Assert.True(expiresAt > DateTime.UtcNow);
    }
}
