import { loadDashboard } from "@/data/dashboard";
import { ProjectCard } from "@/components/ProjectCard";

export default async function DashboardPage() {
  const { orgName, project, lastConfirmedAt } = await loadDashboard();
  return (
    <main className="container py-da-xl">
      <h1 className="mb-da-lg font-marker text-3xl">{orgName ?? "Your dashboard"}</h1>
      {project ? (
        <ProjectCard name={project.name} region={project.region} lastConfirmedAt={lastConfirmedAt} />
      ) : (
        <p>No project has been set up for your organization yet.</p>
      )}
    </main>
  );
}
