import { useNavigate } from "react-router";
import { useSimilarWorkItemsQuery } from "../../hooks/queries";
import { WorkItemTypeIcon } from "../../components/icons";
import { statusLabel } from "./WorkItemForm";
import type { Guid } from "../../types/domain";

export function SimilarWorkItems({ projectId, workItemId }: { projectId: Guid; workItemId: Guid }) {
  const navigate = useNavigate();
  const { data } = useSimilarWorkItemsQuery(projectId, workItemId);
  const similarItems = data ?? [];

  if (similarItems.length === 0) {
    return null;
  }

  return (
    <div style={{ marginTop: "var(--space-6)" }}>
      <h2 style={{ fontSize: "var(--font-size-md)", marginBottom: "var(--space-3)" }}>Similar work items</h2>
      <div className="stack">
        {similarItems.map(({ workItem, score }) => {
          const href =
            workItem.workItemType === "Feature"
              ? `/project/${projectId}/feature/${workItem.id}`
              : `/project/${projectId}/task/${workItem.id}`;
          return (
            <button
              key={workItem.id}
              type="button"
              className="card card-interactive"
              style={{ textAlign: "left", width: "100%" }}
              onClick={() => navigate(href)}
            >
              <div className="row-between">
                <span className="row" style={{ gap: "var(--space-2)" }}>
                  <WorkItemTypeIcon type={workItem.workItemType} className="muted" />
                  <span className="card-title">{workItem.title}</span>
                </span>
                <span className="card-meta">{Math.round(score * 100)}% match</span>
              </div>
              <div className="row-between" style={{ marginTop: "var(--space-2)" }}>
                <span className={`status-badge status-${workItem.status}`}>{statusLabel(workItem.status)}</span>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}
