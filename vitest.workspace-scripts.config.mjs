import { defineConfig } from "vitest/config";

// Root-level vitest, scoped to the CI helper scripts in scripts/.
// Deliberately separate from apps/web's vitest so script tests don't pull in
// the web app's jsdom/react setup.
export default defineConfig({
  test: {
    include: ["scripts/__tests__/**/*.test.mjs"],
    environment: "node",
  },
});
