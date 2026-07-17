namespace DA.NA.Core.Entities;

public class User
{
    public Guid Id { get; set; }

    // Null for DA users (DaAdmin, DaMember) who don't belong to a partner org
    public Guid? OrgId { get; set; }
    public Organisation? Org { get; set; }

    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}
