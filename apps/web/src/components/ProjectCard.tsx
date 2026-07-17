import { freshnessStatus } from "@/data/freshness";
import { Button } from "@/components/ui/Button";

export function ProjectCard({
  name, region, lastConfirmedAt,
}: { name: string; region: string | null; lastConfirmedAt: string | null }) {
  const status = freshnessStatus(lastConfirmedAt);
  const badge = status === "fresh"
    ? { text: "Up to date", className: "bg-da-green text-da-blue" }
    : { text: "Needs updating", className: "bg-da-lavender text-da-blue" };
  return (
    <section className="rounded border border-da-teal p-da-lg">
      <h2 className="text-xl font-medium">{name}</h2>
      {region && <p className="text-da-blue">{region}</p>}
      <p className={`mt-da-sm inline-block rounded px-da-sm py-da-sm ${badge.className}`}>{badge.text}</p>
      <p className="mt-da-sm text-sm">
        {lastConfirmedAt
          ? `Last confirmed ${new Date(lastConfirmedAt).toLocaleDateString()}`
          : "Not yet confirmed"}
      </p>
      <div className="mt-da-md">
        <Button>Review &amp; confirm needs</Button>
      </div>
    </section>
  );
}
