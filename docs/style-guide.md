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
