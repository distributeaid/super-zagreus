"use server";

import { revalidatePath } from "next/cache";
import { apiPost, apiPatch, apiDelete } from "./apiClient";

/** Get-or-create the project's working draft, then re-render `/needs` in edit mode. */
export async function enterEdit(projectId: string): Promise<void> {
  await apiPost(`/api/projects/${projectId}/assessments/working-draft`);
  revalidatePath("/needs");
}

/** Add a catalog item to the draft at its default unit, quantity 1. */
export async function addNeed(draftId: string, itemTypeId: string, unitId: string): Promise<void> {
  await apiPost(`/api/assessments/${draftId}/items`, { itemTypeId, unitId, quantity: 1 });
  revalidatePath("/needs");
}

/** Update a need's quantity (auto-save). */
export async function updateQuantity(draftId: string, itemId: string, quantity: number): Promise<void> {
  await apiPatch(`/api/assessments/${draftId}/items/${itemId}`, { quantity });
  revalidatePath("/needs");
}

/** Remove a need from the draft (auto-save). */
export async function removeNeed(draftId: string, itemId: string): Promise<void> {
  await apiDelete(`/api/assessments/${draftId}/items/${itemId}`);
  revalidatePath("/needs");
}

/** Confirm the current needs — submit the draft; freshness resets on the backend. */
export async function confirmNeeds(draftId: string): Promise<void> {
  await apiPost(`/api/assessments/${draftId}/submit`);
  revalidatePath("/needs");
}
