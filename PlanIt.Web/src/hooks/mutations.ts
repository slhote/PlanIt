import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createWorkItem, updateWorkItem, deleteWorkItem, recomputeSimilarTasks } from "../api/workItems";
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

/**
 * Used by drag-and-drop. A cross-column drag changes both `status` and `order` at once — both are
 * sent in a single PATCH so the move persists as one write instead of two concurrent requests for
 * the same item (which would otherwise race each other's optimistic-concurrency check on the
 * server). Optimistic cache write, revert on failure.
 */
export function useMoveWorkItemMutation(projectId: Guid) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status, order }: { id: Guid; status?: WorkItemStatus; order?: number }) =>
      updateWorkItem(projectId, id, { status, order }),
    onMutate: async ({ id, status, order }) => {
      await queryClient.cancelQueries({ queryKey: ["project", projectId] });
      const previous = queryClient.getQueryData<ProjectBoard>(["project", projectId]);
      queryClient.setQueryData<ProjectBoard>(["project", projectId], (old) =>
        old
          ? {
              ...old,
              workItems: old.workItems.map((w) =>
                w.id === id
                  ? { ...w, ...(status !== undefined ? { status } : {}), ...(order !== undefined ? { order } : {}) }
                  : w,
              ),
            }
          : old,
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

export function useUpdateWorkItemMutation(opts: { projectId: Guid; featureId?: Guid; workItemId: Guid }) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (patch: UpdateWorkItemInput) => updateWorkItem(opts.projectId, opts.workItemId, patch),
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
    mutationFn: (id: Guid) => deleteWorkItem(projectId, id),
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

// No cache invalidation on success -- this only enqueues background work, 
// the embeddings themselves don't land synchronously 
export function useRecomputeSimilarTasksMutation(projectId: Guid) {
  return useMutation({
    mutationFn: () => recomputeSimilarTasks(projectId),
  });
}
