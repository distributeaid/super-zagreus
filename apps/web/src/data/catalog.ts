/** A single catalog item, flattened out of its category grouping. */
export type CatalogItem = {
  id: string;
  name: string;
  category: string;
  defaultUnit: { id: string; name: string };
};

type RawCategory = {
  category: string;
  items: { id: string; name: string; defaultUnit: { id: string; name: string } }[];
};

/** Flatten the grouped `GET /api/categories` response into a searchable item list. */
export function flattenCatalog(categories: RawCategory[]): CatalogItem[] {
  return categories.flatMap((c) => c.items.map((i) => ({ ...i, category: c.category })));
}

/** Filter catalog items by a case-insensitive substring of the item name. */
export function searchCatalog(items: CatalogItem[], query: string): CatalogItem[] {
  const q = query.trim().toLowerCase();
  if (!q) return items;
  return items.filter((i) => i.name.toLowerCase().includes(q));
}
