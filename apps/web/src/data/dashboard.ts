import { apiGet } from "./apiClient";

type Me = { id: string; email: string; role: string; orgId: string | null; orgName: string | null };
type Project = { id: string; name: string; region: string | null };
type CurrentAssessment = { submittedAt: string } | null;

export type Dashboard = {
  orgName: string | null;
  project: Project | null;
  lastConfirmedAt: string | null;
};

export async function loadDashboard(): Promise<Dashboard> {
  const me = await apiGet<Me>("/api/me");
  if (!me?.orgId) return { orgName: me?.orgName ?? null, project: null, lastConfirmedAt: null };

  const projects = await apiGet<Project[]>(`/api/organisations/${me.orgId}/projects`);
  const project = projects?.[0] ?? null;
  if (!project) return { orgName: me.orgName, project: null, lastConfirmedAt: null };

  const current = await apiGet<CurrentAssessment>(`/api/projects/${project.id}/assessments/current`);
  return { orgName: me.orgName, project, lastConfirmedAt: current?.submittedAt ?? null };
}
