using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DA.NA.Core.Entities;
using Microsoft.IdentityModel.Tokens;

namespace DA.NA.Api.Auth;

/// <summary>
/// Issues the application's own JWT for an authenticated user. Centralizes the token
/// shape (claims: sub, role, orgId) so password login and OAuth session issue identical tokens.
/// </summary>
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
            new Claim(ClaimTypes.Role, user.Role.ToString()),
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
