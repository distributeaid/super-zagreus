# Vertical Slice: Auth + Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A DA-provisioned hub user signs in with Google or Microsoft and lands on a dashboard showing their project (name + region) and its real freshness status (up to date / needs updating vs a 90-day window), end-to-end through the whole stack.

**Architecture:** Monorepo. `apps/api` is the existing `zagreus-be` .NET 8 / ASP.NET Core / PostgreSQL / EF Core solution, consolidated in and extended with an OAuth session-exchange endpoint. `apps/web` is a Next.js 16 app using Auth.js for Google/Microsoft sign-in; it holds the session in an httpOnly cookie and proxies API calls (attaching the app JWT) server-side. Authentication is delegated to Google/Microsoft; authorization (org + role) stays in the .NET API.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core, Npgsql, xUnit, `Microsoft.IdentityModel.*` (OIDC token validation); Next.js 16, React 19, TypeScript, Yarn 4, Auth.js (NextAuth v5), Tailwind + Radix, Vitest + Testing Library.

## Backend reality (verified against `zagreus-be@saga`)

- **Entities** (`DA.NA.Core/Entities`): `User { Id, Guid? OrgId, Org, Username, FirstName, LastName, Email (unique), PasswordHash, UserRole Role, CreatedAt }`; `Organisation { Id, Name, ... }`; `Project { Id, OrgId, Name, string? Region, ProjectStatus Status, CreatedAt, DateTime? LastSubmittedAt }`; `NeedsAssessment { Id, ProjectId, CreatedBy, AssessmentStatus Status, Guid? SupersedesId, Notes, CreatedAt, DateTime? SubmittedAt, Items }`; `AssessmentItem { Id, AssessmentId, ItemTypeId, decimal Quantity, UnitId, Notes, CreatedAt }`.
- **Enums:** `UserRole { DaAdmin, DaMember, OrgAdmin, OrgMember }`, `AssessmentStatus { Draft, Submitted }`, `ProjectStatus { Active, Inactive }`.
- **Auth today:** `AuthController` `POST /api/auth/login` verifies a BCrypt password and issues a JWT with claims `sub = user.Id`, `role`, `orgId`; config `Jwt:Key/Issuer/Audience/ExpiryHours` (Issuer/Audience = `da-needs-assessment`, 8h). JWT bearer + policies configured in `DA.NA.Api/Program.cs`. `User.OrgId()` extension (`DA.NA.Api.Extensions`) reads the `orgId` claim; org users are scoped to their own org.
- **Reads for the dashboard already exist:** `GET /api/organisations/{orgId}/projects` → `[{ Id, Name, Region, Status, CreatedAt, LastSubmittedAt, AssessmentCount }]` (org-scoped); `GET /api/projects/{projectId}/assessments/current` → latest **Submitted** assessment incl. `SubmittedAt`, or `404` if none.
- **Freshness mapping:** the needs list's "last confirmed" = the current submitted assessment's `SubmittedAt`. "Needs updating" = no submitted assessment, or `UtcNow - SubmittedAt > 90 days`. No schema change required for the slice.
- **Startup:** `Program.cs` runs EF migrations + `SeedData.InitialiseAsync` (seeds units, item types, and a DA admin `admin` / `ChangeMe123!`), skipped when `ASPNETCORE_ENVIRONMENT=Testing`.

## Global Constraints

- **Backend:** .NET 8 SDK; PostgreSQL 16 (via Docker, per the backend README); EF Core migrations via `dotnet-ef`; tests are xUnit in `DA.NA.Tests`; keep the existing `Jwt:*` config and JWT claim shape (`sub`, `role`, `orgId`).
- **Frontend:** Node 20–22; Yarn 4 (Corepack); Next.js `^16.2.2`, React `^19.2.0`, TypeScript `^5.9.3`; Auth.js (`next-auth@^5`).
- **Auth model:** authentication via Google/Microsoft OAuth/OIDC; the backend verifies the provider ID token and maps the **verified email** to a `User`; unauthorized email → `401`. No passwords for org users; DA admin password login stays for bootstrap.
- **Design tokens:** `da-blue #051E5D`, `da-lavender #DFCDE8`, `da-teal #98BEC6`, `da-green #5AC597`; Roboto body, Permanent Marker accent; spacing `da-sm/md/lg/xl` = 8/16/32/64px. No raw hex in components.
- **License:** AGPL-3.0-only. **Commits:** Conventional Commits.
- **Secrets (never commit):** `Jwt:Key`; Google/Microsoft client IDs + secrets; `AUTH_SECRET`.

---

## File Structure

**Phase A — monorepo + backend consolidation**
- `package.json`, `.yarnrc.yml`, `.gitignore`, `LICENSE` — Yarn 4 workspace root.
- `apps/api/**` — the `zagreus-be` solution, moved in wholesale.

**Phase B — backend OAuth + dashboard convenience + provisioning**
- `apps/api/DA.NA.Api/Auth/IIdTokenVerifier.cs` — provider-token verification abstraction.
- `apps/api/DA.NA.Api/Auth/OidcIdTokenVerifier.cs` — Google/Microsoft implementation.
- `apps/api/DA.NA.Api/Auth/JwtTokenFactory.cs` — app-JWT issuance (extracted from `AuthController`).
- `apps/api/DA.NA.Api/Controllers/AuthController.cs` — add `POST /api/auth/session`.
- `apps/api/DA.NA.Api/Controllers/MeController.cs` — `GET /api/me`.
- `apps/api/DA.NA.Tests/AuthSessionTests.cs`, `MeEndpointTests.cs` — xUnit.
- `apps/api/tools/DA.NA.Provision/**` — console provisioning tool.

**Phase C — frontend scaffold + auth + dashboard**
- `apps/web/**` — Next.js app: Tailwind/tokens, Auth.js (`auth.ts`, route handler, middleware), API proxy client (`src/data`), dashboard page + freshness logic + tests.

---

# Phase A — Monorepo + backend consolidation

## Task A1: Initialize the Yarn 4 workspace root

**Files:**
- Create: `package.json`, `.yarnrc.yml`, `LICENSE`
- Modify: `.gitignore`

**Interfaces:**
- Produces: a Yarn 4 workspace recognizing `apps/*`; later `yarn workspace @zagreus/web <script>` works.

- [ ] **Step 1: Pin Yarn 4 via Corepack**

Run:
```bash
cd ~/Git/super-zagreus
corepack enable
corepack prepare yarn@4.12.0 --activate
```
Expected: no error.

