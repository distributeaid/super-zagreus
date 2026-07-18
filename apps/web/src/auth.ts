import NextAuth from "next-auth";
import Google from "next-auth/providers/google";
import MicrosoftEntraId from "next-auth/providers/microsoft-entra-id";
import { NextResponse } from "next/server";
import { sessionAccess } from "@/data/access";
import { exchangeProviderToken } from "@/data/sessionExchange";

const API_BASE = process.env.API_BASE_URL!;

/**
 * Auth.js (NextAuth v5) instance for the web app.
 *
 * Sign-in is delegated to Google/Microsoft; on initial sign-in the provider ID
 * token is exchanged for the backend app JWT ({@link exchangeProviderToken}),
 * which is stored on the session. The `proxy` route guard uses `auth` +
 * {@link sessionAccess} to gate `/dashboard`.
 *
 * - `handlers` — GET/POST route handlers for `/api/auth/*`.
 * - `auth` — read the current session (server components, the proxy guard).
 * - `signIn` / `signOut` — server actions used by the login/access-denied pages.
 */
export const { handlers, auth, signIn, signOut } = NextAuth({
  providers: [Google, MicrosoftEntraId],
  pages: { signIn: "/login" },
  callbacks: {
    async jwt({ token, account }) {
      // On initial sign-in, exchange the provider id_token for an app JWT.
      if (account?.id_token) {
        const provider = account.provider === "google" ? "google" : "microsoft";
        const result = await exchangeProviderToken(account.id_token, provider, API_BASE);
        if ("apiError" in result) {
          token.apiError = true;
        } else {
          token.apiToken = result.apiToken;
          token.apiExpiresAt = result.apiExpiresAt;
        }
      }
      return token;
    },
    async session({ session, token }) {
      session.apiToken = token.apiToken as string | undefined;
      session.apiError = token.apiError as boolean | undefined;
      session.apiExpiresAt = token.apiExpiresAt as string | undefined;
      return session;
    },
    authorized({ auth, request }) {
      const decision = sessionAccess(auth);
      if (decision === "allow") return true;
      // Signed in, but the backend rejected the account → dedicated page.
      if (decision === "denied")
        return NextResponse.redirect(new URL("/access-denied", request.nextUrl));
      // Not signed in, or the app token is missing/expired → re-authenticate.
      // Returning false lets Auth.js redirect to the configured signIn page (/login).
      return false;
    },
  },
});
