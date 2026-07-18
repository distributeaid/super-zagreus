using DA.NA.Core.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DA.NA.Tests.Infrastructure;

/// <summary>
/// Mints JWT tokens for use in tests.
/// Uses the same signing key and claims structure as AuthController so the
/// API's auth middleware accepts them as valid.
/// </summary>
public static class JwtHelper
{
    // Must match the key injected by ApiFactory for tests
    private static readonly string Key = ApiFactory.TestJwtKey;

    public static string ForDaAdmin(Guid? userId = null) =>
        BuildToken(userId ?? Guid.NewGuid(), UserRole.DaAdmin, orgId: null);

    public static string ForDaMember(Guid? userId = null) =>
        BuildToken(userId ?? Guid.NewGuid(), UserRole.DaMember, orgId: null);

    public static string ForOrgAdmin(Guid orgId, Guid? userId = null) =>
        BuildToken(userId ?? Guid.NewGuid(), UserRole.OrgAdmin, orgId);

    public static string ForOrgMember(Guid orgId, Guid? userId = null) =>
        BuildToken(userId ?? Guid.NewGuid(), UserRole.OrgMember, orgId);

    private static string BuildToken(Guid userId, UserRole role, Guid? orgId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("orgId", orgId?.ToString() ?? ""),
        };

        var token = new JwtSecurityToken(
            issuer: "da-needs-assessment",
            audience: "da-needs-assessment",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