- [ ] **Step 2: Create `.yarnrc.yml`**

```yaml
nodeLinker: node-modules
enableGlobalCache: true
```

- [ ] **Step 3: Create the root `package.json`**

```json
{
  "name": "zagreus",
  "private": true,
  "packageManager": "yarn@4.12.0",
  "license": "AGPL-3.0-only",
  "workspaces": ["apps/web"],
  "engines": { "node": ">=20.0.0 <=22.22.3" }
}
```

- [ ] **Step 4: Add license + extend `.gitignore`**

```bash
curl -fsSL https://www.gnu.org/licenses/agpl-3.0.txt -o LICENSE
```
Append to `.gitignore`:
```
# yarn 4
.yarn/*
!.yarn/releases
.pnp.*
# next
apps/web/.next/
apps/web/out/
# dotnet
apps/api/**/bin/
apps/api/**/obj/
# testing
coverage/
.DS_Store
```

- [ ] **Step 5: Commit**

```bash
git add package.json .yarnrc.yml .gitignore LICENSE
git commit -m "chore: initialize yarn 4 monorepo workspace"
```

## Task A2: Consolidate `zagreus-be` into `apps/api`

**Files:**
- Create: `apps/api/**` (the .NET solution).

**Interfaces:**
- Produces: `apps/api/DistributeAid.NeedsAssessment.sln` builds; existing xUnit tests pass. Later backend tasks modify files under `apps/api/DA.NA.Api` and `apps/api/DA.NA.Core`.

- [ ] **Step 1: Copy the solution in (preserving history is optional for a prototype)**

```bash
cd /tmp
git clone --branch saga --depth 1 https://github.com/distributeaid/zagreus-be.git
mkdir -p ~/Git/super-zagreus/apps/api
rsync -a --exclude .git /tmp/zagreus-be/ ~/Git/super-zagreus/apps/api/
cd ~/Git/super-zagreus
```
Expected: `apps/api/DistributeAid.NeedsAssessment.sln` and the `DA.NA.*` project folders exist.

- [ ] **Step 2: Start Postgres and set the JWT dev secret**

```bash
docker run -d --name da-postgres \
  -e POSTGRES_USER=da_user -e POSTGRES_PASSWORD=da_password \
  -e POSTGRES_DB=da_needs_assessment -p 5432:5432 postgres:16
cd apps/api/DA.NA.Api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -hex 32)"
cd ../../..
```
Expected: container running; secret set.

- [ ] **Step 3: Build and test the consolidated solution**

```bash
dotnet build apps/api/DistributeAid.NeedsAssessment.sln
dotnet test apps/api/DistributeAid.NeedsAssessment.sln
```
Expected: build succeeds; existing xUnit tests pass. If a test needs the DB, ensure the container from Step 2 is running.

- [ ] **Step 4: Commit**

```bash
git add apps/api
git commit -m "chore: consolidate zagreus-be .NET solution into apps/api"
```

---

# Phase B — Backend: OAuth session + `/api/me` + provisioning

## Task B1: Extract app-JWT issuance into a factory

**Files:**
- Create: `apps/api/DA.NA.Api/Auth/JwtTokenFactory.cs`
- Modify: `apps/api/DA.NA.Api/Controllers/AuthController.cs`
- Modify: `apps/api/DA.NA.Api/Program.cs`
- Test: `apps/api/DA.NA.Tests/JwtTokenFactoryTests.cs`

**Interfaces:**
- Produces: `JwtTokenFactory.Create(User user) : (string token, DateTime expiresAt)` issuing a JWT with claims `sub`, `role`, `orgId` — identical to today's login token. Consumed by `AuthController` login and (Task B3) session.

- [ ] **Step 1: Write the failing test**

Create `apps/api/DA.NA.Tests/JwtTokenFactoryTests.cs`:
```csharp
using System.IdentityModel.Tokens.Jwt;
using DA.NA.Api.Auth;
using DA.NA.Core.Entities;
using Microsoft.Extensions.Configuration;
using Xunit;

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
    public void Create_embeds_sub_role_and_org_claims()
    {
        var user = new User { Id = Guid.NewGuid(), Role = UserRole.OrgAdmin, OrgId = Guid.NewGuid() };
        var factory = new JwtTokenFactory(Config());

        var (token, _) = factory.Create(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("OrgAdmin", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.Equal(user.OrgId.ToString(), jwt.Claims.First(c => c.Type == "orgId").Value);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test apps/api/DistributeAid.NeedsAssessment.sln --filter JwtTokenFactoryTests`
Expected: FAIL — `JwtTokenFactory` does not exist.

- [ ] **Step 3: Implement the factory**

Create `apps/api/DA.NA.Api/Auth/JwtTokenFactory.cs`:
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DA.NA.Core.Entities;
using Microsoft.IdentityModel.Tokens;

namespace DA.NA.Api.Auth;

public class JwtTokenFactory
{
    private readonly IConfiguration _config;
    public JwtTokenFactory(IConfiguration config) => _config = config;

    public (string token, DateTime expiresAt) Create(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(double.Parse(_config["Jwt:ExpiryHours"]!));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim("orgId", user.OrgId?.ToString() ?? ""),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }
}
```

> Note: the app's JWT bearer uses `role` for authorization. The existing login used `ClaimTypes.Role`; standardize on the `"role"` short claim here and update the bearer/role handling in Step 5 so login and session issue identical tokens. Keep `sub` for the user id.

- [ ] **Step 4: Register the factory and use it in login**

In `apps/api/DA.NA.Api/Program.cs`, after `builder.Services.AddControllers();` add:
```csharp
builder.Services.AddScoped<DA.NA.Api.Auth.JwtTokenFactory>();
```
In the JWT bearer options in `Program.cs`, set the role claim type so `[Authorize(Roles=...)]` keeps working:
```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = builder.Configuration["Jwt:Issuer"],
    ValidAudience = builder.Configuration["Jwt:Audience"],
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
    RoleClaimType = "role",
    NameClaimType = "sub",
};
```
In `AuthController.Login`, replace the manual token construction with:
```csharp
var factory = new DA.NA.Api.Auth.JwtTokenFactory(_config);
var (tokenString, expiry) = factory.Create(user);
return Ok(new
{
    token = tokenString,
    expiresAt = expiry,
    user = new { user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.OrgId }
});
```

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test apps/api/DistributeAid.NeedsAssessment.sln`
Expected: `JwtTokenFactoryTests` passes; existing auth tests still pass (they authenticate via the returned token).

