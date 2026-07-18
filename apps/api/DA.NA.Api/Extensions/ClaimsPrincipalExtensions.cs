using System.Security.Claims;

namespace DA.NA.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the caller's orgId if they are an org user, null if they are a DA user.
    /// </summary>
    public static Guid? OrgId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("orgId")?.Value, out var id) ? id : null;
}
