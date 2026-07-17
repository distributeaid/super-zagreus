using DA.NA.Core.Entities;
using DA.NA.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DA.NA.Api.Controllers;

[ApiController]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    // ── DA-level endpoints ────────────────────────────────────────────────────

    /// <summary>List all users across all organisations.</summary>
    [Authorize(Policy = "DaUser")]
    [HttpGet("api/users")]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .Select(u => new
            {
                u.Id, u.Username, u.FirstName, u.LastName, u.Email, u.Role,
                u.OrgId, u.CreatedAt
            })
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();
        return Ok(users);
    }

    /// <summary>Get a single user by ID.</summary>
    [Authorize(Policy = "DaUser")]
    [HttpGet("api/users/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _db.Users
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id, u.Username, u.FirstName, u.LastName, u.Email, u.Role,
                u.OrgId, u.CreatedAt,
                Org = u.Org == null ? null : new { u.Org.Id, u.Org.Name }
            })
            .FirstOrDefaultAsync();

        if (user is null) return NotFound();
        return Ok(user);
    }

    /// <summary>List all users belonging to a specific organisation.</summary>
    [HttpGet("api/organisations/{orgId:guid}/users")]
    public async Task<IActionResult> GetByOrg(Guid orgId)
    {
        var orgExists = await _db.Organisations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return NotFound("Organisation not found");

        var users = await _db.Users
            .Where(u => u.OrgId == orgId)
            .Select(u => new
            {
                u.Id, u.Username, u.FirstName, u.LastName, u.Email, u.Role, u.CreatedAt
            })
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Create a DA user (DaAdmin or DaMember). DA users have no org.
    /// Use POST /api/organisations/{orgId}/users to create an org user.
    /// </summary>
    [Authorize(Policy = "DaAdmin")]
    [HttpPost("api/users")]
    public async Task<IActionResult> CreateDaUser([FromBody] CreateDaUserRequest req)
    {
        if (req.Role != UserRole.DaAdmin && req.Role != UserRole.DaMember)
            return BadRequest("This endpoint is for DA users only. Use POST /api/organisations/{orgId}/users to create org users.");

        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict("A user with this username already exists.");

        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict("A user with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = req.Role,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id },
            new { user.Id, user.Username, user.FirstName, user.LastName, user.Email, user.Role, user.OrgId, user.CreatedAt });
    }

    /// <summary>
    /// Update a user's name, email, or role. DA-level — can change any field including role.
    /// Use PATCH /api/organisations/{orgId}/users/{id} for org-scoped updates.
    /// </summary>
    [Authorize(Policy = "DaAdmin")]
    [HttpPatch("api/users/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (req.Email is not null)
        {
            if (await _db.Users.AnyAsync(u => u.Email == req.Email && u.Id != id))
                return Conflict("A user with this email already exists.");
            user.Email = req.Email;
        }

        if (req.FirstName is not null) user.FirstName = req.FirstName;
        if (req.LastName is not null)  user.LastName = req.LastName;
        if (req.Role.HasValue)         user.Role = req.Role.Value;

        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Username, user.FirstName, user.LastName, user.Email, user.Role, user.OrgId, user.CreatedAt });
    }

    /// <summary>Delete a user. Hard delete for prototype.</summary>
    [Authorize(Policy = "DaAdmin")]
    [HttpDelete("api/users/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Org-scoped endpoints ──────────────────────────────────────────────────

    /// <summary>
    /// Create a user within an organisation. Role must be OrgAdmin or OrgMember.
    /// DaAdmins can create users in any org. OrgAdmins can only create users in their own org.
    /// Use POST /api/users to create a DA user.
    /// </summary>
    [Authorize(Policy = "OrgAdmin")]
    [HttpPost("api/organisations/{orgId:guid}/users")]
    public async Task<IActionResult> CreateOrgUser(Guid orgId, [FromBody] CreateOrgUserRequest req)
    {
        // OrgAdmins can only create users within their own org
        var callerRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (callerRole == UserRole.OrgAdmin.ToString())
        {
            var callerOrgId = User.FindFirst("orgId")?.Value;
            if (callerOrgId != orgId.ToString())
                return Forbid();
        }

        var orgExists = await _db.Organisations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return NotFound("Organisation not found");

        if (req.Role != UserRole.OrgAdmin && req.Role != UserRole.OrgMember)
            return BadRequest("Org users must have role OrgAdmin or OrgMember.");

        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict("A user with this username already exists.");

        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict("A user with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Username = req.Username,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = req.Role,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id },
            new { user.Id, user.Username, user.FirstName, user.LastName, user.Email, user.Role, user.OrgId, user.CreatedAt });
    }

    /// <summary>
    /// Update a user's name or email within an org. Role cannot be changed via this endpoint —
    /// use PATCH /api/users/{id} for role changes.
    /// </summary>
    [Authorize(Policy = "OrgAdmin")]
    [HttpPatch("api/organisations/{orgId:guid}/users/{id:guid}")]
    public async Task<IActionResult> UpdateOrgUser(Guid orgId, Guid id, [FromBody] UpdateOrgUserRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.OrgId == orgId);
        if (user is null) return NotFound();

        if (req.Email is not null)
        {
            if (await _db.Users.AnyAsync(u => u.Email == req.Email && u.Id != id))
                return Conflict("A user with this email already exists.");
            user.Email = req.Email;
        }

        if (req.FirstName is not null) user.FirstName = req.FirstName;
        if (req.LastName is not null)  user.LastName = req.LastName;

        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Username, user.FirstName, user.LastName, user.Email, user.Role, user.OrgId, user.CreatedAt });
    }
}