- [ ] **Step 6: Commit**

```bash
git add apps/api
git commit -m "refactor: extract JwtTokenFactory and standardize role claim"
```

## Task B2: ID-token verifier abstraction (with a fake for tests)

**Files:**
- Create: `apps/api/DA.NA.Api/Auth/IIdTokenVerifier.cs`
- Create: `apps/api/DA.NA.Api/Auth/OidcIdTokenVerifier.cs`
- Modify: `apps/api/DA.NA.Api/Program.cs`

**Interfaces:**
- Produces: `IIdTokenVerifier.VerifyAsync(string idToken, string provider) : Task<VerifiedIdentity?>` where `VerifiedIdentity { string Email; bool EmailVerified; }`; returns `null` when the token is invalid. `provider` is `"google"` or `"microsoft"`. Consumed by the session endpoint (Task B3). The concrete `OidcIdTokenVerifier` validates signature/issuer/audience via each provider's OIDC discovery + JWKS.

- [ ] **Step 1: Define the interface and result type**

Create `apps/api/DA.NA.Api/Auth/IIdTokenVerifier.cs`:
```csharp
namespace DA.NA.Api.Auth;

public record VerifiedIdentity(string Email, bool EmailVerified);

public interface IIdTokenVerifier
{
    Task<VerifiedIdentity?> VerifyAsync(string idToken, string provider);
}
```

- [ ] **Step 2: Implement the OIDC verifier**

Create `apps/api/DA.NA.Api/Auth/OidcIdTokenVerifier.cs`:
```csharp
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DA.NA.Api.Auth;

public class OidcIdTokenVerifier : IIdTokenVerifier
{
    private static readonly Dictionary<string, string> Metadata = new()
    {
        ["google"] = "https://accounts.google.com/.well-known/openid-configuration",
        ["microsoft"] = "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration",
    };

    private readonly IConfiguration _config;
    private readonly Dictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers = new();

    public OidcIdTokenVerifier(IConfiguration config)
    {
        _config = config;
        foreach (var (provider, url) in Metadata)
            _managers[provider] = new ConfigurationManager<OpenIdConnectConfiguration>(
                url, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());
    }

    public async Task<VerifiedIdentity?> VerifyAsync(string idToken, string provider)
    {
        if (!_managers.TryGetValue(provider, out var manager)) return null;
        var audience = _config[$"OAuth:{provider}:ClientId"];
        if (string.IsNullOrWhiteSpace(audience)) return null;

        var oidc = await manager.GetConfigurationAsync();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = oidc.Issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            IssuerSigningKeys = oidc.SigningKeys,
        };

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(idToken, parameters);
        if (!result.IsValid) return null;

        var email = result.ClaimsIdentity.FindFirst("email")?.Value;
        if (string.IsNullOrWhiteSpace(email)) return null;
        var verifiedClaim = result.ClaimsIdentity.FindFirst("email_verified")?.Value;
        var verified = verifiedClaim is null || verifiedClaim.Equals("true", StringComparison.OrdinalIgnoreCase);
        return new VerifiedIdentity(email, verified);
    }
}
```

- [ ] **Step 3: Register the verifier**

In `apps/api/DA.NA.Api/Program.cs`, add near the other service registrations:
```csharp
builder.Services.AddSingleton<DA.NA.Api.Auth.IIdTokenVerifier, DA.NA.Api.Auth.OidcIdTokenVerifier>();
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build apps/api/DistributeAid.NeedsAssessment.sln`
Expected: build succeeds. (Live verification against real provider tokens happens in the Task C-series manual run; unit tests use a fake in Task B3.)

- [ ] **Step 5: Commit**

```bash
git add apps/api
git commit -m "feat: add OIDC id-token verifier for Google and Microsoft"
```

## Task B3: `POST /api/auth/session` endpoint (TDD with a fake verifier)

**Files:**
- Modify: `apps/api/DA.NA.Api/Controllers/AuthController.cs`
- Test: `apps/api/DA.NA.Tests/AuthSessionTests.cs`

**Interfaces:**
- Consumes: `IIdTokenVerifier` (Task B2), `JwtTokenFactory` (Task B1), `AppDbContext`.
- Produces: `POST /api/auth/session` body `{ idToken: string, provider: "google"|"microsoft" }` → `200 { token, expiresAt, user }` when the verified email matches an authorized `User`; `401` when the token is invalid or the email is not authorized.

- [ ] **Step 1: Write the failing test (with an in-memory API factory + fake verifier)**

Create `apps/api/DA.NA.Tests/AuthSessionTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using DA.NA.Api.Auth;
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

class FakeVerifier : IIdTokenVerifier
{
    public Task<VerifiedIdentity?> VerifyAsync(string idToken, string provider) =>
        Task.FromResult(idToken == "valid" ? new VerifiedIdentity("hub@example.org", true) : null);
}

public class AuthSessionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthSessionTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureServices(services =>
            {
                services.AddSingleton<IIdTokenVerifier, FakeVerifier>();
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("auth-session-tests"));
            });
        });
        Seed();
    }

    private void Seed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        if (!db.Users.Any(u => u.Email == "hub@example.org"))
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(), Email = "hub@example.org", Username = "hub@example.org",
                OrgId = Guid.NewGuid(), Role = UserRole.OrgAdmin, CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task Authorized_email_gets_a_token()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/session",
            new { idToken = "valid", provider = "google" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.token));
    }

    [Fact]
    public async Task Invalid_token_is_rejected()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/session",
            new { idToken = "nope", provider = "google" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Unauthorized_email_is_rejected()
    {
        // FakeVerifier always returns hub@example.org; delete the user to simulate "not provisioned"
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = db.Users.Single(x => x.Email == "hub@example.org");
            db.Users.Remove(u); db.SaveChanges();
        }
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/session",
            new { idToken = "valid", provider = "google" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private record SessionResponse(string token, DateTime expiresAt);
}
```

> The in-memory provider requires the `Microsoft.EntityFrameworkCore.InMemory` package in `DA.NA.Tests`. Add it in Step 2 if missing.

- [ ] **Step 2: Ensure the test project can use the in-memory provider**

