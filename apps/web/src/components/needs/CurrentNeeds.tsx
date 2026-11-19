import { groupByCategory, type NeedItem } from "@/data/needs";
import { Button } from "@/components/ui/Button";

/** Read-only view of the last confirmed needs, grouped by category, with a CTA to edit. */
export function CurrentNeeds({
  items,
  lastConfirmedAt,
  onReview,
}: {
  items: NeedItem[];
  lastConfirmedAt: string | null;
  onReview: () => void;
}) {
  const groups = groupByCategory(items);
  return (
    <main className="container py-da-xl">
      <div className="mb-da-lg flex items-center justify-between">
        <h1 className="font-marker text-3xl text-da-blue">Your needs</h1>
        <form action={onReview}>
          <Button>Review &amp; update needs</Button>
        </form>
      </div>
      <p className="mb-da-lg text-sm">
        {lastConfirmedAt ? `Last confirmed ${new Date(lastConfirmedAt).toLocaleDateString()}` : "Not yet confirmed"}
      </p>
      {items.length === 0 ? (
        <p>No needs recorded yet.</p>
      ) : (
        groups.map((g) => (
          <section key={g.category} className="mb-da-md">
            <h2 className="text-xl font-medium text-da-blue">{g.category}</h2>
            <ul>
              {g.items.map((i) => (
                <li key={i.id} className="flex justify-between border-b border-da-teal py-da-sm">
                  <span>{i.name}</span>
                  <span>{i.quantity} {i.unit}</span>
                </li>
              ))}
            </ul>
          </section>
        ))
      )}
    </main>
  );
}
