import NextAuth from "next-auth";
import Google from "next-auth/providers/google";
import MicrosoftEntraId from "next-auth/providers/microsoft-entra-id";
import { NextResponse } from "next/server";
import { sessionAccess } from "@/data/access";

const API_BASE = process.env.API_BASE_URL!;

export const { handlers, auth, signIn, signOut } = NextAuth({
  providers: [Google, MicrosoftEntraId],
  pages: { signIn: "/login" },
  callbacks: {
    async jwt({ token, account }) {
      // On initial sign-in, exchange the provider id_token for an app JWT.
      if (account?.id_token) {
        const provider = account.provider === "google" ? "google" : "microsoft";
        const res = await fetch(`${API_BASE}/api/auth/session`, {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ idToken: account.id_token, provider }),
        });
        if (!res.ok) {
          token.apiError = true;
          return token;
        }
        const data = (await res.json()) as { token: string; expiresAt: string };
        token.apiToken = data.token;
        token.apiExpiresAt = data.expiresAt;
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
