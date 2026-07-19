// Next.js "proxy" (middleware) entry point: run the Auth.js guard on matched routes.
export { auth as proxy } from "@/auth";

/** Restrict the guard to `/dashboard` and `/needs` and their sub-paths. */
export const config = { matcher: ["/dashboard/:path*", "/needs/:path*"] };