Run:
```bash
dotnet add apps/api/DA.NA.Tests package Microsoft.EntityFrameworkCore.InMemory
```
Expected: package added.

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test apps/api/DistributeAid.NeedsAssessment.sln --filter AuthSessionTests`
Expected: FAIL — `/api/auth/session` returns 404 (endpoint missing).

- [ ] **Step 4: Implement the session endpoint**

In `apps/api/DA.NA.Api/Controllers/AuthController.cs`, inject the verifier + factory and add the action. Update the constructor and add:
```csharp
// constructor params: (AppDbContext db, IConfiguration config, IIdTokenVerifier verifier, JwtTokenFactory tokens)
// store _verifier and _tokens

/// <summary>Exchange a verified Google/Microsoft ID token for an app session JWT.</summary>
[HttpPost("session")]
public async Task<IActionResult> Session([FromBody] SessionRequest req)
{
    var identity = await _verifier.VerifyAsync(req.IdToken, req.Provider);
    if (identity is null || !identity.EmailVerified)
        return Unauthorized("Could not verify the sign-in.");

    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == identity.Email);
    if (user is null)
        return Unauthorized("This account is not authorized. Ask DistributeAid to provision access.");

    var (token, expiry) = _tokens.Create(user);
    return Ok(new
    {
        token,
        expiresAt = expiry,
        user = new { user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.OrgId }
    });
}
```
Add at the bottom of the file:
```csharp
/// <param name="IdToken">The provider ID token from the OAuth sign-in.</param>
/// <param name="Provider">"google" or "microsoft".</param>
public record SessionRequest(
    [Required(AllowEmptyStrings = false)] string IdToken,
    [Required(AllowEmptyStrings = false)] string Provider);
```

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test apps/api/DistributeAid.NeedsAssessment.sln --filter AuthSessionTests`
Expected: all 3 tests pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api
git commit -m "feat: add OAuth session-exchange endpoint POST /api/auth/session"
```

## Task B4: `GET /api/me` convenience endpoint

**Files:**
- Create: `apps/api/DA.NA.Api/Controllers/MeController.cs`
- Test: `apps/api/DA.NA.Tests/MeEndpointTests.cs`

**Interfaces:**
- Produces: `GET /api/me` (authorized) → `{ id, email, role, orgId, orgName }` for the caller, read from the `sub` claim. The frontend uses this to learn the caller's `orgId` (to fetch projects) without hard-coding it.

- [ ] **Step 1: Write the failing test**

Create `apps/api/DA.NA.Tests/MeEndpointTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DA.NA.Api.Auth;
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class MeEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private User _user = null!;

    public MeEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureServices(s =>
                s.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("me-tests")));
        });
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        var org = new Organisation { Id = Guid.NewGuid(), Name = "Aegean Hub", CreatedAt = DateTime.UtcNow };
        _user = new User { Id = Guid.NewGuid(), Email = "hub@example.org", Username = "hub@example.org",
            OrgId = org.Id, Role = UserRole.OrgAdmin, CreatedAt = DateTime.UtcNow };
        db.Organisations.Add(org); db.Users.Add(_user); db.SaveChanges();
    }

    [Fact]
    public async Task Returns_caller_identity_with_org_name()
    {
        var cfg = _factory.Services.GetRequiredService<IConfiguration>();
        var (token, _) = new JwtTokenFactory(cfg).Create(_user);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var me = await res.Content.ReadFromJsonAsync<Me>();
        Assert.Equal("hub@example.org", me!.email);
        Assert.Equal("Aegean Hub", me.orgName);
    }

    private record Me(Guid id, string email, string role, Guid? orgId, string? orgName);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test apps/api/DistributeAid.NeedsAssessment.sln --filter MeEndpointTests`
Expected: FAIL — 404 (endpoint missing).

- [ ] **Step 3: Implement the endpoint**

Create `apps/api/DA.NA.Api/Controllers/MeController.cs`:
```csharp
using DA.NA.Api.Extensions;
using DA.NA.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DA.NA.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    public MeController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var me = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id, u.Email, Role = u.Role.ToString(),
                u.OrgId, OrgName = u.Org != null ? u.Org.Name : null
            })
            .FirstOrDefaultAsync();

        return me is null ? NotFound() : Ok(me);
    }
}
```

> `User.FindFirst(ClaimTypes.NameIdentifier)` resolves the `sub` claim because `NameClaimType = "sub"` was set in Program.cs (Task B1, Step 4).

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test apps/api/DistributeAid.NeedsAssessment.sln --filter MeEndpointTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api
git commit -m "feat: add GET /api/me endpoint"
```

## Task B5: Provisioning console tool

**Files:**
- Create: `apps/api/tools/DA.NA.Provision/DA.NA.Provision.csproj`
- Create: `apps/api/tools/DA.NA.Provision/Program.cs`

**Interfaces:**
- Produces: `dotnet run --project apps/api/tools/DA.NA.Provision -- --org "Name" --region "Greece" --email you@gmail.com` creates (idempotently) an organisation, one project, and an `OrgAdmin` user authorized by email (empty password — OAuth only). This is how you get an account to sign in as.

- [ ] **Step 1: Create the console project referencing DA.NA.Core**

Create `apps/api/tools/DA.NA.Provision/DA.NA.Provision.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\DA.NA.Core\DA.NA.Core.csproj" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Implement the provisioning logic**

Create `apps/api/tools/DA.NA.Provision/Program.cs`:
```csharp
using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using Microsoft.EntityFrameworkCore;

string Arg(string name) =>
    args.SkipWhile(a => a != name).Skip(1).FirstOrDefault()
    ?? throw new ArgumentException($"Missing required argument {name}");

var orgName = Arg("--org");
var region = args.SkipWhile(a => a != "--region").Skip(1).FirstOrDefault();
var email = Arg("--email");
var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? "Host=localhost;Port=5432;Database=da_needs_assessment;Username=da_user;Password=da_password";

var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
await using var db = new AppDbContext(options);
await db.Database.MigrateAsync();

var org = await db.Organisations.FirstOrDefaultAsync(o => o.Name == orgName);
if (org is null)
{
    org = new Organisation { Id = Guid.NewGuid(), Name = orgName, CreatedAt = DateTime.UtcNow };
    db.Organisations.Add(org);
}

if (!await db.Projects.AnyAsync(p => p.OrgId == org.Id))
    db.Projects.Add(new Project
    {
        Id = Guid.NewGuid(), OrgId = org.Id, Name = $"{orgName} — Main",
        Region = region, Status = ProjectStatus.Active, CreatedAt = DateTime.UtcNow
    });

if (!await db.Users.AnyAsync(u => u.Email == email))
    db.Users.Add(new User
    {
        Id = Guid.NewGuid(), Email = email, Username = email,
        FirstName = "", LastName = "", PasswordHash = "",
        OrgId = org.Id, Role = UserRole.OrgAdmin, CreatedAt = DateTime.UtcNow
    });

