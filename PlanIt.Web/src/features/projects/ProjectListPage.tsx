import { useState } from "react";
import { useNavigate } from "react-router";
import { useProjectsQuery } from "../../hooks/queries";
import { CreateProjectModal } from "./CreateProjectModal";
import { setLastProjectId } from "./lastProject";
import { MapIcon } from "../../components/icons";

export function ProjectListPage() {
  const navigate = useNavigate();
  const projectsQuery = useProjectsQuery();
  const [creating, setCreating] = useState(false);

  function goToProject(projectId: string) {
    setLastProjectId(projectId);
    navigate(`/project/${projectId}`);
  }

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>Your projects</h1>
          <p className="page-subtitle">Pick a project to open its board.</p>
        </div>
      </div>

      {projectsQuery.isLoading && (
        <div className="loading-state">
          <div className="spinner" />
          <p>Loading projects…</p>
        </div>
      )}

      {projectsQuery.isError && (
        <div className="error-state">
          <p>Couldn't load your projects. Try again.</p>
        </div>
      )}

      {projectsQuery.data && projectsQuery.data.length === 0 && (
        <div className="empty-state">
          <div className="empty-state-icon">🗂️</div>
          <h3>No projects yet</h3>
          <p>Create your first project to start breaking work into features and tasks.</p>
          <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>
            + Create your first project
          </button>
        </div>
      )}

      {projectsQuery.data && projectsQuery.data.length > 0 && (
        <div className="stack">
          {projectsQuery.data.map((project) => (
            <button
              key={project.id}
              type="button"
              className="card card-interactive"
              style={{ textAlign: "left", width: "100%", cursor: "pointer" }}
              onClick={() => goToProject(project.id)}
            >
              <div className="row" style={{ gap: "var(--space-2)" }}>
                <MapIcon className="muted" size={18} />
                <span className="card-title" style={{ fontSize: "var(--font-size-md)" }}>
                  {project.name}
                </span>
              </div>
              {project.description && <p className="card-meta">{project.description}</p>}
            </button>
          ))}
        </div>
      )}

      {projectsQuery.data && projectsQuery.data.length > 0 && (
        <button type="button" className="fab" onClick={() => setCreating(true)} aria-label="Create project">
          +
        </button>
      )}

      {creating && (
        <CreateProjectModal
          onClose={() => setCreating(false)}
          onCreated={(projectId) => {
            setCreating(false);
            goToProject(projectId);
          }}
        />
      )}
    </div>
  );
}
