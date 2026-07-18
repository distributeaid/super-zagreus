"use client";

import { Button } from "@/components/ui/Button";

/**
 * Error boundary for the dashboard route. Catches errors thrown while loading the
 * dashboard (e.g. an unexpected API failure) and offers retry / re-sign-in.
 *
 * @param reset Next.js-provided callback to retry rendering the segment.
 */
export default function DashboardError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <main className="container flex min-h-screen flex-col items-center justify-center gap-da-lg text-center">
      <h1 className="font-marker text-3xl text-da-blue">Something went wrong loading your dashboard</h1>
      <p>Your session may have expired. Try signing in again, or retry the request.</p>
      <div className="flex gap-da-md">
        <Button onClick={reset} variant="secondary">
          Try again
        </Button>
        <a href="/login">
          <Button>Sign in</Button>
        </a>
      </div>
    </main>
  );
}
