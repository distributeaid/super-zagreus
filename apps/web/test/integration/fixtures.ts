/** Auth.js secret the test server is started with; also used to forge session cookies. */
export const AUTH_SECRET = "integration-test-secret-not-used-anywhere-else";

/** Auth.js session cookie name over plain HTTP (no `__Secure-` prefix); also the JWT salt. */
export const SESSION_COOKIE = "authjs.session-token";

/** Backend app token the stub API accepts as a valid Bearer token. */
export const API_TOKEN = "integration-test-api-token";

/** Organisation id returned by the stub `/api/me`. */
export const ORG_ID = "00000000-0000-0000-0000-0000000000a1";

/** Project id returned by the stub organisation projects endpoint. */
export const PROJECT_ID = "00000000-0000-0000-0000-0000000000b2";

/** Project name the dashboard renders when the guard allows the request. */
export const PROJECT_NAME = "Integration Test Project";
