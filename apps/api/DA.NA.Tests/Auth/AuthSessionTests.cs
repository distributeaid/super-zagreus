using System.Net;
using System.Net.Http.Json;
using DA.NA.Api.Auth;
using DA.NA.Core.Entities;
using DA.NA.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DA.NA.Tests.Auth;

/// <summary>Stands in for the real OIDC verifier: "valid" resolves to a verified email, anything else fails.</summary>
internal class SessionFakeVerifier : IIdTokenVerifier
{
    public Task<VerifiedIdentity?> VerifyAsync(string idToken, string provider) =>
        Task.FromResult(idToken == "valid" ? new VerifiedIdentity("hub@example.org", true) : null);
}

public class AuthSessionTests : TestBase
{
    private HttpClient AuthClient() =>
        Factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IIdTokenVerifier>();
            s.AddSingleton<IIdTokenVerifier, SessionFakeVerifier>();
        })).CreateClient();

    [Fact]
    public async Task Authorized_email_gets_a_token()
    {
        var orgId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Aegean Hub", CreatedAt = DateTime.UtcNow });
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(), Email = "hub@example.org", Username = "hub@example.org",
                OrgId = orgId, Role = UserRole.OrgAdmin, CreatedAt = DateTime.UtcNow
            });
            return Task.CompletedTask;
        });

        var res = await AuthClient().PostAsJsonAsync("/api/auth/session",
            new { idToken = "valid", provider = "google" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.token));
    }

    [Fact]
    public async Task Invalid_token_is_rejected()
    {
        var res = await AuthClient().PostAsJsonAsync("/api/auth/session",
            new { idToken = "nope", provider = "google" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Verified_but_unprovisioned_email_is_rejected()
    {
        var res = await AuthClient().PostAsJsonAsync("/api/auth/session",
            new { idToken = "valid", provider = "google" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private record SessionResponse(string token, DateTime expiresAt);
}