await db.SaveChangesAsync();
Console.WriteLine($"Provisioned org '{orgName}' with a project and authorized {email} as OrgAdmin.");
```

- [ ] **Step 3: Verify it provisions**

Run (Postgres from Task A2 running):
```bash
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=da_needs_assessment;Username=da_user;Password=da_password"
dotnet run --project apps/api/tools/DA.NA.Provision -- --org "Aegean Hub" --region "Greece" --email your.name@gmail.com
```
Expected: prints the "Provisioned…" line. Re-running is idempotent (no duplicate org/project/user).

- [ ] **Step 4: Commit**

```bash
git add apps/api/tools
git commit -m "feat: add provisioning console tool for OAuth hub users"
```

---

# Phase C — Frontend: scaffold + Google/Microsoft auth + dashboard

## Task C1: Scaffold `apps/web` with Tailwind + DA tokens

**Files:**
- Create: `apps/web/package.json`, `next.config.ts`, `tsconfig.json`, `postcss.config.mjs`, `tailwind.config.ts`
- Create: `apps/web/src/app/layout.tsx`, `src/app/page.tsx`, `src/app/globals.css`, `src/lib/fonts.ts`

**Interfaces:**
- Produces: a booting Next.js app with DA tokens (`bg-da-blue`, `text-da-blue`, `p-da-md`, `font-marker`) and Roboto/Permanent Marker fonts.

- [ ] **Step 1: Create the web manifest**

Create `apps/web/package.json`:
```json
{
  "name": "@zagreus/web",
  "version": "0.1.0",
  "private": true,
  "license": "AGPL-3.0-only",
  "scripts": {
    "dev": "next dev",
    "build": "next build",
    "start": "next start",
    "typecheck": "tsc --noEmit",
    "test": "vitest run"
  },
  "dependencies": {
    "next": "^16.2.2",
    "react": "^19.2.0",
    "react-dom": "^19.2.0"
  },
  "devDependencies": {
    "@types/node": "^22.19.0",
    "@types/react": "^19.2.0",
    "@types/react-dom": "^19.2.0",
    "typescript": "^5.9.3",
    "tailwindcss": "^3.4.19",
    "postcss": "^8.4.47",
    "autoprefixer": "^10.4.20"
  }
}
```

- [ ] **Step 2: Config files**

Create `apps/web/tsconfig.json`:
```json
{
  "compilerOptions": {
    "target": "ES2022", "lib": ["dom", "dom.iterable", "ES2022"],
    "strict": true, "noEmit": true, "esModuleInterop": true,
    "module": "esnext", "moduleResolution": "bundler",
    "resolveJsonModule": true, "isolatedModules": true, "jsx": "preserve",
    "incremental": true, "skipLibCheck": true,
    "plugins": [{ "name": "next" }], "paths": { "@/*": ["./src/*"] }
  },
  "include": ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts"],
  "exclude": ["node_modules"]
}
```
Create `apps/web/next.config.ts`:
```ts
import type { NextConfig } from "next";
const nextConfig: NextConfig = { reactStrictMode: true };
export default nextConfig;
```
Create `apps/web/postcss.config.mjs`:
```js
export default { plugins: { tailwindcss: {}, autoprefixer: {} } };
```
Create `apps/web/tailwind.config.ts`:
```ts
import type { Config } from "tailwindcss";
const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    container: { center: true, padding: { DEFAULT: "16px", md: "24px", lg: "32px" } },
    extend: {
      colors: { "da-blue": "#051E5D", "da-lavender": "#DFCDE8", "da-teal": "#98BEC6", "da-green": "#5AC597" },
      spacing: { "da-sm": "8px", "da-md": "16px", "da-lg": "32px", "da-xl": "64px" },
      fontFamily: {
        sans: ["var(--font-roboto)", "system-ui", "sans-serif"],
        marker: ["var(--font-permanent-marker)", "cursive"],
      },
    },
  },
  plugins: [],
};
export default config;
```

- [ ] **Step 3: Fonts, layout, styles, placeholder page**

Create `apps/web/src/lib/fonts.ts`:
```ts
import { Roboto, Permanent_Marker } from "next/font/google";
export const roboto = Roboto({ subsets: ["latin"], weight: ["400", "500", "700"], variable: "--font-roboto", display: "swap" });
export const permanentMarker = Permanent_Marker({ subsets: ["latin"], weight: "400", variable: "--font-permanent-marker", display: "swap" });
```
Create `apps/web/src/app/globals.css`:
```css
@tailwind base;
@tailwind components;
@tailwind utilities;
html, body { margin: 0; padding: 0; }
```
Create `apps/web/src/app/layout.tsx`:
```tsx
import type { Metadata } from "next";
import { roboto, permanentMarker } from "@/lib/fonts";
import "./globals.css";

export const metadata: Metadata = { title: "Zagreus", description: "DistributeAid partner portal" };

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${roboto.variable} ${permanentMarker.variable}`}>
      <body className="font-sans text-da-blue">{children}</body>
    </html>
  );
}
```
Create `apps/web/src/app/page.tsx`:
```tsx
import { redirect } from "next/navigation";
export default function Index() {
  redirect("/dashboard");
}
```

- [ ] **Step 4: Install and build**

```bash
yarn install
yarn workspace @zagreus/web build
```
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add apps/web package.json yarn.lock
git commit -m "feat: scaffold apps/web with Tailwind and DA tokens"
```

## Task C2: Auth.js with Google + Microsoft, backed by the API session exchange

**Files:**
- Create: `apps/web/src/auth.ts`
- Create: `apps/web/src/app/api/auth/[...nextauth]/route.ts`
- Create: `apps/web/src/app/login/page.tsx`
- Create: `apps/web/middleware.ts`
- Create: `apps/web/.env.local.example`
- Modify: `apps/web/package.json` (add `next-auth`)

**Interfaces:**
- Produces: Google/Microsoft sign-in. On sign-in, Auth.js calls the API `POST /api/auth/session` with the provider `id_token` and stores the returned **app JWT** in the session (httpOnly cookie). `auth()` returns the session; `middleware.ts` guards `/dashboard`. Exposes `getApiToken()` for the proxy (Task C3).

- [ ] **Step 1: Add Auth.js**

```bash
yarn workspace @zagreus/web add next-auth@^5.0.0-beta.25
```
Expected: `next-auth` v5 added.

- [ ] **Step 2: Configure Auth.js**

Create `apps/web/src/auth.ts`:
```ts
import NextAuth from "next-auth";
import Google from "next-auth/providers/google";
import MicrosoftEntraId from "next-auth/providers/microsoft-entra-id";