/// <param name="Username">Permanent login handle. Cannot be changed after creation.</param>
/// <param name="FirstName">User's first name.</param>
/// <param name="LastName">User's last name.</param>
/// <param name="Email">Contact email. Can be updated independently of username.</param>
/// <param name="Password">Initial password. The user should change this after first login.</param>
/// <param name="Role">Must be DaAdmin or DaMember.</param>
public record CreateDaUserRequest(
    [Required(AllowEmptyStrings = false)] string Username,
    [Required(AllowEmptyStrings = false)] string FirstName,
    [Required(AllowEmptyStrings = false)] string LastName,
    [Required(AllowEmptyStrings = false)] string Email,
    [Required(AllowEmptyStrings = false)] string Password,
    UserRole Role);

/// <param name="Username">Permanent login handle. Cannot be changed after creation.</param>
/// <param name="FirstName">User's first name.</param>
/// <param name="LastName">User's last name.</param>
/// <param name="Email">Contact email. Can be updated independently of username.</param>
/// <param name="Password">Initial password. The user should change this after first login.</param>
/// <param name="Role">Must be OrgAdmin or OrgMember.</param>
public record CreateOrgUserRequest(
    [Required(AllowEmptyStrings = false)] string Username,
    [Required(AllowEmptyStrings = false)] string FirstName,
    [Required(AllowEmptyStrings = false)] string LastName,
    [Required(AllowEmptyStrings = false)] string Email,
    [Required(AllowEmptyStrings = false)] string Password,
    UserRole Role);

/// <param name="FirstName">New first name. Omit or pass null to leave unchanged.</param>
/// <param name="LastName">New last name. Omit or pass null to leave unchanged.</param>
/// <param name="Email">New email. Must be unique. Omit or pass null to leave unchanged.</param>
/// <param name="Role">New role. Omit or pass null to leave unchanged.</param>
public record UpdateUserRequest(string? FirstName, string? LastName, string? Email, UserRole? Role);

/// <param name="FirstName">New first name. Omit or pass null to leave unchanged.</param>
/// <param name="LastName">New last name. Omit or pass null to leave unchanged.</param>
/// <param name="Email">New email. Must be unique. Omit or pass null to leave unchanged.</param>
public record UpdateOrgUserRequest(string? FirstName, string? LastName, string? Email);
