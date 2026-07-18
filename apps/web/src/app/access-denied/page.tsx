import { signOut } from "@/auth";
import { Button } from "@/components/ui/Button";

/** Shown when a user authenticated with the provider but isn't provisioned in the API. */
export default function AccessDeniedPage() {
  return (
    <main className="container flex min-h-screen flex-col items-center justify-center gap-da-md text-center">
      <h1 className="font-marker text-3xl text-da-blue">Access not authorized</h1>
      <p className="max-w-md">
        You signed in successfully, but this account isn&apos;t authorized to use the
        portal yet. Ask DistributeAid to provision your access, then sign in again.
      </p>
      <form
        action={async () => {
          "use server";
          await signOut({ redirectTo: "/login" });
        }}
      >
        <Button>Sign out</Button>
      </form>
    </main>
  );
}