const API_BASE = process.env.API_BASE_URL!;

export const { handlers, auth, signIn, signOut } = NextAuth({
  providers: [Google, MicrosoftEntraId],
  callbacks: {
    async jwt({ token, account }) {
      // On initial sign-in, exchange the provider id_token for an app JWT.
      if (account?.id_token) {
        const provider = account.provider === "google" ? "google" : "microsoft";
        const res = await fetch(`${API_BASE}/api/auth/session`, {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ idToken: account.id_token, provider }),
        });
        if (!res.ok) {
          token.apiError = true;
          return token;
        }
        const data = (await res.json()) as { token: string; expiresAt: string };
        token.apiToken = data.token;
        token.apiExpiresAt = data.expiresAt;
      }
      return token;
    },
    async session({ session, token }) {
      session.apiToken = token.apiToken as string | undefined;
      session.apiError = token.apiError as boolean | undefined;
      return session;
    },
  },
});
```
Create `apps/web/src/app/api/auth/[...nextauth]/route.ts`:
```ts
import { handlers } from "@/auth";
export const { GET, POST } = handlers;
```
Create a types shim `apps/web/src/types/next-auth.d.ts`:
```ts
import "next-auth";
declare module "next-auth" {
  interface Session {
    apiToken?: string;
    apiError?: boolean;
  }
}
```

- [ ] **Step 3: Login page + route guard**

Create `apps/web/src/app/login/page.tsx`:
```tsx
import { signIn } from "@/auth";
import { Button } from "@/components/ui/Button";

export default function LoginPage() {
  return (
    <main className="container flex min-h-screen flex-col items-center justify-center gap-da-lg">
      <h1 className="font-marker text-4xl text-da-blue">Zagreus</h1>
      <form action={async () => { "use server"; await signIn("google", { redirectTo: "/dashboard" }); }}>
        <Button>Sign in with Google</Button>
      </form>
      <form action={async () => { "use server"; await signIn("microsoft-entra-id", { redirectTo: "/dashboard" }); }}>
        <Button variant="secondary">Sign in with Microsoft</Button>
      </form>
    </main>
  );
}
```
Create `apps/web/middleware.ts`:
```ts
export { auth as middleware } from "@/auth";
export const config = { matcher: ["/dashboard/:path*"] };
```

- [ ] **Step 4: Env example**

Create `apps/web/.env.local.example`:
```
API_BASE_URL=http://localhost:5000
AUTH_SECRET=generate-with: openssl rand -base64 33
AUTH_GOOGLE_ID=
AUTH_GOOGLE_SECRET=
AUTH_MICROSOFT_ENTRA_ID_ID=
AUTH_MICROSOFT_ENTRA_ID_SECRET=
AUTH_MICROSOFT_ENTRA_ID_ISSUER=https://login.microsoftonline.com/common/v2.0
```

- [ ] **Step 5: Verify build (no live sign-in yet)**

```bash
yarn workspace @zagreus/web build
```
Expected: build succeeds. (Live sign-in is exercised in Task C5 after you register OAuth apps.)

- [ ] **Step 6: Commit**

```bash
git add apps/web
git commit -m "feat: add Google/Microsoft auth via Auth.js with API session exchange"
```

## Task C3: Server-side API client (proxy) + Button + Button test

**Files:**
- Create: `apps/web/src/data/apiClient.ts`
- Create: `apps/web/src/components/ui/Button.tsx`
- Create: `apps/web/src/components/ui/Button.test.tsx`
- Create: `apps/web/vitest.config.ts`, `vitest.setup.ts`
- Modify: `apps/web/package.json` (test deps)

**Interfaces:**
- Produces: `apiGet<T>(path: string): Promise<T>` — a server-only fetch helper that reads the app JWT from the Auth.js session and attaches `Authorization: Bearer`. `Button` primitive (`variant?: "primary"|"secondary"`). Consumed by the dashboard (Task C4).

- [ ] **Step 1: Add test tooling**

```bash
yarn workspace @zagreus/web add -D vitest@^2.1.8 @vitejs/plugin-react@^4.3.4 jsdom@^25.0.1 vite-tsconfig-paths@^5.1.4 @testing-library/react@^16.1.0 @testing-library/jest-dom@^6.6.3 @testing-library/user-event@^14.5.2
```

- [ ] **Step 2: Vitest config + setup**

Create `apps/web/vitest.config.ts`:
```ts
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import tsconfigPaths from "vite-tsconfig-paths";
export default defineConfig({
  plugins: [react(), tsconfigPaths()],
  test: { environment: "jsdom", globals: true, setupFiles: ["./vitest.setup.ts"], include: ["src/**/*.test.{ts,tsx}"] },
});
```
Create `apps/web/vitest.setup.ts`:
```ts
import "@testing-library/jest-dom/vitest";
```

- [ ] **Step 3: Write the failing Button test**

Create `apps/web/src/components/ui/Button.test.tsx`:
```tsx
import { render, screen } from "@testing-library/react";
import { Button } from "./Button";

function setup(ui: React.ReactNode) { return render(ui); }

test("renders its label with DA primary tokens", () => {
  setup(<Button>Go</Button>);
  const b = screen.getByRole("button", { name: "Go" });
  expect(b).toHaveClass("bg-da-blue");
  expect(b).toHaveClass("text-white");
});

test("uses secondary tokens when asked", () => {
  setup(<Button variant="secondary">Alt</Button>);
  expect(screen.getByRole("button", { name: "Alt" })).toHaveClass("bg-da-lavender");
});
```

- [ ] **Step 4: Run to verify it fails**

Run: `yarn workspace @zagreus/web test src/components/ui/Button.test.tsx`
Expected: FAIL — cannot resolve `./Button`.

- [ ] **Step 5: Implement Button and the API client**

Create `apps/web/src/components/ui/Button.tsx`:
```tsx
import type { ButtonHTMLAttributes, ReactNode } from "react";
const VARIANTS = { primary: "bg-da-blue text-white", secondary: "bg-da-lavender text-da-blue" } as const;
export function Button({ children, variant = "primary", className = "", ...rest }:
  { children: ReactNode; variant?: keyof typeof VARIANTS } & ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button className={`rounded px-da-md py-da-sm font-sans font-medium ${VARIANTS[variant]} ${className}`} {...rest}>
      {children}
    </button>
  );
}
```
Create `apps/web/src/data/apiClient.ts`:
```ts
import "server-only";
import { auth } from "@/auth";

