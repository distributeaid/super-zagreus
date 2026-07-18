using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace DA.NA.Api.Auth;

/// <summary>
/// Verifies Google and Microsoft OIDC ID tokens by validating them against each
/// provider's published signing keys (fetched and cached from its OIDC discovery
/// document). The expected audience is the app's OAuth client id for that provider,
/// read from configuration key OAuth:{provider}:ClientId.
/// </summary>
public class OidcIdTokenVerifier : IIdTokenVerifier
{
    private static readonly Dictionary<string, string> MetadataUrls = new()
    {
        ["google"] = "https://accounts.google.com/.well-known/openid-configuration",
        ["microsoft"] = "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration",
    };

    private readonly IConfiguration _config;
    private readonly Dictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers = new();

    public OidcIdTokenVerifier(IConfiguration config)
    {
        _config = config;
        foreach (var (provider, url) in MetadataUrls)
            _managers[provider] = new ConfigurationManager<OpenIdConnectConfiguration>(
                url, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());
    }

    public async Task<VerifiedIdentity?> VerifyAsync(string idToken, string provider)
    {
        if (!_managers.TryGetValue(provider, out var manager)) return null;

        var audience = _config[$"OAuth:{provider}:ClientId"];
        if (string.IsNullOrWhiteSpace(audience)) return null;

        var oidc = await manager.GetConfigurationAsync();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            IssuerSigningKeys = oidc.SigningKeys,
        };

        if (provider == "microsoft")
        {
            // Microsoft's "common" discovery endpoint publishes a templated, multi-tenant
            // issuer ("https://login.microsoftonline.com/{tenantid}/v2.0") rather than a
            // fixed string. A real token's issuer contains the actual tenant GUID in place
            // of "{tenantid}", so a plain ValidIssuer comparison would reject every real
            // sign-in. Use a custom validator that matches the template instead.
            var expectedTemplate = oidc.Issuer;
            parameters.IssuerValidator = (issuer, _, _) =>
                TemplatedIssuerValidator.Validate(issuer, expectedTemplate);
        }
        else
        {
            parameters.ValidIssuer = oidc.Issuer;
        }

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(idToken, parameters);
        if (!result.IsValid) return null;

        var email = result.ClaimsIdentity.FindFirst("email")?.Value;
        if (string.IsNullOrWhiteSpace(email)) return null;

        var verifiedClaim = result.ClaimsIdentity.FindFirst("email_verified")?.Value;
        var verified = verifiedClaim is null
            || verifiedClaim.Equals("true", StringComparison.OrdinalIgnoreCase);

        return new VerifiedIdentity(email, verified);
    }
}
