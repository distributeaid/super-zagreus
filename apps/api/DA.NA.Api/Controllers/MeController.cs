using DA.NA.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DA.NA.Api.Controllers;

/// <summary>Returns the currently signed-in user's identity, org, and role.</summary>
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
                u.Id,
                u.Email,
                Role = u.Role.ToString(),
                u.OrgId,
                OrgName = u.Org != null ? u.Org.Name : null
            })
            .FirstOrDefaultAsync();

        return me is null ? NotFound() : Ok(me);
    }
}
