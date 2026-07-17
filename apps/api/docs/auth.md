# Authentication & Users

## How it works

The API uses JWT (JSON Web Token) authentication. When you log in, you receive a token that proves who you are. You include that token in every subsequent request and the API uses it to determine what you're allowed to do.

Tokens expire after 8 hours. You don't need to manage this manually — the Bruno collection handles it automatically (see below).

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

---

## Important notes

- **Usernames are permanent.** They cannot be changed after creation. Email addresses can be updated freely.
- **The JWT signing key** is intentionally absent from `appsettings.json`. The API will refuse to start without it and will print step-by-step instructions in the error message. Short version: run `dotnet user-secrets set "Jwt:Key" "<random-string>"` from the `DA.NA.Api` folder, or set the `JWT__KEY` environment variable.
- **No password reset yet.** In the prototype, a `DaAdmin` can delete and recreate a user if they lose access.
