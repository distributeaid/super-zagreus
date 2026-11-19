"use client";

import { useState } from "react";
import { searchCatalog, type CatalogItem } from "@/data/catalog";

/** Search-only catalog picker: type to filter by name, click a result to add it. */
export function AddNeed({ catalog, onAdd }: { catalog: CatalogItem[]; onAdd: (item: CatalogItem) => void }) {
  const [query, setQuery] = useState("");
  const results = query.trim() ? searchCatalog(catalog, query).slice(0, 10) : [];

  return (
    <div className="mb-da-lg">
      <label className="flex flex-col gap-da-sm">
        <span className="font-medium text-da-blue">Add a need</span>
        <input
          type="search"
          aria-label="Add a need"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search the catalog…"
          className="rounded border border-da-teal px-da-md py-da-sm"
        />
      </label>
      {results.length > 0 && (
        <ul className="mt-da-sm rounded border border-da-teal">
          {results.map((item) => (
            <li key={item.id}>
              <button
                type="button"
                onClick={() => {
                  onAdd(item);
                  setQuery("");
                }}
                className="flex w-full justify-between px-da-md py-da-sm text-left hover:bg-da-lavender"
              >
                <span>{item.name}</span>
                <span className="text-da-blue">{item.category}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
