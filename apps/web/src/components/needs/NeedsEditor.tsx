"use client";

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
  const groups = groupByCategory(items);
  return (
    <main className="container py-da-xl">
      <h1 className="mb-da-lg font-marker text-3xl text-da-blue">Update your needs</h1>
      <AddNeed catalog={catalog} onAdd={(item) => addNeed(draftId, item.id, item.defaultUnit.id)} />
      {groups.map((g) => (
        <section key={g.category} className="mb-da-md">
          <h2 className="text-xl font-medium text-da-blue">{g.category}</h2>
          <ul>
            {g.items.map((need) => (
              <NeedRow
                key={need.id}
                need={need}
                onQuantityChange={(q) => updateQuantity(draftId, need.id, q)}
                onRemove={() => removeNeed(draftId, need.id)}
              />
            ))}
          </ul>
        </section>
      ))}
      <div className="mt-da-lg">
        <ConfirmButton disabled={items.length === 0} onConfirm={() => confirmNeeds(draftId)} />
      </div>
    </main>
  );
}
