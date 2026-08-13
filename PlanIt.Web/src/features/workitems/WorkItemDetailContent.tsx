import { useState } from "react";
import { useNavigate } from "react-router";
import { useFeatureQuery, useProjectMembersQuery, useWorkItemQuery } from "../../hooks/queries";
import { useDeleteWorkItemMutation, useUpdateWorkItemMutation } from "../../hooks/mutations";
import { Modal } from "../../components/Modal";
import { initials } from "../../components/initials";
import { WorkItemForm, statusLabel } from "./WorkItemForm";
import { CreateWorkItemModal } from "./CreateWorkItemModal";
import { HikingShoeIcon, WorkItemTypeIcon } from "../../components/icons";
import type { Guid, WorkItem } from "../../types/domain";

export function WorkItemDetailContent({
  projectId,
  workItemId,
  kind,
  parentFeatureId,
}: {
  projectId: Guid;
  workItemId: Guid;
  kind: "feature" | "task";
  parentFeatureId?: Guid;
}) {
  const navigate = useNavigate();
  const featureQuery = useFeatureQuery(projectId, kind === "feature" ? workItemId : undefined);
  const taskQuery = useWorkItemQuery(projectId, kind === "task" ? workItemId : undefined);
  const parentFeatureQuery = useWorkItemQuery(projectId, kind === "task" ? parentFeatureId : undefined);
  const membersQuery = useProjectMembersQuery(projectId);

  const [editing, setEditing] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [addingTask, setAddingTask] = useState(false);

  const updateWorkItem = useUpdateWorkItemMutation({ projectId, featureId: kind === "feature" ? workItemId : parentFeatureId, workItemId });
  const deleteWorkItem = useDeleteWorkItemMutation(projectId);

  const isLoading = kind === "feature" ? featureQuery.isLoading : taskQuery.isLoading;
  const isError = kind === "feature" ? featureQuery.isError : taskQuery.isError;
  const item: WorkItem | undefined = kind === "feature" ? featureQuery.data?.feature : taskQuery.data;
  const childTasks = featureQuery.data?.childTasks ?? [];
  const members = membersQuery.data ?? [];
  const assignee = members.find((m) => m.userId === item?.assigneeId)?.user;

  if (isLoading) {
    return (
      <div className="loading-state">
        <div className="spinner" />
        <p>Loading…</p>
      </div>
    );
  }

  if (isError || !item) {
    return (
      <div className="error-state">
        <p>Couldn't find this work item. It may have been deleted.</p>
      </div>
    );
  }

  const backHref = kind === "task" && (parentFeatureId ?? item.parentId)
    ? `/project/${projectId}/feature/${parentFeatureId ?? item.parentId}`
    : `/project/${projectId}`;
  const backLabel =
    kind === "task" && parentFeatureQuery.data ? `← Back to ${parentFeatureQuery.data.title}` : "← Back to board";

  return (
    <div>
      <button type="button" className="btn btn-ghost btn-sm" style={{ marginBottom: "var(--space-4)" }} onClick={() => navigate(backHref)}>
        {backLabel}
      </button>

      {editing ? (
        <div className="card">
          <WorkItemForm
            mode="edit"
            initial={{
              workItemType: item.workItemType,
              parentId: item.parentId,
              title: item.title,
              description: item.description,
              status: item.status,
              assigneeId: item.assigneeId,
              tags: item.tags,
            }}
            lockedType={item.workItemType}
            lockedParentId={item.parentId}
            featureOptions={[]}
            members={members}
            submitting={updateWorkItem.isPending}
            submitError={updateWorkItem.isError ? (updateWorkItem.error as Error).message : undefined}
            onCancel={() => setEditing(false)}
            onSubmit={(values) =>
              updateWorkItem.mutate(
                {
                  title: values.title,
                  description: values.description,
                  status: values.status,
                  assigneeId: values.assigneeId,
                  tags: values.tags,
                },
                { onSuccess: () => setEditing(false) },
              )
            }
          />
        </div>
      ) : (
        <div className="stack">
          <div className="row-between">
            <span className="work-item-type-badge">
              <WorkItemTypeIcon type={item.workItemType} />
              {item.workItemType}
            </span>
            <span className={`status-badge status-${item.status}`}>{statusLabel(item.status)}</span>
          </div>

          <h1 style={{ fontSize: "var(--font-size-lg)" }}>{item.title}</h1>

          {item.description && <p style={{ color: "var(--ink-80)" }}>{item.description}</p>}

          <div className="row">
            {assignee ? (
              <span className="row" style={{ gap: "var(--space-2)" }}>
                <span className="avatar avatar-sm">{initials(assignee.username)}</span>
                <span>{assignee.username}</span>
              </span>
            ) : (
              <span className="muted">Unassigned</span>
            )}
          </div>

          {item.tags.length > 0 && (
            <div className="tag-row" style={{ marginTop: 0 }}>
              {item.tags.map((tag) => (
                <span key={tag} className="tag">
                  {tag}
                </span>
              ))}
            </div>
          )}

          <div className="row" style={{ marginTop: "var(--space-2)" }}>
            <button type="button" className="btn btn-secondary btn-sm" onClick={() => setEditing(true)}>
              Edit
            </button>
            <button type="button" className="btn btn-danger btn-sm" onClick={() => setConfirmingDelete(true)}>
              Delete
            </button>
          </div>
        </div>
      )}

      {kind === "feature" && !editing && (
        <div style={{ marginTop: "var(--space-6)" }}>
          <div className="row-between" style={{ marginBottom: "var(--space-3)" }}>
            <h2 style={{ fontSize: "var(--font-size-md)" }}>Tasks</h2>
            <button type="button" className="btn btn-secondary btn-sm" onClick={() => setAddingTask(true)}>
              + Add task
            </button>
          </div>

          {childTasks.length === 0 ? (
            <div className="empty-state">
              <div className="empty-state-icon">📋</div>
              <h3>No tasks yet</h3>
              <p>Break this feature down into tasks your team can pick up.</p>
              <button type="button" className="btn btn-primary btn-sm" onClick={() => setAddingTask(true)}>
                + Add the first task
              </button>
            </div>
          ) : (
            <div className="stack">
              {childTasks.map((task) => (
                <button
                  key={task.id}
                  type="button"
                  className="card card-interactive"
                  style={{ textAlign: "left", width: "100%" }}
                  onClick={() => navigate(`/project/${projectId}/feature/${workItemId}/task/${task.id}`)}
                >
                  <div className="row-between">
                    <span className="row" style={{ gap: "var(--space-2)" }}>
                      <HikingShoeIcon className="muted" />
                      <span className="card-title">{task.title}</span>
                    </span>
                    <span className={`status-badge status-${task.status}`}>{statusLabel(task.status)}</span>
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {confirmingDelete && (
        <Modal title={`Delete ${item.workItemType.toLowerCase()}?`} onClose={() => setConfirmingDelete(false)}>
          <p style={{ marginBottom: "var(--space-4)" }}>
            {childTasks.length > 0
              ? `This will also delete ${childTasks.length} task${childTasks.length === 1 ? "" : "s"} under "${item.title}". This can't be undone.`
              : `"${item.title}" will be permanently deleted. This can't be undone.`}
          </p>
          {deleteWorkItem.isError && <p className="field-error">{(deleteWorkItem.error as Error).message}</p>}
          <div className="thumb-bar" style={{ padding: 0 }}>
            <button type="button" className="btn btn-ghost" onClick={() => setConfirmingDelete(false)}>
              Cancel
            </button>
            <button
              type="button"
              className="btn btn-danger btn-block"
              disabled={deleteWorkItem.isPending}
              onClick={() =>
                deleteWorkItem.mutate(item.id, {
                  onSuccess: () => navigate(backHref),
                })
              }
            >
              {deleteWorkItem.isPending ? "Deleting…" : "Delete"}
            </button>
          </div>
        </Modal>
      )}

      {addingTask && (
        <CreateWorkItemModal
          projectId={projectId}
          lockedType="Task"
          lockedParentId={workItemId}
          onClose={() => setAddingTask(false)}
        />
      )}
    </div>
  );
}
