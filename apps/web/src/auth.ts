import NextAuth from "next-auth";
import Google from "next-auth/providers/google";
import MicrosoftEntraId from "next-auth/providers/microsoft-entra-id";

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
      return session;
    },
    authorized({ auth }) {
      return !!auth?.apiToken && !auth.apiError;
    },
  },
});
