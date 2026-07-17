using Microsoft.IdentityModel.Tokens;

namespace DA.NA.Api.Auth;

/// <summary>
/// Validates a token's <c>iss</c> claim against a multi-tenant, templated issuer such
/// as the one published by Microsoft's <c>common</c>/<c>organizations</c> OIDC discovery
/// endpoints: <c>https://login.microsoftonline.com/{tenantid}/v2.0</c>.
///
/// That discovery document's "issuer" field is a literal template — the string
/// "{tenantid}" is not replaced with a real value. A real ID token's issuer instead
/// contains the signing tenant's actual GUID (e.g.
/// "https://login.microsoftonline.com/72f988bf-.../v2.0"), so a plain string comparison
/// against the discovery document's issuer never matches. This validator instead checks
/// that the token's issuer fits the template shape, with a non-empty, slash-free
/// tenant identifier in place of "{tenantid}".
/// </summary>
public static class TemplatedIssuerValidator
{
    private const string Placeholder = "{tenantid}";

    /// <summary>
    /// Returns <paramref name="issuer"/> if it matches <paramref name="templatedIssuer"/>
    /// (either literally, or by fitting the "{tenantid}" template with a concrete,
    /// slash-free tenant segment). Throws <see cref="SecurityTokenInvalidIssuerException"/>
    /// otherwise, per the contract of <see cref="TokenValidationParameters.IssuerValidator"/>.
    /// </summary>
    public static string Validate(string issuer, string templatedIssuer)
    {
        var placeholderIndex = templatedIssuer.IndexOf(Placeholder, StringComparison.Ordinal);

        if (placeholderIndex < 0)
        {
            if (issuer == templatedIssuer) return issuer;
            throw new SecurityTokenInvalidIssuerException(
                $"Issuer '{issuer}' does not match expected issuer '{templatedIssuer}'.");
        }

        var prefix = templatedIssuer[..placeholderIndex];
        var suffix = templatedIssuer[(placeholderIndex + Placeholder.Length)..];

        if (issuer.Length > prefix.Length + suffix.Length
            && issuer.StartsWith(prefix, StringComparison.Ordinal)
            && issuer.EndsWith(suffix, StringComparison.Ordinal))
        {
            var tenantSegment = issuer[prefix.Length..^suffix.Length];
            if (tenantSegment.Length > 0
                && !tenantSegment.Contains('/')
                && tenantSegment != Placeholder)
            {
                return issuer;
            }
        }

        throw new SecurityTokenInvalidIssuerException(
            $"Issuer '{issuer}' does not match templated issuer '{templatedIssuer}'.");
    }
}
