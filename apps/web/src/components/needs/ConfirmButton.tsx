"use client";

import { Button } from "@/components/ui/Button";

/** Confirms the current needs (submits the draft). Disabled when the list is empty. */
export function ConfirmButton({ disabled, onConfirm }: { disabled: boolean; onConfirm: () => void }) {
  return (
    <Button onClick={onConfirm} disabled={disabled}>
      Confirm current needs
    </Button>
  );
}
