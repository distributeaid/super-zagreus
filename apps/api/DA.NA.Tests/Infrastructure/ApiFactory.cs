using DA.NA.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DA.NA.Tests.Infrastructure;

/// <summary>
/// Boots the real API in memory for integration tests.
/// Swaps the PostgreSQL database for a single shared SQLite in-memory connection.
///
/// Why a shared connection? SQLite in-memory databases are tied to a connection —
/// each new connection sees an empty database. If we just pass "DataSource=:memory:"
/// every DbContext would open its own connection and see nothing seeded by tests.
/// By owning one open connection here and handing it to every DbContext, they all
/// share the same in-memory database for the lifetime of this factory instance.
///
/// The factory sets the environment to "Testing" so Program.cs skips SeedData —
/// tests seed exactly what they need themselves.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // Must be at least 32 chars and must match what JwtHelper uses to sign test tokens
    public const string TestJwtKey = "test-only-not-for-production-key-abc123!!";

    // One connection, kept open for the factory's lifetime
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        // Set before WebApplicationFactory runs Program.cs — builder.Configuration reads
        // env vars at startup, before ConfigureWebHost can inject in-memory values.
        // JWT__KEY uses double-underscore which .NET maps to Jwt:Key in the config hierarchy.
        Environment.SetEnvironmentVariable("JWT__KEY", TestJwtKey);
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the real PostgreSQL registration that Program.cs added
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // All DbContext instances share _connection → same in-memory database
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }
}
