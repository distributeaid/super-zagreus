namespace DA.NA.Api.Auth;

/// <summary>A verified identity extracted from a provider ID token.</summary>
public record VerifiedIdentity(string Email, bool EmailVerified);

/// <summary>
/// Verifies a Google/Microsoft OIDC ID token and returns the verified identity,
/// or null when the token is invalid. Implementations validate signature, issuer,
/// audience, and expiry against the provider's published keys.
/// </summary>
public interface IIdTokenVerifier
{
    Task<VerifiedIdentity?> VerifyAsync(string idToken, string provider);
}
