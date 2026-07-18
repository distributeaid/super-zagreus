import "next-auth";
declare module "next-auth" {
  interface Session {
    apiToken?: string;
    apiError?: boolean;
    apiExpiresAt?: string;
  }
}
