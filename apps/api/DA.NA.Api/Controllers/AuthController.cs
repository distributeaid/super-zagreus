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
    private readonly DA.NA.Api.Auth.IIdTokenVerifier _verifier;

    public AuthController(AppDbContext db, DA.NA.Api.Auth.JwtTokenFactory tokens, DA.NA.Api.Auth.IIdTokenVerifier verifier)
    {
        _db = db;
        _tokens = tokens;
        _verifier = verifier;
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

    /// <summary>
    /// Exchange a verified Google/Microsoft ID token for an app session JWT.
    /// The verified email must match a DA-provisioned user, otherwise access is denied.
    /// </summary>
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
}

/// <param name="Username">Login username. Unique and permanent — use email to contact the user.</param>
/// <param name="Password">Account password.</param>
public record LoginRequest(
    [Required(AllowEmptyStrings = false)] string Username,
    [Required(AllowEmptyStrings = false)] string Password);

/// <param name="IdToken">The provider ID token obtained from the Google/Microsoft sign-in.</param>
/// <param name="Provider">"google" or "microsoft".</param>
public record SessionRequest(
    [Required(AllowEmptyStrings = false)] string IdToken,
    [Required(AllowEmptyStrings = false)] string Provider);
