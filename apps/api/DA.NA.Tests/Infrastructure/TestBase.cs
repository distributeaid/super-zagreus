using DA.NA.Core.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DA.NA.Tests.Infrastructure;

/// <summary>
/// Base class for all integration tests.
///
/// ApiFactory owns one open SQLite connection that is shared by every DbContext
/// instance (both the API's and the test's). This means anything seeded in
/// SeedAsync is immediately visible to API requests.
///
/// IAsyncLifetime is an xUnit interface that provides async setup (InitializeAsync)
/// and teardown (DisposeAsync) — the equivalent of constructor/Dispose for async work.
/// </summary>
public abstract class TestBase : IAsyncLifetime
{
    protected readonly ApiFactory Factory;
    protected readonly HttpClient Client;

    protected TestBase()
    {
        Factory = new ApiFactory();
        Client = Factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Create all tables based on the EF Core model.
        // EnsureCreatedAsync is idempotent — safe to call once per factory instance.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
    }

    /// <summary>
    /// Seeds data into the test database.
    /// Call this at the start of a test to put the database in the state you need.
    /// </summary>
    protected async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Returns an HttpClient with the Authorization header pre-set to the given token.
    /// </summary>
    protected HttpClient ClientFor(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
