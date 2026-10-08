// Next.js "proxy" (middleware) entry point: run the Auth.js guard on matched routes.
// Must stay in src/ or Next silently stops running it; test/integration/dashboardGuard.test.ts catches that.
export { auth as proxy } from "@/auth";

/** Restrict the guard to `/dashboard` and its sub-paths. */
export const config = { matcher: ["/dashboard/:path*"] };
