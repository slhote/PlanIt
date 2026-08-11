import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createWorkItem, updateWorkItem, deleteWorkItem, reorderWorkItems } from "../api/workItems";
import type { CreateWorkItemInput, UpdateWorkItemInput } from "../api/workItems";
import { createProject, type ProjectBoard, type CreateProjectInput } from "../api/projects";
import { addProjectMember, removeProjectMember } from "../api/projectMembers";
import type { Guid, ProjectMemberRole, WorkItemStatus } from "../types/domain";

export function useCreateProjectMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateProjectInput) => createProject(input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
    },
  });
}

export function useCreateWorkItemMutation(projectId: Guid) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateWorkItemInput) => createWorkItem(input),
    onSuccess: (created) => {
      // Structural change (new card) — write straight into the board cache so it
      // appears immediately, same as a live WorkItemCreated broadcast would.
      queryClient.setQueryData<ProjectBoard>(["project", projectId], (old) =>
        old ? { ...old, workItems: [...old.workItems, created] } : old,
      );
      if (created.parentId) {
        queryClient.invalidateQueries({ queryKey: ["feature", created.parentId] });
      }
    },
  });
}

/** Used by drag-and-drop: optimistic cache write, revert on failure. */
export function useUpdateWorkItemStatusMutation(projectId: Guid) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: Guid; status: WorkItemStatus }) => updateWorkItem(id, { status }),
    onMutate: async ({ id, status }) => {
      await queryClient.cancelQueries({ queryKey: ["project", projectId] });
      const previous = queryClient.getQueryData<ProjectBoard>(["project", projectId]);
      queryClient.setQueryData<ProjectBoard>(["project", projectId], (old) =>
        old ? { ...old, workItems: old.workItems.map((w) => (w.id === id ? { ...w, status } : w)) } : old,
      );
      return { previous };
    },
    onError: (_err, _vars, context) => {
      if (context?.previous) {
        queryClient.setQueryData(["project", projectId], context.previous);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ["project", projectId] });
    },
  });
}

/** Used by drag-and-drop reordering within/across columns. Board owns the optimistic visual order itself (via local state), so this just persists it and re-syncs from the server truth on failure. */
export function useReorderWorkItemsMutation(projectId: Guid) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (orderedIds: Guid[]) => reorderWorkItems(orderedIds),
    onError: () => {
      queryClient.invalidateQueries({ queryKey: ["project", projectId] });
    },
  });
}

export function useUpdateWorkItemMutation(opts: { projectId: Guid; featureId?: Guid; workItemId: Guid }) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (patch: UpdateWorkItemInput) => updateWorkItem(opts.workItemId, patch),
    onSuccess: () => {
      // Content-only edit — invalidate rather than patch the cache directly, per
      // the same lazy-refetch treatment a WorkItemUpdated broadcast would get.
      queryClient.invalidateQueries({ queryKey: ["project", opts.projectId] });
      queryClient.invalidateQueries({ queryKey: ["workItem", opts.workItemId] });
      if (opts.featureId) queryClient.invalidateQueries({ queryKey: ["feature", opts.featureId] });
    },
  });
}

export function useDeleteWorkItemMutation(projectId: Guid) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: Guid) => deleteWorkItem(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["project", projectId] });
    },
  });
}

export function useAddProjectMemberMutation(projectId: Guid) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, role }: { userId: Guid; role?: ProjectMemberRole }) =>
      addProjectMember(projectId, userId, role),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projectMembers", projectId] });
    },
  });
}

export function useRemoveProjectMemberMutation(projectId: Guid) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (userId: Guid) => removeProjectMember(projectId, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projectMembers", projectId] });
    },
  });
}
