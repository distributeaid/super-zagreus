// `proxy.ts` re-exports the Auth.js guard; importing it pulls in `@/auth`, which
// wires up next-auth. Stub it so the module loads in isolation and we can assert
// the route matcher — the only thing this file configures.
vi.mock("@/auth", () => ({ auth: vi.fn() }));

import { config } from "./proxy";

describe("proxy matcher", () => {
  it("guards /dashboard and its sub-paths", () => {
    expect(config.matcher).toContain("/dashboard/:path*");
  });

  it("guards /needs and its sub-paths", () => {
    expect(config.matcher).toContain("/needs/:path*");
  });
});
