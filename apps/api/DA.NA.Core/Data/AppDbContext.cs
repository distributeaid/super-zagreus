using DA.NA.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DA.NA.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<OrgAssociation> OrgAssociations => Set<OrgAssociation>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<ItemType> ItemTypes => Set<ItemType>();
    public DbSet<NeedsAssessment> NeedsAssessments => Set<NeedsAssessment>();
    public DbSet<AssessmentItem> AssessmentItems => Set<AssessmentItem>();
    public DbSet<StalenessResponse> StalenessResponses => Set<StalenessResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Store enums as strings so the database is readable without a codebook
        modelBuilder.Entity<User>().Property(u => u.Role).HasConversion<string>();
        modelBuilder.Entity<Project>().Property(p => p.Status).HasConversion<string>();
        modelBuilder.Entity<Unit>().Property(u => u.Dimension).HasConversion<string>();
        modelBuilder.Entity<NeedsAssessment>().Property(a => a.Status).HasConversion<string>();
        modelBuilder.Entity<StalenessResponse>().Property(s => s.Response).HasConversion<string>();

        // OrgAssociation composite PK and self-referencing many-to-many
        modelBuilder.Entity<OrgAssociation>().HasKey(a => new { a.ParentOrgId, a.ChildOrgId });
        modelBuilder.Entity<OrgAssociation>()
            .HasOne(a => a.ParentOrg)
            .WithMany(o => o.ChildAssociations)
            .HasForeignKey(a => a.ParentOrgId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OrgAssociation>()
            .HasOne(a => a.ChildOrg)
            .WithMany(o => o.ParentAssociations)
            .HasForeignKey(a => a.ChildOrgId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing: assessment supersedes a previous assessment
        modelBuilder.Entity<NeedsAssessment>()
            .HasOne(a => a.Supersedes)
            .WithMany()
            .HasForeignKey(a => a.SupersedesId)
            .OnDelete(DeleteBehavior.Restrict);

        // StalenessResponse -> NewAssessment (nullable)
        modelBuilder.Entity<StalenessResponse>()
            .HasOne(s => s.NewAssessment)
            .WithMany()
            .HasForeignKey(s => s.NewAssessmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraints
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Unit>().HasIndex(u => u.Name).IsUnique();
    }
}
