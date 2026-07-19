"use client";

import type { NeedItem } from "@/data/needs";

/** One editable need: name + unit, a quantity input (fires onQuantityChange), and a remove button. */
export function NeedRow({
  need,
  onQuantityChange,
  onRemove,
}: {
  need: NeedItem;
  onQuantityChange: (quantity: number) => void;
  onRemove: () => void;
}) {
  return (
    <li className="flex items-center gap-da-md border-b border-da-teal py-da-sm">
      <span className="flex-1">{need.name}</span>
      <label className="flex items-center gap-da-sm">
        <span className="sr-only">Quantity for {need.name}</span>
        <input
          type="number"
          min={0}
          aria-label={`Quantity for ${need.name}`}
          defaultValue={need.quantity}
          onChange={(e) => {
            const value = Number(e.target.value);
            if (!Number.isNaN(value) && value > 0) onQuantityChange(value);
          }}
          className="w-20 rounded border border-da-teal px-da-sm py-da-sm"
        />
        <span>{need.unit}</span>
      </label>
      <button type="button" onClick={onRemove} className="text-da-blue underline">
        Remove
      </button>
    </li>
  );
}
