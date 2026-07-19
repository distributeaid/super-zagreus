"use client";

import { useState } from "react";
import { groupByCategory, type NeedItem } from "@/data/needs";
import type { CatalogItem } from "@/data/catalog";
import { AddNeed } from "./AddNeed";
import { NeedRow } from "./NeedRow";
import { ConfirmButton } from "./ConfirmButton";
import { addNeed, updateQuantity, removeNeed, confirmNeeds } from "@/data/needsActions";

/** The editable working draft: add/adjust/remove needs (auto-saved) and confirm. */
export function NeedsEditor({
  draftId,
  items,
  catalog,
}: {
  draftId: string;
  items: NeedItem[];
  catalog: CatalogItem[];
}) {
  const [error, setError] = useState<string | null>(null);

  // Fire a mutation immediately (auto-save) and surface an inline error if it fails,
  // so an edit is never silently dropped.
  function run(action: () => Promise<void>) {
    setError(null);
    action().catch(() => setError("Couldn't save your last change. Please try again."));
  }

  const groups = groupByCategory(items);
  return (
    <main className="container py-da-xl">
      <h1 className="mb-da-lg font-marker text-3xl text-da-blue">Update your needs</h1>
      {error && (
        <p role="alert" className="mb-da-md rounded border border-da-teal bg-da-lavender p-da-sm text-da-blue">
          {error}
        </p>
      )}
      <AddNeed catalog={catalog} onAdd={(item) => run(() => addNeed(draftId, item.id, item.defaultUnit.id))} />
      {groups.map((g) => (
        <section key={g.category} className="mb-da-md">
          <h2 className="text-xl font-medium text-da-blue">{g.category}</h2>
          <ul>
            {g.items.map((need) => (
              <NeedRow
                key={need.id}
                need={need}
                onQuantityChange={(q) => run(() => updateQuantity(draftId, need.id, q))}
                onRemove={() => run(() => removeNeed(draftId, need.id))}
              />
            ))}
          </ul>
        </section>
      ))}
      <div className="mt-da-lg">
        <ConfirmButton disabled={items.length === 0} onConfirm={() => run(() => confirmNeeds(draftId))} />
      </div>
    </main>
  );
}
