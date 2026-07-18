# Local development — credentials & secrets setup

This guide covers the secrets and OAuth credentials you need to run the full stack
locally: the .NET API (`apps/api`) and the Next.js web app (`apps/web`) signing in with
Google/Microsoft.

For first-time .NET / Docker / database setup, see
[apps/api/README.md](../apps/api/README.md) first — this guide assumes the API builds and
Postgres is running.

## The golden rule

**Never commit secrets.** They live in two ignored places:

- **Backend** → .NET user-secrets (stored outside the repo, keyed by the project's
  `UserSecretsId`). Not in `appsettings.json`.
- **Frontend** → `apps/web/.env.local` (gitignored via `.env*.local`).

Client **IDs** are public (they appear in browser URLs during sign-in). Client **secrets**,
`AUTH_SECRET`, and `Jwt:Key` are secret.

## What you need at a glance

| Secret | Lives in | How to get it |
|---|---|---|
| `Jwt:Key` | API user-secrets | Generate: `openssl rand -hex 32` |
| `OAuth:google:ClientId` | API user-secrets | Google OAuth client ID |
| `OAuth:microsoft:ClientId` | API user-secrets | Microsoft app (client) ID |
| `AUTH_SECRET` | `apps/web/.env.local` | Generate: `openssl rand -base64 33` |
| `AUTH_GOOGLE_ID` / `_SECRET` | `apps/web/.env.local` | Google OAuth client |
| `AUTH_MICROSOFT_ENTRA_ID_ID` / `_SECRET` | `apps/web/.env.local` | Microsoft app registration |

