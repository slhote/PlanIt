import { useEffect, useState } from "react";
import { Navigate } from "react-router";
import { fetchProjectBoard } from "../../api/projects";
import { clearLastProjectId, getLastProjectId } from "./lastProject";

export function LandingRedirect() {
  const [target, setTarget] = useState<string | null | undefined>(undefined);

  useEffect(() => {
    const lastProjectId = getLastProjectId();
    if (!lastProjectId) {
      setTarget(null);
      return;
    }
    fetchProjectBoard(lastProjectId)
      .then(() => setTarget(`/project/${lastProjectId}`))
      .catch(() => {
        clearLastProjectId();
        setTarget(null);
      });
  }, []);

  if (target === undefined) {
    return (
      <div className="page">
        <div className="loading-state">
          <div className="spinner" />
        </div>
      </div>
    );
  }

  return <Navigate to={target ?? "/projects"} replace />;
}
