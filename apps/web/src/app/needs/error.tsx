"use client";

import { Button } from "@/components/ui/Button";

/** Error boundary for the needs route — offers a retry / re-sign-in on an unexpected failure. */
export default function NeedsError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <main className="container flex min-h-screen flex-col items-center justify-center gap-da-lg text-center">
      <h1 className="font-marker text-3xl text-da-blue">Something went wrong loading your needs</h1>
      <p>Your session may have expired. Try again, or sign in again.</p>
      <div className="flex gap-da-md">
        <Button onClick={reset} variant="secondary">Try again</Button>
        <a href="/login"><Button>Sign in</Button></a>
      </div>
    </main>
  );
}
