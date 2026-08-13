import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { ensureConnected, joinProject } from "./signalrClient";
import type { ProjectBoard } from "../api/projects";
import type { Guid, WorkItem, WorkItemStatus } from "../types/domain";

/** Joins the project's SignalR group and keeps the board/members caches in sync with live
 * broadcasts for as long as the calling component is mounted (typically ProjectBoardPage). */
export function useProjectRealtime(projectId: Guid | undefined) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!projectId) return;

    let unsubscribe: (() => void) | undefined;
    let cancelled = false;

    void (async () => {
      const connection = await ensureConnected();
      await joinProject(projectId).catch(() => {});
      if (cancelled) return;

      const updateBoard = (updater: (board: ProjectBoard) => ProjectBoard) => {
        queryClient.setQueryData<ProjectBoard>(["project", projectId], (old) => (old ? updater(old) : old));
      };

      const onCreated = (item: WorkItem) => {
        updateBoard((board) =>
          board.workItems.some((w) => w.id === item.id) ? board : { ...board, workItems: [...board.workItems, item] },
        );
      };
      const onDeleted = (payload: { deletedIds: Guid[] }) => {
        const deleted = new Set(payload.deletedIds);
        updateBoard((board) => ({ ...board, workItems: board.workItems.filter((w) => !deleted.has(w.id)) }));
      };
      const onStatusChanged = (payload: { workItemId: Guid; newStatus: WorkItemStatus }) => {
        updateBoard((board) => ({
          ...board,
          workItems: board.workItems.map((w) => (w.id === payload.workItemId ? { ...w, status: payload.newStatus } : w)),
        }));
      };
      const onMoved = (payload: { workItemId: Guid; status: WorkItemStatus; order: number }) => {
        updateBoard((board) => ({
          ...board,
          workItems: board.workItems.map((w) =>
            w.id === payload.workItemId ? { ...w, status: payload.status, order: payload.order } : w,
          ),
        }));
      };
      // Content-only edits (title/description/tags/assignee) get the lightweight
      // invalidate-and-refetch treatment, same as the local mutation hooks already do for these.
      const onUpdated = () => {
        queryClient.invalidateQueries({ queryKey: ["project", projectId] });
      };
      const onMemberAdded = () => {
        queryClient.invalidateQueries({ queryKey: ["projectMembers", projectId] });
      };
      const onMemberRemoved = () => {
        queryClient.invalidateQueries({ queryKey: ["projectMembers", projectId] });
      };

      connection.on("WorkItemCreated", onCreated);
      connection.on("WorkItemDeleted", onDeleted);
      connection.on("WorkItemStatusChanged", onStatusChanged);
      connection.on("WorkItemMoved", onMoved);
      connection.on("WorkItemUpdated", onUpdated);
      connection.on("ProjectMemberAdded", onMemberAdded);
      connection.on("ProjectMemberRemoved", onMemberRemoved);

      unsubscribe = () => {
        connection.off("WorkItemCreated", onCreated);
        connection.off("WorkItemDeleted", onDeleted);
        connection.off("WorkItemStatusChanged", onStatusChanged);
        connection.off("WorkItemMoved", onMoved);
        connection.off("WorkItemUpdated", onUpdated);
        connection.off("ProjectMemberAdded", onMemberAdded);
        connection.off("ProjectMemberRemoved", onMemberRemoved);
      };
    })();

    return () => {
      cancelled = true;
      unsubscribe?.();
    };
  }, [projectId, queryClient]);
}
