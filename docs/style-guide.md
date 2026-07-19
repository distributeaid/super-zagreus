# Style guide

Conventions for code in this repo. These are the preferences applied in review; keep
new code consistent with them.

## TypeScript / general

- **Docstrings on all exports.** Every exported function and constant has a JSDoc
  docstring explaining its purpose, parameters, and return value.

  ```ts
  /**
   * Classify a needs list's freshness from its last-confirmed timestamp.
   *
   * @param lastConfirmedAt ISO timestamp of the current submitted assessment, or `null`.
   * @param now Reference time (injectable for tests; defaults to now).
   * @returns `"stale"` when never confirmed or older than the window, else `"fresh"`.
   */
  export function freshnessStatus(lastConfirmedAt: string | null, now = new Date()) { … }
  ```

- **Imports at the top of the file.** Keep all `import` statements at the top, above
  other code.

  In test files that mock modules, this still holds: use `vi.hoisted(...)` for any
  values the (hoisted) `vi.mock` factories need, so the real `import` lines stay at the
  top and Vitest hoists the mock setup above them.

## Backend (ASP.NET Core)

- **Never return tracked EF entities from controller actions.** Always project to an
  anonymous object or DTO (`.Select(...)`, or a shared projection helper like
  `AssessmentsController.ToDto`). Tracked entity graphs serialize their EF-fixup
  back-references into cycles (`Items[].Assessment.Project.Assessments…`) and throw a 500 —
  after the DB write has already succeeded, which makes the failure look like a client bug.

- **Every endpoint the frontend calls gets at least one test through the real HTTP
  pipeline** (the `TestBase`/`ApiFactory` integration setup) asserting the status code AND
  deserializing the response body. Unit tests that mock the HTTP layer verify what we
  *send*, not what comes *back* — response-serialization bugs are invisible to them. For
  multi-step flows, prefer a lifecycle smoke test (see
  `DA.NA.Tests/Assessments/NeedsLifecycleSmokeTests.cs`) that drives the whole advertised
  sequence and asserts every response.

## Tests

- **Framework: Vitest** + React Testing Library (config in `apps/web/vitest.config.mts`).
- **Use `it`, not `test`,** for individual cases.
- **Every test lives under at least one `describe` block** — typically named for the
  unit under test.

  ```ts
  import { sessionAccess } from "./access";

  describe("sessionAccess", () => {
    it("denies a session the backend rejected (apiError)", () => {
      expect(sessionAccess({ apiError: true })).toBe("denied");
    });
  });
  ```