const API_BASE = process.env.API_BASE_URL!;

export async function apiGet<T>(path: string): Promise<T> {
  const session = await auth();
  const token = session?.apiToken;
  if (!token) throw new Error("Not authenticated");
  const res = await fetch(`${API_BASE}${path}`, {
    headers: { authorization: `Bearer ${token}` },
    cache: "no-store",
  });
  if (res.status === 404) return null as T;
  if (!res.ok) throw new Error(`API ${path} failed: ${res.status}`);
  return (await res.json()) as T;
}
```

- [ ] **Step 6: Run to verify pass**

Run: `yarn workspace @zagreus/web test src/components/ui/Button.test.tsx`
Expected: both tests pass.

- [ ] **Step 7: Commit**

```bash
git add apps/web
git commit -m "feat: add server-side API client and Button primitive"
```

## Task C4: Freshness helper + dashboard page

**Files:**
- Create: `apps/web/src/data/freshness.ts`
- Create: `apps/web/src/data/freshness.test.ts`
- Create: `apps/web/src/data/dashboard.ts`
- Create: `apps/web/src/components/ProjectCard.tsx`
- Create: `apps/web/src/app/dashboard/page.tsx`

**Interfaces:**
- Consumes: `apiGet` (Task C3), `Button` (Task C3), `GET /api/me`, `GET /api/organisations/{orgId}/projects`, `GET /api/projects/{projectId}/assessments/current`.
- Produces: `freshnessStatus(lastConfirmedAt: string | null, now?: Date): "fresh" | "stale"` (stale when null or > 90 days). `loadDashboard()` returns `{ orgName, project, lastConfirmedAt }`. The dashboard page renders the project card + status badge + CTA.

- [ ] **Step 1: Write the failing freshness test**

Create `apps/web/src/data/freshness.test.ts`:
```ts
import { freshnessStatus, STALE_AFTER_DAYS } from "./freshness";

const NOW = new Date("2026-07-16T00:00:00Z");

test("never-confirmed lists are stale", () => {
  expect(freshnessStatus(null, NOW)).toBe("stale");
});

test("recently confirmed lists are fresh", () => {
  const recent = new Date("2026-06-01T00:00:00Z").toISOString();
  expect(freshnessStatus(recent, NOW)).toBe("fresh");
});

test("lists confirmed more than 90 days ago are stale", () => {
  const old = new Date("2026-04-01T00:00:00Z").toISOString(); // > 90 days before NOW
  expect(freshnessStatus(old, NOW)).toBe("stale");
});

