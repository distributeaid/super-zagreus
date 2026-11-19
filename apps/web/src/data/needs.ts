/** A need as rendered in the UI, flattened from an API assessment item. */
export type NeedItem = {
  id: string;
  itemTypeId: string;
  name: string;
  category: string;
  quantity: number;
  unit: string;
};

type ApiItem = {
  id: string;
  itemTypeId: string;
  quantity: number;
  unit: { name: string };
  itemType: { name: string; category: string };
};

/** Map API assessment items into flat {@link NeedItem}s for display/editing. */
export function toNeedItems(items: ApiItem[]): NeedItem[] {
  return items.map((i) => ({
    id: i.id,
    itemTypeId: i.itemTypeId,
    name: i.itemType.name,
    category: i.itemType.category,
    quantity: i.quantity,
    unit: i.unit.name,
  }));
}

/** Group needs by category, categories in alphabetical order. */
export function groupByCategory(items: NeedItem[]): { category: string; items: NeedItem[] }[] {
  const byCategory = new Map<string, NeedItem[]>();
  for (const item of items) {
    const list = byCategory.get(item.category) ?? [];
    list.push(item);
    byCategory.set(item.category, list);
  }
  return [...byCategory.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([category, list]) => ({ category, items: list }));
}

/** Decide whether the needs page shows the editor (an open draft exists) or the read-only view. */
export function selectNeedsMode(
  assessments: { id: string; status: string }[],
): { mode: "edit"; draftId: string } | { mode: "view" } {
  const draft = assessments.find((a) => a.status === "Draft");
  return draft ? { mode: "edit", draftId: draft.id } : { mode: "view" };
}