The **same** Google client ID goes in *both* the API (`OAuth:google:ClientId`, used to
validate the ID token's audience) and the web app (`AUTH_GOOGLE_ID`). Same for Microsoft.

---

## 1. Backend secrets (`apps/api`)

Run these from the repo root. The `--project` flag is required — `dotnet user-secrets`
targets the project that owns the secret store (here, `DA.NA.Api`, whose
`UserSecretsId` is `da-needs-assessment-api`). Without it you get
`Could not find a MSBuild project file`.

```bash
# JWT signing key for the app's own tokens
dotnet user-secrets set "Jwt:Key" "$(openssl rand -hex 32)" --project apps/api/DA.NA.Api

# OAuth client IDs (fill in after step 2 below)
dotnet user-secrets set "OAuth:google:ClientId"    "<google-client-id>"    --project apps/api/DA.NA.Api
dotnet user-secrets set "OAuth:microsoft:ClientId" "<microsoft-client-id>" --project apps/api/DA.NA.Api
```

Verify:

```bash
dotnet user-secrets list --project apps/api/DA.NA.Api
```

---

## 2. Register the OAuth apps

You only do this once per developer (or share a team app). The redirect URIs below must
match **exactly** — `http` (not https), `localhost` (not `127.0.0.1`), port `3000`.

### Google

1. [Google Cloud Console](https://console.cloud.google.com/) → pick/create a project.
2. **APIs & Services → OAuth consent screen** → **External**. Fill the required fields.
   You can leave it in **Testing** mode — but then **only emails added as test users can
   sign in**, so add your own.
3. **APIs & Services → Credentials → Create Credentials → OAuth client ID** →
   **Web application**.
4. Under **Authorized redirect URIs**, add exactly:
   ```
   http://localhost:3000/api/auth/callback/google
   ```
5. Copy the **Client ID** → `AUTH_GOOGLE_ID` and `OAuth:google:ClientId`.
   Copy the **Client secret** → `AUTH_GOOGLE_SECRET`.

### Microsoft (Entra ID)

1. [Entra admin center](https://entra.microsoft.com/) → **App registrations → New
   registration**.
2. Redirect URI (platform **Web**):
   ```
   http://localhost:3000/api/auth/callback/microsoft-entra-id
   ```
3. Under **Certificates & secrets**, create a **client secret**.
4. Copy the **Application (client) ID** → `AUTH_MICROSOFT_ENTRA_ID_ID` and
   `OAuth:microsoft:ClientId`. Copy the secret **Value** → `AUTH_MICROSOFT_ENTRA_ID_SECRET`.
5. Entra often does **not** emit the `email` claim by default. If sign-in is rejected as
   "not authorized" despite a correct provisioned email, add `email` as an optional claim
   (or a token configuration) on the app registration.

---

## 3. Frontend env (`apps/web/.env.local`)

```bash
cp apps/web/.env.local.example apps/web/.env.local
```

Then fill it in:

- `AUTH_SECRET` — generate with `openssl rand -base64 33`. **Required** — a missing
  secret is the `MissingSecret` / "server configuration" error.
- `API_BASE_URL` — the URL the API actually binds. Default is `http://localhost:54764`
  (see [`launchSettings.json`](../apps/api/DA.NA.Api/Properties/launchSettings.json)).
  **Not** `5000` — on macOS that port is taken by AirPlay/Control Center. Use the
  plain-HTTP port; the https port's self-signed cert breaks the server-side `fetch`.
- `AUTH_GOOGLE_ID` / `AUTH_GOOGLE_SECRET`, and the `AUTH_MICROSOFT_ENTRA_ID_*` pair —
  from step 2.

---

## 4. Authorize yourself (provisioning)

Authentication is delegated to Google/Microsoft, but **authorization** lives in the API:
only an email that maps to a `User` row can sign in. Provision your real sign-in email:

```bash
# Postgres must be running (docker start da-postgres)
dotnet run --project apps/api/tools/DA.NA.Provision -- \
  --org "Aegean Hub" --region "Greece" --email you@your-domain.org
```

This creates (idempotently) an organisation, a project, and an `OrgAdmin` user with no
password (OAuth only). Re-running with the same `--org` name attaches additional users to
the existing org. Confirm:

```bash
docker exec da-postgres psql -U da_user -d da_needs_assessment -c 'SELECT "Email","Role" FROM "Users";'
```

> **After provisioning or changing credentials, sign in fresh.** The provider-token →
> app-JWT exchange only runs on the *initial* sign-in, so an existing browser session
> won't pick up newly-granted access. Use a private window or clear the `localhost:3000`
> cookies, then sign in again.

For API-only work without OAuth, the seed DA admin (`admin` / `ChangeMe123!`) can log in
via `POST /api/auth/login`.

---

## 5. Run the stack

Three processes, each in its own terminal:

```bash
# 1. Database
docker start da-postgres

# 2. API (binds http://localhost:54764)
cd apps/api/DA.NA.Api && dotnet run

# 3. Web
yarn workspace @zagreus/web dev     # http://localhost:3000
```

Open http://localhost:3000 → you're redirected to `/login` → sign in with the provider
matching your provisioned email → land on `/dashboard`.

Restart the web dev server after editing `.env.local` or auth/middleware code — env files
and middleware are read at startup.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `MissingSecret` / "There was a problem with the server configuration" | `AUTH_SECRET` not set | Set it in `apps/web/.env.local`, restart the web server |
| `Could not find a MSBuild project file` (on `dotnet user-secrets`) | Ran from the repo root | Add `--project apps/api/DA.NA.Api` |
| `CallbackRouteError` → `ECONNREFUSED` in the `jwt` callback | API not running, or `API_BASE_URL` points at the wrong port | Start the API; set `API_BASE_URL=http://localhost:54764` |
| `redirect_uri_mismatch` at the provider | Redirect URI doesn't match the registration exactly | Use `http://localhost:3000/api/auth/callback/{google,microsoft-entra-id}` |
| Land on `/access-denied` after sign-in | Your email isn't provisioned | Run the provisioning tool (step 4), then sign in fresh |
| Redirected back to `/login` repeatedly | App token missing/expired | Sign in fresh (private window / clear cookies) |
| Signed in, but access changes aren't reflected | The token exchange only runs on initial sign-in | Sign in fresh — don't just refresh |

See also [apps/api/README.md](../apps/api/README.md) for API usage, Swagger, and the
Bruno collection.