test("the window is 90 days", () => {
  expect(STALE_AFTER_DAYS).toBe(90);
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `yarn workspace @zagreus/web test src/data/freshness.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement the freshness helper**

Create `apps/web/src/data/freshness.ts`:
```ts
export const STALE_AFTER_DAYS = 90;

export function freshnessStatus(
  lastConfirmedAt: string | null,
  now: Date = new Date(),
): "fresh" | "stale" {
  if (!lastConfirmedAt) return "stale";
  const confirmed = new Date(lastConfirmedAt).getTime();
  const ageDays = (now.getTime() - confirmed) / (1000 * 60 * 60 * 24);
  return ageDays > STALE_AFTER_DAYS ? "stale" : "fresh";
}
```

- [ ] **Step 4: Run to verify pass**

Run: `yarn workspace @zagreus/web test src/data/freshness.test.ts`
Expected: all 4 tests pass.

- [ ] **Step 5: Implement the dashboard data loader**

Create `apps/web/src/data/dashboard.ts`:
```ts
import { apiGet } from "./apiClient";

type Me = { id: string; email: string; role: string; orgId: string | null; orgName: string | null };
type Project = { id: string; name: string; region: string | null };
type CurrentAssessment = { submittedAt: string } | null;

export type Dashboard = {
  orgName: string | null;
  project: Project | null;
  lastConfirmedAt: string | null;
};

export async function loadDashboard(): Promise<Dashboard> {
  const me = await apiGet<Me>("/api/me");
  if (!me?.orgId) return { orgName: me?.orgName ?? null, project: null, lastConfirmedAt: null };

  const projects = await apiGet<Project[]>(`/api/organisations/${me.orgId}/projects`);
  const project = projects?.[0] ?? null;
  if (!project) return { orgName: me.orgName, project: null, lastConfirmedAt: null };

  const current = await apiGet<CurrentAssessment>(`/api/projects/${project.id}/assessments/current`);
  return { orgName: me.orgName, project, lastConfirmedAt: current?.submittedAt ?? null };
}
```

- [ ] **Step 6: Project card + dashboard page**

Create `apps/web/src/components/ProjectCard.tsx`:
```tsx
import { freshnessStatus } from "@/data/freshness";
import { Button } from "@/components/ui/Button";

export function ProjectCard({
  name, region, lastConfirmedAt,
}: { name: string; region: string | null; lastConfirmedAt: string | null }) {
  const status = freshnessStatus(lastConfirmedAt);
  const badge = status === "fresh"
    ? { text: "Up to date", className: "bg-da-green text-da-blue" }
    : { text: "Needs updating", className: "bg-da-lavender text-da-blue" };
  return (
    <section className="rounded border border-da-teal p-da-lg">
      <h2 className="text-xl font-medium">{name}</h2>
      {region && <p className="text-da-blue">{region}</p>}
      <p className={`mt-da-sm inline-block rounded px-da-sm py-da-sm ${badge.className}`}>{badge.text}</p>
      <p className="mt-da-sm text-sm">
        {lastConfirmedAt
          ? `Last confirmed ${new Date(lastConfirmedAt).toLocaleDateString()}`
          : "Not yet confirmed"}
      </p>
      <div className="mt-da-md">
        <Button>Review &amp; confirm needs</Button>
      </div>
    </section>
  );
}
```
Create `apps/web/src/app/dashboard/page.tsx`:
```tsx
import { loadDashboard } from "@/data/dashboard";
import { ProjectCard } from "@/components/ProjectCard";

export default async function DashboardPage() {
  const { orgName, project, lastConfirmedAt } = await loadDashboard();
  return (
    <main className="container py-da-xl">
      <h1 className="mb-da-lg font-marker text-3xl">{orgName ?? "Your dashboard"}</h1>
      {project ? (
        <ProjectCard name={project.name} region={project.region} lastConfirmedAt={lastConfirmedAt} />
      ) : (
        <p>No project has been set up for your organization yet.</p>
      )}
    </main>
  );
}
```

- [ ] **Step 7: Commit**

```bash
git add apps/web
git commit -m "feat: add dashboard with project card and 90-day freshness badge"
```

## Task C5: ProjectCard test + end-to-end manual verification

**Files:**
- Create: `apps/web/src/components/ProjectCard.test.tsx`

**Interfaces:**
- Consumes: `ProjectCard` (Task C4).
- Produces: coverage of the stale/fresh badge rendering; plus the documented manual E2E run.

- [ ] **Step 1: Write the failing ProjectCard test**

Create `apps/web/src/components/ProjectCard.test.tsx`:
```tsx
import { render, screen } from "@testing-library/react";
import { ProjectCard } from "./ProjectCard";

function setup(props: { name: string; region: string | null; lastConfirmedAt: string | null }) {
  return render(<ProjectCard {...props} />);
}

test("shows 'Needs updating' when never confirmed", () => {
  setup({ name: "Aegean Hub", region: "Greece", lastConfirmedAt: null });
  expect(screen.getByText("Needs updating")).toBeInTheDocument();
  expect(screen.getByText("Not yet confirmed")).toBeInTheDocument();
});

test("shows 'Up to date' when recently confirmed", () => {
  const recent = new Date(Date.now() - 5 * 24 * 3600 * 1000).toISOString();
  setup({ name: "Aegean Hub", region: "Greece", lastConfirmedAt: recent });
  expect(screen.getByText("Up to date")).toBeInTheDocument();
});

test("renders the review CTA", () => {
  setup({ name: "Aegean Hub", region: null, lastConfirmedAt: null });
  expect(screen.getByRole("button", { name: /review & confirm needs/i })).toBeInTheDocument();
});
```

- [ ] **Step 2: Run to verify pass (implementation already exists)**

Run: `yarn workspace @zagreus/web test src/components/ProjectCard.test.tsx`
Expected: all 3 pass (ProjectCard was built in Task C4). If the CTA query fails on `&`, confirm the accessible name matches `Review & confirm needs`.

- [ ] **Step 3: Register OAuth apps (you do this — required for live sign-in)**

- Google: Google Cloud Console → Credentials → OAuth client (Web). Authorized redirect URI: `http://localhost:3000/api/auth/callback/google`. Copy client ID/secret → `AUTH_GOOGLE_ID` / `AUTH_GOOGLE_SECRET`, and set `OAuth:google:ClientId` for the API (`dotnet user-secrets set "OAuth:google:ClientId" "<client-id>"` in `DA.NA.Api`).
- Microsoft: Entra admin center → App registrations → new registration. Redirect URI: `http://localhost:3000/api/auth/callback/microsoft-entra-id`. Copy values → `AUTH_MICROSOFT_ENTRA_ID_ID` / `_SECRET`, and `dotnet user-secrets set "OAuth:microsoft:ClientId" "<client-id>"`.
- In `apps/web/.env.local`, fill the values from `.env.local.example` and set `AUTH_SECRET` (`openssl rand -base64 33`).

- [ ] **Step 4: Run the whole slice locally and verify end-to-end**

```bash
# Terminal 1 — API (Postgres from Task A2 running)
cd apps/api/DA.NA.Api && dotnet run
# Terminal 2 — provision yourself (once)
dotnet run --project apps/api/tools/DA.NA.Provision -- --org "Aegean Hub" --region "Greece" --email <your-google-or-ms-email>
# Terminal 3 — web
yarn workspace @zagreus/web dev
```
Then in a browser: open `http://localhost:3000` → redirected to `/login` (via middleware) → sign in with the provider matching your provisioned email → land on `/dashboard` showing "Aegean Hub", the project card, "Needs updating" (no submitted assessment yet), and the "Review & confirm needs" CTA.
Expected: the full path works. Signing in with a non-provisioned email is rejected at the session exchange (you stay signed out with an error).

- [ ] **Step 5: Run all tests + build (verification gate)**

```bash
dotnet test apps/api/DistributeAid.NeedsAssessment.sln
yarn workspace @zagreus/web test
yarn workspace @zagreus/web build
```
Expected: all green.

- [ ] **Step 6: Commit**

```bash
git add apps/web
git commit -m "test: cover ProjectCard freshness badge and finalize auth+dashboard slice"
```

---

## Self-Review

**Spec coverage:**
- Monorepo `apps/web` + `apps/api` → Tasks A1, A2, C1. (`apps/api` is the real .NET solution, per §2.1.)
- OAuth Google/Microsoft, backend verifies provider token → email→User → app JWT; unauthorized email denied → Tasks B2, B3, C2. Matches technical spec §7.
- httpOnly-cookie session + server-side proxy attaching Bearer → Task C2 (Auth.js session cookie) + C3 (`apiGet`). Route guard → C2 middleware.
- Dashboard: project card + freshness badge + CTA; 90-day window; list-level freshness from the current submitted assessment's `SubmittedAt` → Tasks C4, C5. Matches PRD §5.5 and the reconciled list-level model.
- Provisioning script (authorize by email, no password) → Task B5. Matches PRD §3 / technical spec §9.
- Design tokens (colors, fonts, spacing) → Task C1. Testing (xUnit + Vitest/Testing-Library) → throughout.

**Deferred by design (out of this slice, per the plan's scope):** needs editor, catalog, add/edit/confirm writes, missing-item request, reporting/CSV, multi-project UI, automated notifications, and full path-scoped CI (added in a later plan alongside the broader frontend).

**Placeholder scan:** No TBD/TODO; every code step has full contents; each command states its expected result. The one genuinely external step (OAuth app registration, C5 Step 3) is explicitly the user's action, with exact redirect URIs and secret names.

**Type/name consistency:** `JwtTokenFactory.Create(User)` (B1) is reused in B3/B4 tests. `IIdTokenVerifier.VerifyAsync(idToken, provider)` (B2) matches the fake and the session endpoint (B3). `apiGet<T>` (C3) is used by `loadDashboard` (C4). `freshnessStatus`/`STALE_AFTER_DAYS` (C4) are used by `ProjectCard` and its test (C4/C5). Provider strings `"google"`/`"microsoft"` are consistent between Auth.js (C2), the verifier (B2), and its config keys `OAuth:{provider}:ClientId`.
