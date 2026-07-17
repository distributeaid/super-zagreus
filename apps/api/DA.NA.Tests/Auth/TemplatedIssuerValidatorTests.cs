using DA.NA.Api.Auth;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DA.NA.Tests.Auth;

/// <summary>
/// Covers the issuer-matching logic used for Microsoft's multi-tenant "common"
/// discovery endpoint, whose issuer template is
/// "https://login.microsoftonline.com/{tenantid}/v2.0". A real token's issuer contains
/// a concrete tenant GUID in place of "{tenantid}"; a plain string comparison against
/// the raw template would reject every real token, which is the bug this fixes.
/// </summary>
public class TemplatedIssuerValidatorTests
{
    private const string MicrosoftTemplate = "https://login.microsoftonline.com/{tenantid}/v2.0";

    [Fact]
    public void Real_tenant_guid_issuer_is_accepted()
    {
        const string issuer = "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0";

        var result = TemplatedIssuerValidator.Validate(issuer, MicrosoftTemplate);

        Assert.Equal(issuer, result);
    }

    [Fact]
    public void Different_tenant_guid_is_also_accepted()
    {
        const string issuer = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";

        var result = TemplatedIssuerValidator.Validate(issuer, MicrosoftTemplate);

        Assert.Equal(issuer, result);
    }

    [Fact]
    public void Completely_different_domain_is_rejected()
    {
        const string issuer = "https://evil.example.com/some-tenant/v2.0";

        Assert.Throws<SecurityTokenInvalidIssuerException>(
            () => TemplatedIssuerValidator.Validate(issuer, MicrosoftTemplate));
    }

    [Fact]
    public void Literal_placeholder_itself_is_rejected()
    {
        // The raw discovery-document value, unsubstituted, must never be treated as valid —
        // it is not a real tenant identifier.
        Assert.Throws<SecurityTokenInvalidIssuerException>(
            () => TemplatedIssuerValidator.Validate(MicrosoftTemplate, MicrosoftTemplate));
    }

    [Fact]
    public void Missing_tenant_segment_is_rejected()
    {
        const string issuer = "https://login.microsoftonline.com//v2.0";

        Assert.Throws<SecurityTokenInvalidIssuerException>(
            () => TemplatedIssuerValidator.Validate(issuer, MicrosoftTemplate));
    }

    [Fact]
    public void Wrong_suffix_is_rejected()
    {
        const string issuer = "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v1.0";

        Assert.Throws<SecurityTokenInvalidIssuerException>(
            () => TemplatedIssuerValidator.Validate(issuer, MicrosoftTemplate));
    }

    [Fact]
    public void Google_style_fixed_issuer_requires_exact_match()
    {
        const string googleIssuer = "https://accounts.google.com";

        var result = TemplatedIssuerValidator.Validate(googleIssuer, googleIssuer);

        Assert.Equal(googleIssuer, result);
    }

    [Fact]
    public void Google_style_fixed_issuer_rejects_mismatch()
    {
        Assert.Throws<SecurityTokenInvalidIssuerException>(
            () => TemplatedIssuerValidator.Validate("https://not-google.com", "https://accounts.google.com"));
    }
}
