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
