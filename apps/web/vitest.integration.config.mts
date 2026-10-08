import { defineConfig } from "vitest/config";

// Boots the built Next.js server once (see test/integration/globalSetup.ts) and drives it
// over HTTP, so the edge proxy guard actually runs. Requires `next build` first.
export default defineConfig({
  test: {
    environment: "node",
    globals: true,
    include: ["test/integration/**/*.test.ts"],
    globalSetup: ["./test/integration/globalSetup.ts"],
    testTimeout: 60_000,
    hookTimeout: 120_000,
    pool: "forks",
    poolOptions: { forks: { singleFork: true } },
  },
});
