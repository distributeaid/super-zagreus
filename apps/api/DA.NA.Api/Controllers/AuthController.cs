using DA.NA.Core.Data;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DA.NA.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DA.NA.Api.Auth.JwtTokenFactory _tokens;

    public AuthController(AppDbContext db, DA.NA.Api.Auth.JwtTokenFactory tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    /// <summary>
    /// Exchange username and password for a JWT token.
    /// Include the token in subsequent requests as: Authorization: Bearer {token}
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Invalid username or password.");

        var (tokenString, expiry) = _tokens.Create(user);

        return Ok(new
        {
            token = tokenString,
            expiresAt = expiry,
            user = new { user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.OrgId }
        });
    }
}

/// <param name="Username">Login username. Unique and permanent — use email to contact the user.</param>
/// <param name="Password">Account password.</param>
public record LoginRequest(
    [Required(AllowEmptyStrings = false)] string Username,
    [Required(AllowEmptyStrings = false)] string Password);
