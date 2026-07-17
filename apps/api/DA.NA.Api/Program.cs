using DA.NA.Core.Data;
using DA.NA.Core.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("""
        -------------------------------------------------------------------------
        STARTUP FAILED: Jwt:Key is missing or too short (minimum 32 characters).
        -------------------------------------------------------------------------
        The JWT signing key is not stored in appsettings.json to prevent it from
        being accidentally committed to source control.

        To fix this, set the key using one of the following methods:

        Option 1 — dotnet user-secrets (recommended for local development):
          cd DA.NA.Api
          dotnet user-secrets set "Jwt:Key" "your-long-random-secret-here"

        Option 2 — environment variable:
          Windows (PowerShell): $env:JWT__KEY = "your-long-random-secret-here"
          Mac/Linux:            export JWT__KEY="your-long-random-secret-here"
          Note: use double underscore (__) to represent the colon (:) in the key name.

        Option 3 — appsettings.Development.json (local only, do NOT commit):
          Add: "Jwt": { "Key": "your-long-random-secret-here" }

        To generate a suitable key, run:
          node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"
          -- or --
          openssl rand -hex 32
        -------------------------------------------------------------------------
        """);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DaUser", p => p.RequireRole(UserRole.DaAdmin.ToString(), UserRole.DaMember.ToString()))
    .AddPolicy("DaAdmin", p => p.RequireRole(UserRole.DaAdmin.ToString()))
    .AddPolicy("OrgAdmin", p => p.RequireRole(UserRole.OrgAdmin.ToString(), UserRole.DaAdmin.ToString()));

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DistributeAid Needs Assessment API", Version = "v1" });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Open CORS for prototype — lock this down before production
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Run any pending EF Core migrations and seed reference data on startup
// Skipped in the "Testing" environment — tests manage their own database
if (!app.Environment.IsEnvironment("Testing"))
    await SeedData.InitialiseAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DA Needs Assessment v1");
    c.RoutePrefix = string.Empty; // Swagger at root: http://localhost:5000
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Needed so the test project can reference Program to boot the API in tests
public partial class Program { }
