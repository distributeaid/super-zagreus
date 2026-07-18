# Authentication & Users

## How it works

The API uses JWT (JSON Web Token) authentication. You obtain a token that proves who you are, include it in every subsequent request, and the API uses it to determine what you're allowed to do.

There are two ways to get a token:

- **Password login** (`POST /api/auth/login`) — for DA team users and the seeded admin. See [Logging in](#logging-in).
- **Google/Microsoft sign-in** (`POST /api/auth/session`) — for partner (organisation) users, who have no password. See [Signing in with Google or Microsoft](#signing-in-with-google-or-microsoft-oauth).

Either way you receive the same token, and it expires after 8 hours. You don't need to manage this manually — the Bruno collection handles it automatically (see below).

---

## Roles

There are two tiers of users: DA team and organisation users.

| Role | Tier | What they can do |
|---|---|---|
| `DaAdmin` | DA | Full access. Create/edit/delete orgs, create any user, change roles. |
| `DaMember` | DA | Read access to everything. Cannot create or modify users or orgs. |
| `OrgAdmin` | Org | Full access within their own org — manage users, projects, and assessments. |
| `OrgMember` | Org | Create and submit assessments within their own org. |

DA users have no org and can see all data across all organisations. Org users always belong to a specific organisation and **only ever see data belonging to their own org** — other orgs, projects, and assessments are invisible to them.

---

## First run — seeded admin

On first run a default DA admin is created:

| Field | Value |
|---|---|
| Username | `admin` |
| Password | `ChangeMe123!` |
| Email | `admin@example.com` |

**Change this password immediately after first login in any shared environment.**

Use this account to create your first real DA user, then you can leave the seed account alone or delete it.

---

## Logging in

```
POST /api/auth/login
{
  "username": "admin",
  "password": "ChangeMe123!"
}
```

Response:
```json
{
  "token": "eyJ...",
  "expiresAt": "2026-03-22T20:00:00Z",
  "user": {
    "id": "...",
    "firstName": "DA",
    "lastName": "Admin",
    "email": "admin@example.com",
    "role": "DaAdmin",
    "orgId": null
  }
}
```

Include the token in all subsequent requests:
```
Authorization: Bearer eyJ...
```

---

## Signing in with Google or Microsoft (OAuth)

Partner (organisation) users don't have passwords — they sign in with Google or Microsoft. The web app (`apps/web`) runs the provider sign-in, then exchanges the provider's OIDC **ID token** for an app token:

```
POST /api/auth/session
{
  "idToken": "<the Google/Microsoft ID token>",
  "provider": "google"          // or "microsoft"
}
```

The API verifies the ID token against the provider's published signing keys — signature, issuer, audience (the configured `OAuth:{provider}:ClientId`), and expiry — then maps the **verified email** to a `User`. On success it returns the same `{ token, expiresAt, user }` shape as password login.

It returns **401** when:
- the ID token can't be verified, or the email isn't verified by the provider, or
- the email doesn't match a provisioned user (`"This account is not authorized…"`).

Note that only the **email** is used to authorize — a user does not need a password to sign in this way. See [Provisioning a partner user](#provisioning-a-partner-user-oauth) below, and [docs/local-development.md](../../../docs/local-development.md) for the OAuth client setup and `apps/web/.env.local`.

---

## Who am I? (`GET /api/me`)

Returns the signed-in user's identity, org, and role — used by the web app to learn the caller's `orgId` without hard-coding it:

```
GET /api/me           (requires Authorization: Bearer <token>)

{
  "id": "...",
  "email": "hub@example.org",
  "role": "OrgAdmin",
  "orgId": "...",
  "orgName": "Aegean Hub"
}
```

---

## Using Bruno (recommended)

The Bruno collection handles auth automatically. You just need to set your credentials in the environment:

1. Open Bruno → select the **local** environment → click the edit icon
2. Set `username` and `password` to your account credentials
3. Fire any request — Bruno will log in automatically if you don't have a valid token, and will refresh it when it expires

The `accessToken` and `tokenExpiresAt` variables are managed by Bruno and you don't need to touch them.

---

## Using Swagger UI

Swagger UI requires you to log in manually:

1. Call `POST /api/auth/login` in Swagger to get a token
2. Click the **Authorize** button (padlock icon, top right)
3. Enter `Bearer <your token>` in the value field
4. Click **Authorize** — all subsequent requests in Swagger will include the token

---

## Setting up users for the first time

There are two kinds of users:

- **Password users** (DA team) — created via the API with a `DaAdmin` token (below).
- **Partner users** (organisation) — authorized by email with no password; they sign in
  via Google/Microsoft. Provision them with the console tool (see
  [Provisioning a partner user](#provisioning-a-partner-user-oauth)).

Any user whose email matches their Google/Microsoft account can sign in via OAuth,
regardless of whether they also have a password.

### Creating password users (API)

All user management requires a `DaAdmin` token.

**Create a DA team member:**
```
POST /api/users
{
  "username": "jsmith",
  "firstName": "Jane",
  "lastName": "Smith",
  "email": "jane@example.org",
  "password": "TemporaryPassword1!",
  "role": "DaMember"
}
```

**Create an organisation and its first user:**
```
# 1. Create the org
POST /api/organisations
{ "name": "Aegean Migrant Solidarity" }

# 2. Create an org admin for that org
POST /api/organisations/{orgId}/users
{
  "username": "ahassan",
  "firstName": "Ahmed",
  "lastName": "Hassan",
  "email": "ahmed@ams.org",
  "password": "TemporaryPassword1!",
  "role": "OrgAdmin"
}
```

### Provisioning a partner user (OAuth)

Partner users are authorized by **email, with no password**. The `DA.NA.Provision` console
tool creates an organisation, a project, and an `OrgAdmin` user in one idempotent step:

```
dotnet run --project apps/api/tools/DA.NA.Provision -- \
  --org "Aegean Hub" --region "Greece" --email partner@their-domain.org
```

The email must exactly match the account they sign in with at Google/Microsoft. Re-running
with the same `--org` attaches more users to the existing organisation. Full OAuth setup is
in [docs/local-development.md](../../../docs/local-development.md).

---

## Important notes

- **Usernames are permanent.** They cannot be changed after creation. Email addresses can be updated freely.
- **The JWT signing key** is intentionally absent from `appsettings.json`. The API will refuse to start without it and will print step-by-step instructions in the error message. Short version: run `dotnet user-secrets set "Jwt:Key" "<random-string>"` from the `DA.NA.Api` folder, or set the `JWT__KEY` environment variable.
- **No password reset yet.** In the prototype, a `DaAdmin` can delete and recreate a user if they lose access.
