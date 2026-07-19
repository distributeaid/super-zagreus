import { apiGet } from "./apiClient";

type Me = { id: string; email: string; role: string; orgId: string | null; orgName: string | null };
type Project = { id: string; name: string; region: string | null };
type CurrentAssessment = { submittedAt: string } | null;

/** View model for the dashboard page: the caller's org, their project, and its freshness clock. */
export type Dashboard = {
  orgName: string | null;
  project: Project | null;
  lastConfirmedAt: string | null;
};

/**
 * Assemble the dashboard view model for the signed-in user.
 *
 * Reads the caller's org (`/api/me`), their first project, and that project's
 * current submitted assessment, degrading gracefully at each step: no org, no
 * project, or no submitted assessment each yield `null` fields rather than an error.
 *
 * @returns The org name, the project (or `null`), and the last-confirmed timestamp
 *          (`SubmittedAt` of the current assessment, or `null` if none).
 */
export async function loadDashboard(): Promise<Dashboard> {
  const me = await apiGet<Me>("/api/me");
  if (!me?.orgId) return { orgName: me?.orgName ?? null, project: null, lastConfirmedAt: null };

  const projects = await apiGet<Project[]>(`/api/organisations/${me.orgId}/projects`);
  const project = projects?.[0] ?? null;
  if (!project) return { orgName: me.orgName, project: null, lastConfirmedAt: null };

  const current = await apiGet<CurrentAssessment>(`/api/projects/${project.id}/assessments/current`);
  return { orgName: me.orgName, project, lastConfirmedAt: current?.submittedAt ?? null };
}

/** Resolve the caller's single project (org's first project), or null if none. */
export async function getCurrentProject(): Promise<{ id: string; name: string; region: string | null; orgName: string | null } | null> {
  const me = await apiGet<{ orgId: string | null; orgName: string | null }>("/api/me");
  if (!me?.orgId) return null;
  const projects = await apiGet<{ id: string; name: string; region: string | null }[]>(`/api/organisations/${me.orgId}/projects`);
  const project = projects?.[0];
  return project ? { ...project, orgName: me.orgName } : null;
}
