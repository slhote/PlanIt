import type { User, WorkItem } from "../types/domain";
import { initials } from "./initials";
import { WorkItemTypeIcon } from "./icons";

export function WorkItemCard({
  item,
  assignee,
  subtaskProgress,
}: {
  item: WorkItem;
  assignee?: User;
  subtaskProgress?: { done: number; total: number };
}) {
  return (
    <div className="card work-item-card">
      <div className="row-between">
        <span className="work-item-type-badge">
          <WorkItemTypeIcon type={item.workItemType} />
          {item.workItemType}
        </span>
        {subtaskProgress && subtaskProgress.total > 0 && (
          <span className="board-column-count">
            {subtaskProgress.done}/{subtaskProgress.total}
          </span>
        )}
      </div>
      <div className="card-title" style={{ marginTop: "var(--space-2)" }}>
        {item.title}
      </div>
      {item.tags.length > 0 && (
        <div className="tag-row">
          {item.tags.map((tag) => (
            <span key={tag} className="tag">
              {tag}
            </span>
          ))}
        </div>
      )}
      <div className="work-item-card-footer">
        {assignee ? (
          <span className="row" style={{ gap: "var(--space-2)" }}>
            <span className="avatar avatar-sm">{initials(assignee.username)}</span>
            <span className="card-meta">{assignee.username}</span>
          </span>
        ) : (
          <span className="card-meta">Unassigned</span>
        )}
      </div>
    </div>
  );
}
