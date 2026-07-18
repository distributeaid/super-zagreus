import { redirect } from "next/navigation";
import { apiGet } from "@/data/apiClient";
import { getCurrentProject } from "@/data/dashboard";
import { flattenCatalog } from "@/data/catalog";
import { toNeedItems, selectNeedsMode } from "@/data/needs";
import { enterEdit } from "@/data/needsActions";
import { CurrentNeeds } from "@/components/needs/CurrentNeeds";
import { NeedsEditor } from "@/components/needs/NeedsEditor";

type Assessment = {
  id: string;
  status: string;
  submittedAt: string | null;
  items: { id: string; itemTypeId: string; quantity: number; unit: { name: string }; itemType: { name: string; category: string } }[];
};

/** Needs page: read-only View by default, Edit when an open draft exists, Confirm returns to View. */
export default async function NeedsPage() {
  const project = await getCurrentProject();
  if (!project) {
    return <main className="container py-da-xl"><p>No project has been set up for your organization yet.</p></main>;
  }

  const rawCategories = await apiGet<Parameters<typeof flattenCatalog>[0]>("/api/categories");
  const catalog = flattenCatalog(rawCategories ?? []);

  const assessments = await apiGet<{ id: string; status: string }[]>(`/api/projects/${project.id}/assessments`);
  const mode = selectNeedsMode(assessments ?? []);

  if (mode.mode === "edit") {
    const draft = await apiGet<Assessment>(`/api/assessments/${mode.draftId}`);
    return <NeedsEditor draftId={mode.draftId} items={toNeedItems(draft!.items)} catalog={catalog} />;
  }

  // View: read-only current needs (with items), or an empty state for a new hub.
  const current = await apiGet<{ id: string; submittedAt: string | null } | null>(`/api/projects/${project.id}/assessments/current`);
  const items = current ? toNeedItems((await apiGet<Assessment>(`/api/assessments/${current.id}`))!.items) : [];

  async function onReview() {
    "use server";
    await enterEdit(project!.id);
  }

  return <CurrentNeeds items={items} lastConfirmedAt={current?.submittedAt ?? null} onReview={onReview} />;
}
