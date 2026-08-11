import { Routes, Route } from "react-router";
import { LoginPage } from "../auth/LoginPage";
import { RequireAuth } from "../auth/RequireAuth";
import { AppShell } from "../components/AppShell";
import { LandingRedirect } from "../features/projects/LandingRedirect";
import { ProjectListPage } from "../features/projects/ProjectListPage";
import { ProjectBoardPage } from "../features/projects/ProjectBoardPage";
import { WorkItemDetailPage } from "../features/workitems/WorkItemDetailPage";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireAuth>
            <AppShell />
          </RequireAuth>
        }
      >
        <Route path="/" element={<LandingRedirect />} />
        <Route path="/projects" element={<ProjectListPage />} />
        <Route path="/project/:projectId" element={<ProjectBoardPage />} />
        <Route path="/project/:projectId/feature/:featureId" element={<WorkItemDetailPage kind="feature" />} />
        <Route path="/project/:projectId/task/:taskId" element={<WorkItemDetailPage kind="task" />} />
        <Route
          path="/project/:projectId/feature/:featureId/task/:taskId"
          element={<WorkItemDetailPage kind="task" />}
        />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  );
}

function NotFound() {
  return (
    <div className="page">
      <div className="empty-state">
        <div className="empty-state-icon">🧭</div>
        <h3>Page not found</h3>
        <p>That link doesn't match anything in PlanIt.</p>
      </div>
    </div>
  );
}
