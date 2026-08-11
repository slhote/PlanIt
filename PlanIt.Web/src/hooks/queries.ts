import { useQuery } from "@tanstack/react-query";
import { fetchProjects, fetchProjectBoard } from "../api/projects";
import { fetchFeature, fetchWorkItem } from "../api/workItems";
import { fetchProjectMembers } from "../api/projectMembers";
import { fetchUsers } from "../api/users";
import type { Guid } from "../types/domain";

export function useProjectsQuery() {
  return useQuery({ queryKey: ["projects"], queryFn: fetchProjects });
}

export function useProjectBoardQuery(projectId: Guid | undefined) {
  return useQuery({
    queryKey: ["project", projectId],
    queryFn: () => fetchProjectBoard(projectId as Guid),
    enabled: !!projectId,
    retry: false,
  });
}

export function useFeatureQuery(featureId: Guid | undefined) {
  return useQuery({
    queryKey: ["feature", featureId],
    queryFn: () => fetchFeature(featureId as Guid),
    enabled: !!featureId,
  });
}

export function useWorkItemQuery(workItemId: Guid | undefined) {
  return useQuery({
    queryKey: ["workItem", workItemId],
    queryFn: () => fetchWorkItem(workItemId as Guid),
    enabled: !!workItemId,
  });
}

export function useProjectMembersQuery(projectId: Guid | undefined) {
  return useQuery({
    queryKey: ["projectMembers", projectId],
    queryFn: () => fetchProjectMembers(projectId as Guid),
    enabled: !!projectId,
  });
}

export function useUsersQuery() {
  return useQuery({ queryKey: ["users"], queryFn: fetchUsers });
}
