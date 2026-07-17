import { signIn } from "@/auth";
import { Button } from "@/components/ui/Button";

export default function LoginPage() {
  return (
    <main className="container flex min-h-screen flex-col items-center justify-center gap-da-lg">
      <h1 className="font-marker text-4xl text-da-blue">Zagreus</h1>
      <form action={async () => { "use server"; await signIn("google", { redirectTo: "/dashboard" }); }}>
        <Button>Sign in with Google</Button>
      </form>
      <form action={async () => { "use server"; await signIn("microsoft-entra-id", { redirectTo: "/dashboard" }); }}>
        <Button variant="secondary">Sign in with Microsoft</Button>
      </form>
    </main>
  );
}
