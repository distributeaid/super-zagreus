# Authentik Migration

**Status:** Proposed
**Date:** 2026-10-25
**Depends on:** [ADR-001](../docs/adr/001-self-hosted-idp-with-Authentik.md)

## Goals

- Replace Google/Microsoft authentication with self-hosted Authentik as sole IdP for Needs Assessment.
- Users will use Passkey first sign-in, password sign-in as backup method.
- No passwords stored by DA

## Scope

**In scope**

- Frontend: Replace Microsoft/Google NextAuth providers with Authentik
- Backend: Validate Authentik tokens
- Authentik configuration for Needs Assessment (provider, flows, groups)
- Documentation for new Auth procedures

**Out of scope**

- In-app user management UI
- Migrating existing sign-ins, accoutns are in few in number currently. Re-creating in Idp is minimal work
- Baserow authentication handling
- Org-level SSO federation

## Authentik Configuration

- Application/Provider: OAuth2/OpenId provider, confidential client, slug (Type of Id used by Authentik) - `needs-assessment`
- Redirect URIs (Strict):
    - `https://<app-domain>/api/auth/callback/authentik`
    - `http://localhost:3000/api/auth/callback/authentik (development provider only)`
- Scopes: `openid`, `email`, `profile`, and `groups` mapping
- Subject mode: 
- Authentication flow:
    1. Identification stage: Takes email, Passkey flow with password stage attached as backup.
    2. WebAuthn setup stage: Shown only to users without a passkey
    3. User login
- Passkey settings: User Verification required, resident key required
- Groups: `needs-assessment-users`(can sign in), `needs-assessment-admins` (admin features)
- Access policy: group membership policy will restrict sign-in to `needs-assessment-users`
- Users: existing users will be manually created in Authentik and added to groups, each user enrolls a passkey upon first sign-in

## Frontend

### `auth.ts`

- Implement Authentik provider from `next-auth/providers/authentik`
- Request scopes `openid` `email` `profile`
- `jwt` callback: on sign-in, store `access_token`, `refresh_token`, and `expires_at` in session token
- `jwt` callback: refresh access token against `https://<authentik-domain>/application/o/token/` before expiry; on error set `error = "RefreshTokenError"`
- `session` callback: expose `accessToken` and `error` to server code
- Remove Google and Microsoft Entra providers and callbacks

### Other files

- `app/login/page.tsx`: single "Sign In" action calling `signIn("authentik", { redirectTo: "/dashboard" })`
- `data/sessignExchange.ts`: exchangeProviderToken - remove "google" | "microsoft", add "authentik" provider


### Environment Variables
| Variable | Notes |
|---|---|
| `AUTH_AUTHENTIK_ID` | Authentik client ID |
| `AUTH_AUTHENTIK_SECRET` | Authentik client secret (never committed to source control) |
| `AUTH_AUTHENTIK_ISSUER` | `https://<authentik-domain>/application/o/<slug>/` (trailing slash required) |

- Remove all Microsoft and Google variables

## Backend

### Authentication(`Program.cs`)

- `AddJwtBearer`:
    - `ValidIssuer` = Authentik issuer URL (same value as `AUTH_AUTHENTIK_ISSUER`)
    - `ValidAudience` = Authentik client ID
    - `MapInboundClaims = false`, `sub`, `email`, and `groups` keep their names
    - `NameClaimType = "preferred_username"`, `RoleClaimType = "groups"`

### Configuration(`appsettings.json`)

```json
"Authentik": {
  "Issuer": "https://<authentik-domain>/application/o/<slug>/",
  "ClientId": "<client-id>"
}
```

### Authorization

- Policy `DaAdmin` requires `groups` claim `needs-assessment-admins`

### User records

- Add `users.authSubject` (`varchar(255)`)
- Example value: `3f6a9c1e-8b2d-4e7a-9c55-1d2e8f4b7a10`

## References 

- ADR-001: Self-hosted IdP with Authentik
- Authentik documentation: https://docs.goauthentik.io
- Auth.js Authentik provider: https://authjs.dev/getting-started/providers/authentik