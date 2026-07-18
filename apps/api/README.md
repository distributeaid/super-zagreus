# DistributeAid — Needs Assessment API

Prototype backend. .NET 8 / ASP.NET Core / PostgreSQL.

---

## Contents

- [First time setup](#first-time-setup)
  - [Windows setup](#windows-setup)
  - [Mac setup](#mac-setup)
- [Running the API](#running-the-api-after-initial-setup)
- [Using the API](#using-the-api)
  - [Bruno collection](#bruno-collection)
  - [Authentication](#authentication)
  - [Swagger UI](#swagger-ui)
  - [Quick walkthrough](#quick-walkthrough)
- [Solution structure](#solution-structure)
- [Fixed unit GUIDs](#fixed-unit-guids)

---

## First time setup

Follow the guide for your operating system. If you have never used .NET or Docker before, start here — this guide assumes no prior experience with either.

- [Windows setup](#windows-setup)
- [Mac setup](#mac-setup)

Once set up, see [Running the API](#running-the-api) and [Using the API](#using-the-api).

---

## Windows setup

### Step 1 — Install the .NET 8 SDK

1. Go to https://dotnet.microsoft.com/en-us/download/dotnet/8.0
2. Under **.NET 8.0**, click the **Windows** / **x64** installer
3. Run the downloaded `.exe` and follow the prompts
4. Open a new **Command Prompt** or **PowerShell** window and verify it worked:
   ```
   dotnet --version
   ```
   You should see `8.0.x`. If you see "command not found", restart your terminal and try again.

### Step 2 — Install Docker Desktop

Docker is used to run PostgreSQL (the database) locally without installing it directly on your machine.

1. Go to https://www.docker.com/products/docker-desktop/
2. Download **Docker Desktop for Windows**
3. Run the installer. It may ask you to enable WSL 2 — follow any prompts it gives you and restart if required
4. Once installed, open **Docker Desktop** from the Start menu and wait for it to finish starting (the whale icon in the taskbar should stop animating)
5. Verify Docker is working by opening a terminal and running:
   ```
   docker --version
   ```

### Step 3 — Install the EF Core CLI tool

This is needed to create and manage database migrations.

```
dotnet tool install --global dotnet-ef
```

Close and reopen your terminal after this step.

### Step 4 — Clone the repository

If you have Git installed:
```
git clone <repository-url>
cd da_needs_assessment_proto
```

Or download and unzip the repository from GitHub.

### Step 5 — Start the database

Run this command once to create and start a PostgreSQL container:

```
docker run -d --name da-postgres -e POSTGRES_USER=da_user -e POSTGRES_PASSWORD=da_password -e POSTGRES_DB=da_needs_assessment -p 5432:5432 postgres:16
```

To stop and start it on subsequent days:
```
docker stop da-postgres
docker start da-postgres
```

### Step 6 — Generate the database migration

This only needs to be done once (or whenever the data model changes).

```
cd da_needs_assessment_proto\DA.NA.Api
dotnet ef migrations add InitialCreate --project ..\DA.NA.Core
```

### Step 7 — Run the API

```
cd da_needs_assessment_proto\DA.NA.Api
dotnet run
```

On first run this will create all the database tables and seed reference data. You will see output like:

```
Running database migrations...
Seeding reference data...
Now listening on: http://localhost:54764
```

Open the URL shown in your browser. You should see the Swagger UI — a list of all available API endpoints you can test interactively.

---

## Mac setup

### Step 1 — Install the .NET 8 SDK

1. Go to https://dotnet.microsoft.com/en-us/download/dotnet/8.0
2. Under **.NET 8.0**, click the **macOS** installer for your chip:
   - **Apple Silicon (M1/M2/M3):** choose `Arm64`
   - **Intel Mac:** choose `x64`
3. Run the downloaded `.pkg` file and follow the prompts
4. Open a new **Terminal** window and verify it worked:
   ```
   dotnet --version
   ```
   You should see `8.0.x`. If you see "command not found", restart your terminal and try again.

### Step 2 — Install Docker Desktop

1. Go to https://www.docker.com/products/docker-desktop/
2. Download **Docker Desktop for Mac** — choose the correct version for your chip (Apple Silicon or Intel)
3. Open the downloaded `.dmg`, drag Docker to your Applications folder, then open it
4. Wait for Docker to finish starting (the whale icon in the menu bar should stop animating)
5. Verify it worked:
   ```
   docker --version
   ```

### Step 3 — Install the EF Core CLI tool

```
dotnet tool install --global dotnet-ef
```

If you get a PATH warning after running this, add the following line to your `~/.zshrc` (or `~/.bash_profile` if you use bash):

```
export PATH="$PATH:$HOME/.dotnet/tools"
```

Then run `source ~/.zshrc` (or open a new terminal window).

### Step 4 — Clone the repository

```
git clone <repository-url>
cd da_needs_assessment_proto
```

Or download and unzip the repository from GitHub.

### Step 5 — Start the database

Run this command once to create and start a PostgreSQL container:

```
docker run -d --name da-postgres \
  -e POSTGRES_USER=da_user \
  -e POSTGRES_PASSWORD=da_password \
  -e POSTGRES_DB=da_needs_assessment \
  -p 5432:5432 \
  postgres:16
```

To stop and start it on subsequent days:
```
docker stop da-postgres
docker start da-postgres
```

### Step 6 — Generate the database migration

This only needs to be done once (or whenever the data model changes).

```
cd da_needs_assessment_proto/DA.NA.Api
dotnet ef migrations add InitialCreate --project ../DA.NA.Core
```

### Step 7 — Run the API

```
cd da_needs_assessment_proto/DA.NA.Api
dotnet run
```

On first run this will create all the database tables and seed reference data. You will see output like:

```
Running database migrations...
Seeding reference data...
Now listening on: http://localhost:54764
```

Open the URL shown in your browser. You should see the Swagger UI — a list of all available API endpoints you can test interactively.

---

## Running the API (after initial setup)

Each time you want to run the API:

1. Make sure Docker Desktop is running
2. Start the database container: `docker start da-postgres`
3. From the `DA.NA.Api` folder: `dotnet run`

---

## Adding a migration (when you change the data model)

See **[docs/dotnet-tips.md — Entity Framework Core migrations](docs/dotnet-tips.md#entity-framework-core-migrations)**.

---

## Using the API

### Bruno collection

A [Bruno](https://www.usebruno.com/) collection is included at `devtools/bruno`. Bruno is a free, Git-friendly API client (like Postman, but the collection is just files in the repo).

To use it:
1. Download and install Bruno from https://www.usebruno.com/
2. Open Bruno → **Open Collection** → select the `devtools/bruno` folder
3. Select the **local** environment from the environment dropdown (top right)
4. Run the API (`dotnet run`) then fire requests directly from Bruno

When you add a new endpoint, add a `.bru` file in the relevant folder — Bruno will pick it up immediately.

### Authentication

All endpoints (except `GET /api/categories` and `GET /api/units`) require a JWT token. See **[docs/auth.md](docs/auth.md)** for:
- How to log in
- User roles and what each can access
- Setting up users for the first time
- How Bruno handles tokens automatically

### Swagger UI

The Swagger UI (available at the URL shown when you run the API) lets you test all endpoints interactively.

A few things to know when using Swagger:

- For any field that accepts a GUID and you don't want to set it, replace the placeholder value with `null` — don't leave the auto-filled UUID in place or you'll get a foreign key error
- Log in via `POST /api/auth/login` first, then click the **Authorize** padlock and enter `Bearer <token>`

### Quick walkthrough

```
# Get all item categories and types (use these IDs when adding assessment items)
GET /api/categories

# Get all units
GET /api/units

# Create an organisation
POST /api/organisations
{ "name": "Aegean Migrant Solidarity" }

# Create a frontline org
POST /api/organisations
{ "name": "Refugee Support Lesvos" }

# Create a project under the frontline org
POST /api/organisations/{orgId}/projects
{ "name": "Winter Distribution 2025", "region": "Greece" }

# Create a draft assessment
POST /api/projects/{projectId}/assessments
{ "notes": null }

# Add an item to the assessment
POST /api/assessments/{assessmentId}/items
{ "itemTypeId": "<id from /api/categories>", "quantity": 500, "unitId": "11111111-1111-1111-1111-111111111111", "notes": "prefer size 3-4" }

# Submit the assessment (requires at least one item)
POST /api/assessments/{assessmentId}/submit

# View the current assessment for a project
GET /api/projects/{projectId}/assessments/current
```

---

## Solution structure

| Project | Purpose |
|---|---|
| `DA.NA.Core` | Entities, enums, DbContext, migrations, seed data |
| `DA.NA.Api` | ASP.NET Core entry point — controllers, routing, Swagger |
| `DA.NA.Assessments` | Placeholder — assessment business logic will be extracted here |
| `DA.NA.Staleness` | Placeholder — background staleness jobs and notifications |
| `DA.NA.Analytics` | Placeholder — reporting, exports, historical trends |
| `DA.NA.Tests` | xUnit tests |

---

## Fixed unit GUIDs

These IDs are stable across database rebuilds and can be used directly in test requests.

| Unit | GUID |
|---|---|
| item | `11111111-1111-1111-1111-111111111111` |
| box | `22222222-2222-2222-2222-222222222222` |
| pallet | `33333333-3333-3333-3333-333333333333` |
| kg | `44444444-4444-4444-4444-444444444444` |
| lb | `55555555-5555-5555-5555-555555555555` |
| litre | `66666666-6666-6666-6666-666666666666` |
| gallon | `77777777-7777-7777-7777-777777777777` |
