import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { useProjectBoardQuery, useProjectMembersQuery } from "../../hooks/queries";
import { useUpdateWorkItemStatusMutation, useUpdateWorkItemOrderMutation } from "../../hooks/mutations";
import { Board, computeNewOrder } from "./Board";
import { CreateWorkItemModal } from "../workitems/CreateWorkItemModal";
import { CollaboratorsModal } from "./CollaboratorsModal";
import { setLastProjectId } from "./lastProject";
import { MapIcon } from "../../components/icons";
import { statusLabel } from "../workitems/WorkItemForm";
import { useProjectRealtime } from "../../realtime/useProjectRealtime";
import type { Guid, User, WorkItem, WorkItemStatus } from "../../types/domain";

export function ProjectBoardPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const navigate = useNavigate();
  const boardQuery = useProjectBoardQuery(projectId);
  const membersQuery = useProjectMembersQuery(projectId);
  const updateStatus = useUpdateWorkItemStatusMutation(projectId as Guid);
  const reorder = useUpdateWorkItemOrderMutation(projectId as Guid);
  useProjectRealtime(projectId);

  const [assigneeFilter, setAssigneeFilter] = useState<"all" | "unassigned" | Guid>("all");
  const [creating, setCreating] = useState(false);
  const [managingCollaborators, setManagingCollaborators] = useState(false);
  const [celebration, setCelebration] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  useEffect(() => {
    if (projectId) setLastProjectId(projectId);
  }, [projectId]);

  useEffect(() => {
    if (!celebration) return;
    const timer = setTimeout(() => setCelebration(null), 2600);
    return () => clearTimeout(timer);
  }, [celebration]);

  useEffect(() => {
    if (!saveError) return;
    const timer = setTimeout(() => setSaveError(null), 5000);
    return () => clearTimeout(timer);
  }, [saveError]);

  const usersById = useMemo(() => {
    const map = new Map<Guid, User>();
    (membersQuery.data ?? []).forEach((m) => map.set(m.userId, m.user));
    return map;
  }, [membersQuery.data]);

  const allWorkItems = boardQuery.data?.workItems ?? [];

  const topLevelItems = useMemo(() => {
    return allWorkItems.filter((w) => w.parentId === null);
  }, [allWorkItems]);

  const filteredItems = useMemo(() => {
    if (assigneeFilter === "all") return topLevelItems;
    if (assigneeFilter === "unassigned") return topLevelItems.filter((w) => w.assigneeId === null);
    return topLevelItems.filter((w) => w.assigneeId === assigneeFilter);
  }, [topLevelItems, assigneeFilter]);

  const itemsByStatus = useMemo(() => {
    const grouped: Record<WorkItemStatus, WorkItem[]> = { ToDo: [], InProgress: [], Completed: [] };
    for (const item of filteredItems) grouped[item.status].push(item);
    // The server doesn't guarantee row order — `order` is the actual sort key for a column.
    for (const status of Object.keys(grouped) as WorkItemStatus[]) {
      grouped[status].sort((a, b) => a.order - b.order);
    }
    return grouped;
  }, [filteredItems]);

  function subtaskProgressOf(item: WorkItem) {
    if (item.workItemType !== "Feature") return undefined;
    const children = allWorkItems.filter((w) => w.parentId === item.id);
    return { done: children.filter((c) => c.status === "Completed").length, total: children.length };
  }

  function handleOpenItem(item: WorkItem) {
    if (item.workItemType === "Feature") {
      navigate(`/project/${projectId}/feature/${item.id}`);
    } else if (item.parentId) {
      navigate(`/project/${projectId}/feature/${item.parentId}/task/${item.id}`);
    } else {
      navigate(`/project/${projectId}/task/${item.id}`);
    }
  }

  function handleStatusChange(item: WorkItem, newStatus: WorkItemStatus) {
    updateStatus.mutate(
      { id: item.id, status: newStatus },
      {
        onSuccess: () => {
          if (newStatus === "Completed") {
            setCelebration(`Nice work — "${item.title}" is complete.`);
          }
        },
        onError: (err) => {
          setSaveError(
            `Couldn't move "${item.title}" to ${statusLabel(newStatus)} — ${(err as Error).message}`,
          );
        },
      },
    );
  }

  function handleReorder(orderedItems: WorkItem[], movedItemId: Guid) {
    const order = computeNewOrder(orderedItems, movedItemId);
    reorder.mutate(
      { id: movedItemId, order },
      {
        onError: (err) => {
          setSaveError(`Couldn't save the new order — ${(err as Error).message}`);
        },
      },
    );
  }

  if (boardQuery.isLoading) {
    return (
      <div className="page">
        <div className="loading-state">
          <div className="spinner" />
          <p>Loading board…</p>
        </div>
      </div>
    );
  }

  if (boardQuery.isError || !boardQuery.data) {
    return (
      <div className="page">
        <div className="error-state">
          <p>Couldn't load this project. It may have been deleted, or you no longer have access.</p>
        </div>
      </div>
    );
  }

  const { project } = boardQuery.data;
  const members = membersQuery.data ?? [];

  return (
    <div className="page page-board">
      <div className="page-header">
        <div>
          <div className="row" style={{ gap: "var(--space-2)" }}>
            <MapIcon size={20} />
            <h1>{project.name}</h1>
          </div>
          {project.description && <p className="page-subtitle">{project.description}</p>}
        </div>
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => setManagingCollaborators(true)}>
          👥 {members.length}
        </button>
      </div>

      <select
        className="select"
        style={{ width: "auto", minHeight: 36, fontSize: "var(--font-size-sm)", marginBottom: "var(--space-4)" }}
        value={assigneeFilter}
        onChange={(e) => setAssigneeFilter(e.target.value as "all" | "unassigned" | Guid)}
        aria-label="Filter by assignee"
      >
        <option value="all">All assignees</option>
        <option value="unassigned">Unassigned</option>
        {members.map((m) => (
          <option key={m.userId} value={m.userId}>
            {m.user.username}
          </option>
        ))}
      </select>

      {celebration && (
        <div className="celebration-banner" style={{ marginBottom: "var(--space-4)" }}>
          🎉 {celebration}
        </div>
      )}

      {saveError && (
        <div className="error-state" style={{ marginBottom: "var(--space-4)", textAlign: "left" }}>
          <p>{saveError}</p>
        </div>
      )}

      {topLevelItems.length === 0 ? (
        <div className="empty-state">
          <div className="empty-state-icon">✨</div>
          <h3>This board is empty</h3>
          <p>Add your first Feature or Task to start planning the work.</p>
          <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>
            + Add work item
          </button>
        </div>
      ) : (
        <Board
          itemsByStatus={itemsByStatus}
          assigneeOf={(item) => (item.assigneeId ? usersById.get(item.assigneeId) : undefined)}
          subtaskProgressOf={subtaskProgressOf}
          onOpenItem={handleOpenItem}
          onStatusChange={handleStatusChange}
          onReorder={handleReorder}
        />
      )}

      {topLevelItems.length > 0 && (
        <button type="button" className="fab" onClick={() => setCreating(true)} aria-label="Add work item">
          +
        </button>
      )}

      {creating && projectId && <CreateWorkItemModal projectId={projectId} onClose={() => setCreating(false)} />}
      {managingCollaborators && projectId && (
        <CollaboratorsModal projectId={projectId} onClose={() => setManagingCollaborators(false)} />
      )}
    </div>
  );
}
