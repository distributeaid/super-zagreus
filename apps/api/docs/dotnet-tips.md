# .NET / C# Tips for New Contributors

A few things that will save you time if you're new to .NET or C#.

---

## Contents

- [VS Code setup](#vs-code-setup)
- [Navigating unfamiliar code](#navigating-unfamiliar-code)
- [Fixing red squiggly lines](#fixing-red-squiggly-lines)
- [Running things](#running-things)
- [Running tests](#running-tests)
- [Debugging with breakpoints](#debugging-with-breakpoints)
- [Understanding the project structure](#understanding-the-project-structure)
- [NuGet packages](#nuget-packages-the-net-equivalent-of-npm)
- [Entity Framework Core migrations](#entity-framework-core-migrations)
- [Reading compiler errors](#reading-compiler-errors)

---

## VS Code setup

Install the **C# Dev Kit** extension (publisher: Microsoft). This one extension pulls in everything: language support, IntelliSense, test runner, solution explorer. Search for `ms-dotnettools.csdevkit` in the extensions panel.

You'll also want:

- **REST Client** (`humao.rest-client`) — run HTTP requests from `.http` files directly in VS Code
- **GitLens** — not .NET-specific but useful for blame/history or use the existing  Source control tab

---

## Navigating unfamiliar code

| What you want | Shortcut |
| --- | --- |
| Jump to definition (where is this class/method defined?) | `F12` |
| Peek at definition without leaving current file | `Alt+F12` |
| Find all usages of this symbol | `Shift+F12` |
| Go back to where you were before jumping | `Alt+←` |
| Rename a symbol everywhere it's used | `F2` |
| See all members of the current file | `Ctrl+Shift+O` |
| Quick-open any file by name | `Ctrl+P` |
| Search across all files | `Ctrl+Shift+F` |

`F12` is particularly useful in this codebase. If you see `User.OrgId()` and wonder where that comes from, put your cursor on it and hit `F12`. VS Code will jump to the extension method, or if it's a .NET framework type, it will decompile and show you the source.

---

## Fixing red squiggly lines

**Missing `using` directive** — if a type is underlined in red and you know it exists, VS Code can add the import for you:

- Click on the underlined word → click the lightbulb (or `Ctrl+.`) → select "using X.Y.Z"
- Or just start typing the type name and accept the IntelliSense suggestion — it adds the using automatically

**Ambiguous type** — if two namespaces have a type with the same name, `Ctrl+.` will show you both and let you pick.

**Build errors vs. editor errors** — VS Code's squiggles are usually right but occasionally lag. If something looks wrong, run `dotnet build` in the terminal to get the authoritative error list with line numbers.

---

## Running things

```bash
# Run the API (from the solution root)
dotnet run --project DA.NA.Api

# Watch mode — restarts the API automatically when you save a file
dotnet watch --project DA.NA.Api
```

---

## Running tests

```bash
# Run all tests
dotnet test

# Run only the test project (faster if you have multiple projects)
dotnet test DA.NA.Tests

# See pass/fail details including output from failing tests
dotnet test --logger "console;verbosity=normal"

# Run a single test class
dotnet test --filter "FullyQualifiedName~OrganisationScopingTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~DaAdmin_GetAll_ReturnsAllOrgs"
```

### How the tests work in this project

These are **integration tests** — they boot the real API in memory and send actual HTTP requests to it. There is no mocking. This means:

- They test the full stack: routing → auth middleware → controller → database → response
- They are slower than unit tests (a few seconds rather than milliseconds) but catch more real bugs
- Each test **method** gets its own isolated in-memory SQLite database — xUnit creates a fresh instance of the test class for every `[Fact]`, so `TestBase`'s constructor runs once per test. This means tests cannot leave dirty data for each other, but it also means you should only seed what the specific test needs rather than sharing setup across tests.

### What JwtHelper is for

Tests need to make authenticated requests. `JwtHelper` mints real JWT tokens that the API's auth middleware accepts, without needing to call the login endpoint first. Use it to put yourself in the shoes of any role:

```csharp
ClientFor(JwtHelper.ForDaAdmin())         // DA admin — sees everything
ClientFor(JwtHelper.ForDaMember())        // DA member — read-only
ClientFor(JwtHelper.ForOrgAdmin(orgId))   // Org admin for a specific org
ClientFor(JwtHelper.ForOrgMember(orgId))  // Org member for a specific org
```

`ClientFor(token)` is defined on `TestBase` and returns an `HttpClient` with the `Authorization: Bearer <token>` header pre-set. `Client` (without a token) is for testing unauthenticated requests.

### Writing a new test

1. Create a file under `DA.NA.Tests/` in a folder that matches what you're testing (e.g. `Projects/`, `Auth/`)
2. Inherit from `TestBase`
3. Seed whatever data you need with `SeedAsync`, then make a request, then assert

```csharp
public class MyNewTests : TestBase
{
    [Fact]
    public async Task SomeScenario_ExpectedOutcome()
    {
        // Arrange: put the database in the state you need
        var orgId = Guid.NewGuid();
        await SeedAsync(async db =>
        {
            db.Organisations.Add(new Organisation { Id = orgId, Name = "Test Org", CreatedAt = DateTime.UtcNow });
        });

        // Act: make the HTTP request
        var client = ClientFor(JwtHelper.ForOrgAdmin(orgId));
        var response = await client.GetAsync($"/api/organisations/{orgId}");

        // Assert: check what came back
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### Running tests in VS Code

The C# Dev Kit adds a **Testing** panel to the sidebar (beaker icon). From there you can run or debug individual tests with a click, and see pass/fail inline next to each test method in the editor.

---

## Debugging with breakpoints

### Setting a breakpoint

Click in the gutter (the space just left of the line numbers) to place a red dot — that's a breakpoint. The program will pause there. Press `F9` to toggle a breakpoint on the current line.

### Debugging the API

Press `F5` to start the API in debug mode (or open the **Run and Debug** panel on the sidebar and click the green play button). The API starts normally but will pause whenever it hits a breakpoint.

### Debugging a test

In the **Testing** panel, each test has a small debug icon (a bug) next to the run button. Click it to run that single test with the debugger attached. The test will pause at any breakpoint you've set — including breakpoints in controller code that the test calls into.

This is the most useful way to understand what the API is actually doing: set a breakpoint in a controller action, then debug a test that calls it, and you can inspect every variable as the request flows through.

### Once paused at a breakpoint

| What you want | Shortcut |
| --- | --- |
| Step over (run the current line, stay at this level) | `F10` |
| Step into (follow the call into the next method) | `F11` |
| Step out (finish the current method, return to caller) | `Shift+F11` |
| Continue to the next breakpoint (or end) | `F5` |
| Stop debugging | `Shift+F5` |

The **Variables** panel on the left shows all local variables and their current values. You can expand objects to drill into their properties. If you want to watch a specific expression across steps, right-click it and choose **Add to Watch**.

### A practical example

If a test is returning an unexpected status code and you don't know why:

1. Set a breakpoint at the top of the relevant controller action
2. Debug the failing test from the Testing panel
3. Step through with `F10`, checking the variables panel at each step
4. When you hit the `return` statement that produces the wrong response, you'll see exactly what condition led there

---

## Understanding the project structure

This solution uses multiple `.csproj` files (projects) collected in one `.sln` file. Each project compiles to its own `.dll`. They reference each other like packages.

If you're in `DA.NA.Api` and want to use something from `DA.NA.Core`, the reference is already set up in the `.csproj` file — you just need the right `using` directive at the top of your file.

To see which projects reference which, look at the `<ProjectReference>` entries in any `.csproj` file.

---

## NuGet packages (the .NET equivalent of npm)

Packages are listed in each `.csproj` file under `<PackageReference>`. To add one:

```bash
dotnet add DA.NA.Api package SomePackage.Name
```

To restore all packages after pulling someone else's changes (equivalent of `npm install`):

```bash
dotnet restore
```

---

## Entity Framework Core migrations

When you change an entity class in `DA.NA.Core/Entities/` (add a field, rename something), the database schema needs to catch up. EF Core tracks this with migration files.

```bash
# Create a new migration after changing an entity
dotnet ef migrations add DescribeWhatChanged --project DA.NA.Core --startup-project DA.NA.Api

# Apply pending migrations to the database
dotnet ef database update --project DA.NA.Core --startup-project DA.NA.Api
```

In this project, migrations are also applied automatically on startup (`SeedData.InitialiseAsync` calls `MigrateAsync`), so for local development you usually don't need to run `database update` manually.

---

## Reading compiler errors

.NET error messages can be verbose. The useful parts are:

- **CS + number** — the error code. Googling "CS1061" will immediately tell you what's wrong.
- **The file path and line number** — always in the format `file.cs(line,col)`.
- The actual message is usually the last sentence before the file path.

Example:

```console
DA.NA.Api/Controllers/OrganisationsController.cs(27,24): error CS1061:
'ClaimsPrincipal' does not contain a definition for 'OrgId'...
```

Means: line 27 of that file, `ClaimsPrincipal` doesn't have `OrgId` — probably a missing `using DA.NA.Api.Extensions;`.
